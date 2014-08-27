namespace FsiBotHears

open System
open System.Configuration
open System.IO
open System.Threading
open System.Threading.Tasks
open System.Text.RegularExpressions
open System.Net
open LinqToTwitter
open Microsoft.ServiceBus
open Microsoft.ServiceBus.Messaging

type Message = { StatusId:uint64; User:string; Body:string; }

type Listener () = 
    
    let apiKey = "your key goes here"
    let apiSecret = "your secret goes here"
    let accessToken = "your access token goes here"
    let accessTokenSecret = "your access token secret goes here"
    
    let connection = "your connection string goes here"
    let queueName = "mentions"

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
        
        let queue = QueueClient.CreateFromConnectionString(connection, queueName)

        let queueMention (status:Status) =
            let msg = new BrokeredMessage ()
            msg.MessageId <- status.StatusID |> string
            msg.Properties.["StatusID"] <- status.StatusID
            msg.Properties.["Text"] <- status.Text
            msg.Properties.["Author"] <- status.User.ScreenNameResponse
            queue.Send msg
                                 
        let rec pullMentions() =
            let mentions = 
                query { 
                    for tweet in context.Status do 
                    where (tweet.Type = StatusType.Mentions)
                    select tweet }
                |> Seq.toList

            mentions |> List.iter queueMention
                
            let delay = 
                if (context.RateLimitRemaining > 5)
                then pingInterval
                else (1000 * context.MediaRateLimitReset) + 1000
                                     
            Thread.Sleep delay
            pullMentions ()

        // start the loop
        pullMentions () |> ignore

    member this.Stop () = ignore ()