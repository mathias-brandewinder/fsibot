namespace FsiBotHears

open System.Threading
open LinqToTwitter
open Microsoft.ServiceBus.Messaging
open Microsoft.WindowsAzure.Storage

type Message = { StatusId:uint64; User:string; Body:string; }

type Listener () = 
    
    let apiKey = "your key goes here"
    let apiSecret = "your secret goes here"
    let accessToken = "your access token goes here"
    let accessTokenSecret = "your access token secret goes here"
    
    let queueConnection = "you connection string goes here"
    let blobConnection = "you blob connection goes here"

    let queueName = "mentions"
    let containerName = "lastmention"
    let blobName = "lastID"

    let pingInterval = 1000 * 60 * 2 // poll every 2 minutes

    member this.Start () =

        let context = 
            let credentials = SingleUserInMemoryCredentialStore()
            credentials.ConsumerKey <- apiKey
            credentials.ConsumerSecret <- apiSecret
            credentials.AccessToken <- accessToken
            credentials.AccessTokenSecret <- accessTokenSecret
            let authorizer = SingleUserAuthorizer()
            authorizer.CredentialStore <- credentials
            new TwitterContext(authorizer)
        
        let mentionsQueue = QueueClient.CreateFromConnectionString(queueConnection, queueName)

        let container = 
            let account = CloudStorageAccount.Parse blobConnection
            let client = account.CreateCloudBlobClient ()
            client.GetContainerReference containerName

        let updateLastID (ID:uint64) =
            let lastmention = container.GetBlockBlobReference blobName
            ID |> string |> lastmention.UploadText

        let readLastID () =
            let lastmention = container.GetBlockBlobReference blobName
            if lastmention.Exists ()
            then 
                lastmention.DownloadText () 
                |> System.Convert.ToUInt64
                |> Some
            else None

        let queueMention (status:Status) =
            let msg = new BrokeredMessage ()
            msg.MessageId <- status.StatusID |> string
            msg.Properties.["StatusID"] <- status.StatusID
            msg.Properties.["Text"] <- status.Text
            msg.Properties.["Author"] <- status.User.ScreenNameResponse
            mentionsQueue.Send msg
                                 
        let rec pullMentions(sinceID:uint64 Option) =

            let mentions = 
                match sinceID with
                | None ->
                    query { 
                        for tweet in context.Status do 
                        where (tweet.Type = StatusType.Mentions)
                        select tweet }
                | Some(id) ->
                    query { 
                        for tweet in context.Status do 
                        where (tweet.Type = StatusType.Mentions && tweet.SinceID = id)
                        where (tweet.StatusID <> id)
                        select tweet }
                |> Seq.toList

            mentions |> List.iter queueMention

            let updatedSinceID =
                match mentions with
                | [] -> sinceID
                | hd::_ -> hd.StatusID |> Some
                     
            if (updatedSinceID <> sinceID) 
            then 
                match updatedSinceID with
                | None -> ignore ()
                | Some(ID) -> updateLastID ID      
                
            let delay = 
                if (context.RateLimitRemaining > 5)
                then pingInterval
                else (1000 * context.MediaRateLimitReset) + 1000
                                     
            Thread.Sleep delay
            pullMentions (updatedSinceID)

        // start the loop
        let startID = readLastID ()
        pullMentions startID |> ignore

    member this.Stop () = ignore ()