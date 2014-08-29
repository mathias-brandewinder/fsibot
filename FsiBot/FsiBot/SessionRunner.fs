namespace FsiBot

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Microsoft.FSharp.Compiler.Interactive.Shell

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
            | Some value -> sprintf "%A" value.ReflectionValue
            | None -> sprintf "evaluation produced nothing."
        with e -> sprintf "I'm sorry, I'm afraid I can't do that: %s" e.Message 

    let runSession (timeout:int) (code:string) =    

        let session = createSession ()
        let source = new CancellationTokenSource()
        let token = source.Token     
        let work = Task.Factory.StartNew<string>(
            (fun _ -> evalExpression session code), token)

        if work.Wait(timeout)
        then work.Result
        else 
            source.Cancel ()
            session.Interrupt ()            
            "timeout! I've just picked up a fault in the AE35 unit. It's going to go 100% failure in 72 hours."