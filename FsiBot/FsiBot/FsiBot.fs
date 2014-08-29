namespace FsiBot

open System
open System.Threading
open System.Text.RegularExpressions
open System.Net
open Microsoft.ServiceBus.Messaging
open LinqToTwitter
open FsiBot.SessionRunner
open FsiBot.Filters

type Message = { StatusId:uint64; User:string; Body:string; }

type Bot () = 

    let apiKey = "your key goes here"
    let apiSecret = "your secret goes here"
    let accessToken = "your access token goes here"
    let accessTokenSecret = "your access token secret goes here"
    
    let connection = "you connection string goes here"
    let mentionsQueueName = "mentions"
    
    let pingInterval = 1000 // poll every second
    let helpMessage = "send me an F# expression and I'll do my best to evaluate it. #fsharp"
    let dangerMessage = "this mission is too important for me to allow you to jeopardize it."

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
            let fullText = sprintf "@%s %s" msg.User msg.Body
            let text = 
                if String.length fullText > 140 
                then fullText.Substring(0,134) + " [...]"
                else fullText
            context.ReplyAsync(msg.StatusId, text) |> ignore
            
        let removeBotHandle (text:string) =
            Regex.Replace(text, "@fsibot", "", RegexOptions.IgnoreCase)
                 
        let cleanDoubleSemis (text:string) =
            if text.EndsWith ";;" 
            then text.Substring (0, text.Length - 2)
            else text
        
        let (|Help|_|) (text:string) =
            if (text.Contains("#help"))
            then Some(text)
            else None

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

        let processMention (text:string) =            
            match text with
            | Help _ -> helpMessage
            | Danger _ -> dangerMessage
            | _ ->              
                text
                |> removeBotHandle
                |> cleanDoubleSemis
                |> WebUtility.HtmlDecode
                |> runSession timeout

        let rec pullMentions( ) =
            let mention = mentionsQueue.Receive ()
            match mention with
            | Mention tweet -> 
                tweet.Body
                |> processMention
                |> fun text -> { tweet with Body = text }
                |> respond
                mention.Complete ()
            | _ -> ignore ()

            Thread.Sleep pingInterval
            pullMentions ()

        // start the loop
        pullMentions () |> ignore

    member this.Stop () = ignore ()