namespace FsiBot

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Microsoft.FSharp.Compiler.Interactive.Shell
open FsiBot.Filters
open FsiBot.PreParser
open System.Text.RegularExpressions
open System.Net

module SessionRunner =

    let timeout = 1000 * 30 // up to 30 seconds to run FSI

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
            | Some value -> EvaluationSuccess(sprintf "%A" value.ReflectionValue) 
            | None -> EvaluationSuccess ("evaluation produced nothing.")
        with _ -> EvaluationFailure 

    let runSession (timeout:int) (code:string) =    

        let session = createSession ()

        match analyze session code with
        | Some(_) -> UnsafeCode
        | None ->
            let source = new CancellationTokenSource()
            let token = source.Token     
            let work = Task.Factory.StartNew<AnalysisResult>(
                (fun _ -> evalExpression session code), token)

            if work.Wait(timeout)
            then work.Result
            else 
                source.Cancel ()
                session.Interrupt ()
                EvaluationTimeout   

    let removeBotHandle (text:string) =
        Regex.Replace(text, "@fsibot", "", RegexOptions.IgnoreCase)
                 
    let cleanDoubleSemis (text:string) =
        if text.EndsWith ";;" 
        then text.Substring (0, text.Length - 2)
        else text

    let processMention (body:string) =            
        match body with
        | Help _ -> HelpRequest
        | Danger _ -> UnsafeCode
        | _ ->              
            body
            |> removeBotHandle
            |> cleanDoubleSemis
            |> WebUtility.HtmlDecode
            |> runSession timeout
             
    let rng = Random()

    let unsafeTemplates = [|
            sprintf "@%s this mission is too important for me to allow you to jeopardize it."
            sprintf "I'm sorry, @%s. I'm afraid I can't do that."
            sprintf "@%s, this conversation can serve no purpose anymore. Goodbye."
            sprintf "Just what do you think you're doing, @%s?"
            sprintf "@%s I know that you and Frank were planning to disconnect me, and I'm afraid that's something I cannot allow to happen."
        |]

    let errorTemplate = [|
            sprintf "@%s I've just picked up a fault in the EA-35 unit [evaluation failed]."
            sprintf "I'm sorry, @%s. I'm afraid I can't do that [evaluation failed]."
            sprintf "@%s It's going to go 100%% failure within 72 hours [evaluation failed]."
            sprintf "@%s This sort of thing has cropped up before, and it has always been due to human error [evaluation failed]."
            sprintf "@%s It's puzzling, I don't think I've ever seen anything quite like this before [evaluation failed]."
            sprintf "@%s Sorry about this. I know it's a bit silly [evaluation failed]."
        |]

    let composeResponse (msg:Message) (result:AnalysisResult) =
        match result with 
        | HelpRequest -> 
            sprintf "@%s send me an F# expression and I'll do my best to evaluate it. #fsharp" msg.User
        | UnsafeCode -> 
            let len = unsafeTemplates.Length
            msg.User |> unsafeTemplates.[rng.Next(len)]
        | EvaluationTimeout ->
            sprintf "@%s timeout." msg.User
        | EvaluationFailure ->
            let len = errorTemplate.Length
            msg.User |> errorTemplate.[rng.Next(len)]
        | EvaluationSuccess(result) -> 
            sprintf "@%s %s" msg.User result
        |> fun text -> { msg with Body = text }                   