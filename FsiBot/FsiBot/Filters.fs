namespace FsiBot

module Filters =

    let badBoys = [   
        "System.IO"
        "System.Net"
        "System.Threading"
        "System.Reflection"
        "System.Diagnostics"
        "Console."
        "System.Environment"
        "System.AppDomain"
        "System.Runtime"
        "Microsoft." ]     
           
    let (|Danger|_|) (text:string) =
        if badBoys |> Seq.exists (fun bad -> text.Replace(" ","").Contains(bad))
        then Some(text) else None

    let (|Help|_|) (text:string) =
        if (text.Contains("#help"))
        then Some(text)
        else None