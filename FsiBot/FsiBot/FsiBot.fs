namespace FsiBot

open System
open System.Configuration
open System.IO
open System.Threading
open System.Threading.Tasks
open System.Text.RegularExpressions
open System.Net
open Microsoft.ServiceBus.Messaging
open Microsoft.FSharp.Compiler.Interactive.Shell
open LinqToTwitter

type Message = { StatusId:uint64; User:string; Body:string; }

type Bot () = 

    let apiKey = "your key goes here"
    let apiSecret = "your secret goes here"
    let accessToken = "your access token goes here"
    let accessTokenSecret = "your access token secret goes here"
    
    let connection = ""
    let mentionsQueueName = "mentions"
    
    let pingInterval = 1000 // poll every second
    let timeout = 1000 * 30 // up to 30 seconds to run FSI
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

        let createSession () =
            let sbOut = new Text.StringBuilder()
            let sbErr = new Text.StringBuilder()
            let inStream = new StringReader("")
            let outStream = new StringWriter(sbOut)
            let errStream = new StringWriter(sbErr)

            let argv = [| "C:\\fsi.exe" |]
            let allArgs = Array.append argv [|"--noninteractive"|]

            let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
            FsiEvaluationSession.Create(fsiConfig, allArgs, inStream, outStream, errStream) 
                    
        let evalExpression (fsiSession:FsiEvaluationSession) expression = 
            try 
                match fsiSession.EvalExpression(expression) with
                | Some value -> sprintf "%A" value.ReflectionValue
                | None -> sprintf "evaluation produced nothing."
            with e -> sprintf "I'm sorry, I'm afraid I can't do that: %s" e.Message 
                   
        let respond (msg:Message) =
            let fullText = sprintf "@%s %s" msg.User msg.Body
            let text = 
                if String.length fullText > 140 
                then fullText.Substring(0,134) + " [...]"
                else fullText
            context.ReplyAsync(msg.StatusId, text) |> ignore

        let runSession (code:string) =    

            let session = createSession ()
            let source = new CancellationTokenSource()
            let token = source.Token     
            let work = Task.Factory.StartNew<string>(
                (fun _ -> evalExpression session code), token)

            if work.Wait(timeout)
            then work.Result
            else 
                source.Cancel ()
                "timeout! I've just picked up a fault in the AE35 unit. It's going to go 100% failure in 72 hours."
            
        let removeBotHandle (text:string) =
            Regex.Replace(text, "@fsibot", "", RegexOptions.IgnoreCase)
                 
        let cleanDoubleSemis (text:string) =
            if text.EndsWith ";;" 
            then text.Substring (0, text.Length - 2)
            else text

        let badBoys = 
            [   "System.IO"
                "System.Net"
                "System.Reflection"
                "System.Threading" ]
        
        let (|Danger|_|) (text:string) =
            if badBoys |> Seq.exists (fun bad -> text.Contains(bad))
            then Some(text) else None

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
                |> runSession

        let rec pullMentions( ) =
            let mention = mentionsQueue.Receive ()
            match mention with
            | Mention tweet -> 
                let response = 
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
