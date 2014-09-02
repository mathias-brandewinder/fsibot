namespace FsiBot

open System
open System.Threading
open Microsoft.ServiceBus.Messaging
open LinqToTwitter
open FsiBot.SessionRunner

type Bot () = 

    let apiKey = "your key goes here"
    let apiSecret = "your secret goes here"
    let accessToken = "your access token goes here"
    let accessTokenSecret = "your access token secret goes here"
    
    let connection = "you connection string goes here"
    let mentionsQueueName = "mentions"
    
    let pingInterval = 1000 // poll every second

    member this.Start () =

        let mentionsQueue = QueueClient.CreateFromConnectionString(connection, mentionsQueueName)
        
        let context = 
            let credentials = SingleUserInMemoryCredentialStore()
            credentials.ConsumerKey <- apiKey
            credentials.ConsumerSecret <- apiSecret
            credentials.AccessToken <- accessToken
            credentials.AccessTokenSecret <- accessTokenSecret
            let authorizer = SingleUserAuthorizer()
            authorizer.CredentialStore <- credentials
            new TwitterContext(authorizer)
                                       
        let respond (msg:Message) =
            let fullText = msg.Body
            let text = 
                if String.length fullText > 140 
                then fullText.Substring(0,134) + " [...]"
                else fullText
            context.ReplyAsync(msg.StatusId, text) |> ignore
                    
        let (|Mention|_|) (msg:BrokeredMessage) =
            match msg with
            | null -> None
            | msg ->
                try
                    let statusId = msg.Properties.["StatusID"] |> Convert.ToUInt64
                    let text = msg.Properties.["Text"] |> string
                    let user = msg.Properties.["Author"] |> string
                    Some { StatusId = statusId; Body = text; User = user; }
                with 
                | _ -> None

        let rec pullMentions( ) =
            let mention = mentionsQueue.Receive ()
            match mention with
            | Mention tweet -> 
                tweet.Body
                |> processMention
                |> composeResponse tweet
                |> respond
                mention.Complete ()
            | _ -> ignore ()

            Thread.Sleep pingInterval
            pullMentions ()

        // start the loop
        pullMentions () |> ignore

    member this.Stop () = ignore ()