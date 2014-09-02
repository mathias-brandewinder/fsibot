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

    let composeResponse (msg:Message) (result:AnalysisResult) =
        match result with 
        | HelpRequest -> 
            sprintf "@%s send me an F# expression and I'll do my best to evaluate it. #fsharp" msg.User
        | UnsafeCode -> 
            sprintf "@%s this mission is too important for me to allow you to jeopardize it." msg.User
        | EvaluationTimeout ->
            sprintf "@%s timeout." msg.User
        | EvaluationFailure ->
            sprintf "@%s I've just picked up a fault in the EA-35 unit." msg.User
        | EvaluationSuccess(result) -> 
            sprintf "@%s %s" msg.User result
        |> fun text -> { msg with Body = text }                   