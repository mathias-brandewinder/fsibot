namespace BotWorkerRole

open System
open System.IO
open System.Collections.Generic
open System.Diagnostics
open System.Linq
open System.Net
open System.Threading
open Microsoft.WindowsAzure
open Microsoft.WindowsAzure.Diagnostics
open Microsoft.WindowsAzure.ServiceRuntime
open Microsoft.FSharp.Compiler.Interactive.Shell

type WorkerRole() =
    inherit RoleEntryPoint() 

    // This is a sample worker implementation. Replace with your logic.

    let log message (kind : string) = Trace.TraceInformation(message, kind)

    override wr.Run() =

        log "BotWorkerRole entry point called" "Information"

        let sbOut = new Text.StringBuilder()
        let sbErr = new Text.StringBuilder()
        let inStream = new StringReader("")
        let outStream = new StringWriter(sbOut)
        let errStream = new StringWriter(sbErr)

        let argv = [| "C:\\fsi.exe" |]
        let allArgs = Array.append argv [|"--noninteractive"|]

        let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
        let fsiSession = FsiEvaluationSession.Create(fsiConfig, allArgs, inStream, outStream, errStream) 
        
        let evalExpression text =
            match fsiSession.EvalExpression(text) with
            | Some value -> sprintf "%A" value.ReflectionValue
            | None -> sprintf "Got no result!"

        while(true) do 
            Thread.Sleep(10000)
            log (evalExpression "1 + 1") "Information"
            log "Working" "Information"

    override wr.OnStart() = 

        // Set the maximum number of concurrent connections 
        ServicePointManager.DefaultConnectionLimit <- 12
       
        // For information on handling configuration changes
        // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

        base.OnStart()
