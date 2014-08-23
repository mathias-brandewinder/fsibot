namespace BotWorkerRole

open System
open System.IO
open System.Collections.Generic
open System.Diagnostics
open System.Linq
open System.Net
open System.Threading
open System.Text.RegularExpressions
open Microsoft.WindowsAzure
open Microsoft.WindowsAzure.Diagnostics
open Microsoft.WindowsAzure.ServiceRuntime
open Microsoft.FSharp.Compiler.Interactive.Shell
open LinqToTwitter

type Agent<'a> = MailboxProcessor<'a>

type Message = { StatusId:uint64; User:string; Body:string; }

type WorkerRole() =
    inherit RoleEntryPoint() 

    let log message (kind : string) = Trace.TraceInformation(message, kind)

    let apiKey = "APIKey" |> CloudConfigurationManager.GetSetting
    let apiSecret = "APISecret" |> CloudConfigurationManager.GetSetting 
    let accessToken = "AccessToken" |> CloudConfigurationManager.GetSetting
    let accessTokenSecret = "AccessTokenSecret" |> CloudConfigurationManager.GetSetting

    let mentionsDelay = 1000 * 60 // poll every 1 minute
    let timeout = 1000 * 30 // up to 30 seconds to run FSI
    let helpMessage = "send me an F# expression and I'll do my best to evaluate it. #fsharp"

    override wr.Run() =

        log "BotWorkerRole entry point called" "Information"

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
                | None -> sprintf "Evaluation produced nothing."
            with e -> sprintf "Error: %s" e.Message

        let context = 
            let credentials = SingleUserInMemoryCredentialStore()
            credentials.ConsumerKey <- apiKey
            credentials.ConsumerSecret <- apiSecret
            credentials.AccessToken <- accessToken
            credentials.AccessTokenSecret <- accessTokenSecret
            let authorizer = SingleUserAuthorizer()
            authorizer.CredentialStore <- credentials
            new TwitterContext(authorizer)

        let respond(msg:Message) =
            log ("Responding: " + msg.Body) "Information"
            let fullText = sprintf "@%s %s" msg.User msg.Body
            let text = 
                if String.length fullText > 140 
                then fullText.Substring(0,134) + " [...]"
                else fullText
            context.ReplyAsync(msg.StatusId, text) |> ignore

        let runSession (msg:Message) = 
            log ("FSI session: " + msg.Body) "Information"
            let session = createSession ()
            let evaluate code = async { return evalExpression session code }
            let result =
                try Async.RunSynchronously(evaluate (msg.Body), timeout)
                with 
                | _ -> 
                    session.Interrupt ()
                    "timeout!"            
            { StatusId = msg.StatusId; User = msg.User; Body = result}
            |> respond

        let processMentions (msg:Status) = 
            log ("Processing: " + msg.Text) "Information"
            if (msg.Text.Contains("#help"))
            then respond { StatusId = msg.StatusID; User = msg.User.ScreenNameResponse; Body = helpMessage }
            else
                let code = 
                    Regex.Replace(msg.Text, "@fsibot", "", RegexOptions.IgnoreCase)
                    |> WebUtility.HtmlDecode
                { StatusId = msg.StatusID; User = msg.User.ScreenNameResponse; Body = code }
                |> runSession                  

        let rec pullMentions(sinceId:uint64 Option) =
            async {
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

                mentions |> List.iter processMentions
                
                log (sprintf "Rate remaining %i - waiting" context.RateLimitRemaining) "Information"
                log (sprintf "Rate current %i - waiting" context.RateLimitCurrent) "Information"

                let sinceId =
                    match mentions with
                    | [] -> sinceId
                    | hd::_ -> hd.StatusID |> Some
                
                let delay = 
                    if (context.RateLimitRemaining > 2)
                    then mentionsDelay
                    else (1000 * context.MediaRateLimitReset) + 1000
                                     
                do! Async.Sleep delay
                return! pullMentions (sinceId) }

        pullMentions (None) |> Async.RunSynchronously