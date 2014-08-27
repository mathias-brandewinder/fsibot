namespace FsiBot

open System
open System.Configuration
open System.IO
open System.Threading
open System.Threading.Tasks
open System.Text.RegularExpressions
open System.Net
open LinqToTwitter
open Microsoft.FSharp.Compiler.Interactive.Shell

type Message = { StatusId:uint64; User:string; Body:string; }

type Bot () = 
    
    let apiKey = "your key goes here"
    let apiSecret = "your secret goes here"
    let accessToken = "your access token goes here"
    let accessTokenSecret = "your access token secret goes here"
    
    let pingInterval = 1000 * 60 * 2 // poll every 2 minutes
    let timeout = 1000 * 30 // up to 30 seconds to run FSI
    let helpMessage = "send me an F# expression and I'll do my best to evaluate it. #fsharp"
    let dangerMessage = "this mission is too important for me to allow you to jeopardize it."

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
            with e -> sprintf "error: %s" e.Message 
                   
        let respond (msg:Message) =
            let fullText = sprintf "@%s %s" msg.User msg.Body
            let text = 
                if String.length fullText > 140 
                then fullText.Substring(0,134) + " [...]"
                else fullText
            context.ReplyAsync(msg.StatusId, text) |> ignore

        let runSession (msg:Message) =    

            let session = createSession ()
            let source = new CancellationTokenSource()
            let token = source.Token     
            let work = Task.Factory.StartNew<string>(
                (fun _ -> evalExpression session msg.Body), token)

            let result = 
                if work.Wait(timeout)
                then work.Result
                else 
                    source.Cancel ()
                    "timeout!"

            { msg with Body = result }
            |> respond
            
        let removeBotHandle (text:string) =
            Regex.Replace(text, "@fsibot", "", RegexOptions.IgnoreCase)
                 
        let cleanDoubleSemis (text:string) =
            if text.EndsWith ";;" 
            then text.Substring (0, text.Length - 2)
            else text

        let badBoys = 
            [   "System.IO"
                "System.Net" ]
        
        let (|Danger|_|) (text:string) =
            if badBoys |> Seq.exists (fun bad -> text.Contains(bad))
            then Some(text) else None

        let (|Help|_|) (text:string) =
            if (text.Contains("#help"))
            then Some(text)
            else None

        let preprocessMention (msg:Status) =
            match (msg.Text) with
            | Help _ -> respond { StatusId = msg.StatusID; User = msg.User.ScreenNameResponse; Body = helpMessage }
            | Danger _ -> respond { StatusId = msg.StatusID; User = msg.User.ScreenNameResponse; Body = dangerMessage }
            | _ ->              
                let code = 
                    msg.Text
                    |> removeBotHandle
                    |> cleanDoubleSemis
                    |> WebUtility.HtmlDecode
                { StatusId = msg.StatusID; User = msg.User.ScreenNameResponse; Body = code }
                |> runSession

        let rec pullMentions(sinceId:uint64 Option) =
            let mentions = 
                match sinceId with
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

            mentions |> List.iter preprocessMention

            let sinceId =
                match mentions with
                | [] -> sinceId
                | hd::_ -> hd.StatusID |> Some
                
            let delay = 
                if (context.RateLimitRemaining > 5)
                then pingInterval
                else (1000 * context.MediaRateLimitReset) + 1000
                                     
            Thread.Sleep delay
            pullMentions sinceId

        // start the loop
        pullMentions None |> ignore

    member this.Stop () = ignore ()