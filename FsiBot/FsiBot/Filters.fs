namespace FsiBot

type Message = { StatusId:uint64; User:string; Body:string; }

type AnalysisResult =
    | HelpRequest
    | UnsafeCode
    | EvaluationTimeout
    | EvaluationFailure
    | EvaluationSuccess of string

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
           
    let (|Danger|_|) (body:string) =
        if badBoys |> Seq.exists (fun bad -> body.Replace(" ","").Contains(bad))
        then Some(UnsafeCode) 
        else None

    let (|Help|_|) (body:string) =
        if (body.Contains("#help"))
        then Some(HelpRequest)
        else None

module PreParser =

    open Microsoft.FSharp.Compiler.Ast
    open Microsoft.FSharp.Compiler.Interactive.Shell

    type SecurityAnalysis =
        | Unsafe
        | Safe

    let blacklist = 
        [ 
            ["System"; "IO";] |> Set.ofList
            ["System"; "Net";] |> Set.ofList
            ["System"; "Threading";] |> Set.ofList
            ["System"; "Reflection";] |> Set.ofList
            ["System"; "Diagnostics";] |> Set.ofList
            ["System"; "Environment";] |> Set.ofList
            ["System"; "AppDomain";] |> Set.ofList
            ["System"; "Runtime";] |> Set.ofList
            ["Console"; ] |> Set.ofList
            ["Microsoft"; ] |> Set.ofList
        ]
   
    let isUnsafe (x:LongIdentWithDots) =
        match x with 
        | LongIdentWithDots(a,_) ->
            let is = a |> List.map string |> Set.ofList
            blacklist |> List.exists (fun x -> Set.isSubset x is)

    // TODO clean up this horrifying piece of code
    // TODO check for all SynBinding
    let rec analyzeExp (exp:SynExpr) =
        match exp with
        | SynExpr.AddressOf(_,e,_,_) -> analyzeExp e
        | SynExpr.App(_,_,e1,e2,_) -> 
            if analyzeExp e1 = Unsafe then Unsafe
            elif analyzeExp e2 = Unsafe then Unsafe
            else Safe
        | SynExpr.ArbitraryAfterError(_,_) -> Safe
        | SynExpr.ArrayOrList(_,list,_) ->
            if (list |> Seq.exists (fun e -> analyzeExp e = Unsafe))
            then Unsafe
            else Safe
        | SynExpr.ArrayOrListOfSeqExpr(_,e,_) -> analyzeExp e
        | SynExpr.Assert(e,_) -> analyzeExp e
        | SynExpr.CompExpr(_,_,e,_) -> analyzeExp e
        | SynExpr.Const(_,_) -> Safe
        | SynExpr.DiscardAfterMissingQualificationAfterDot(e,_) -> analyzeExp e
        | SynExpr.Do(e,_) -> analyzeExp e
        | SynExpr.DoBang(e,_) -> analyzeExp e
        | SynExpr.DotGet(e,_,_,_) -> analyzeExp e // long ident? check
        | SynExpr.DotIndexedGet(e,_,_,_) -> analyzeExp e
        | SynExpr.DotIndexedSet(e1,_,e2,_,_,_) -> 
            if analyzeExp e1 = Unsafe then Unsafe
            elif analyzeExp e2 = Unsafe then Unsafe
            else Safe
        | SynExpr.DotNamedIndexedPropertySet(e1,_,e2,e3,_) -> 
            if analyzeExp e1 = Unsafe then Unsafe
            elif analyzeExp e2 = Unsafe then Unsafe
            elif analyzeExp e3 = Unsafe then Unsafe
            else Safe
        | SynExpr.DotSet(e1,_,e2,_) ->
            if analyzeExp e1 = Unsafe then Unsafe
            elif analyzeExp e2 = Unsafe then Unsafe
            else Safe
        | SynExpr.Downcast(e,_,_) -> analyzeExp e
        | SynExpr.For(_,_,e1,_,e2,e3,_) -> 
            if analyzeExp e1 = Unsafe then Unsafe
            elif analyzeExp e2 = Unsafe then Unsafe
            elif analyzeExp e3 = Unsafe then Unsafe
            else Safe
        | SynExpr.ForEach(_,_,_,_,e1,e2,_) ->
            if analyzeExp e1 = Unsafe then Unsafe
            elif analyzeExp e2 = Unsafe then Unsafe
            else Safe
        | SynExpr.FromParseError(e,_) -> analyzeExp e
        | SynExpr.Ident(_) -> Safe
        | SynExpr.IfThenElse(e1,e2,e3,_,_,_,_) -> 
            if analyzeExp e1 = Unsafe then Unsafe
            elif analyzeExp e2 = Unsafe then Unsafe
            else 
                match e3 with
                | None -> Safe
                | Some e -> 
                    if analyzeExp e = Unsafe 
                    then Unsafe
                    else Safe
        | SynExpr.ImplicitZero(_) -> Safe
        | SynExpr.InferredDowncast(e,_) -> analyzeExp e
        | SynExpr.InferredUpcast(e,_) -> analyzeExp e
        | SynExpr.JoinIn(e1,_,e2,_) -> 
            if analyzeExp e1 = Unsafe then Unsafe
            elif analyzeExp e2 = Unsafe then Unsafe
            else Safe
        | SynExpr.Lambda(_,_,_,e,_) -> analyzeExp e
        | SynExpr.Lazy(e,_) -> analyzeExp e
        | SynExpr.LetOrUse(_,_,l,e,_) -> 
            let unsafeBindings = 
                [ for b in l ->
                    match b with
                    | SynBinding.Binding(_,_,_,_,_,_,_,_,_,e,_,_) -> analyzeExp e ]
                |> List.filter (fun x -> x = Unsafe)
                |> List.length
            if unsafeBindings > 0 
            then Unsafe     
            else analyzeExp e
        | SynExpr.LetOrUseBang(_,_,_,_,e1,e2,_) -> 
            if analyzeExp e1 = Unsafe then Unsafe
            elif analyzeExp e2 = Unsafe then Unsafe
            else Safe
        | SynExpr.LongIdent(_,x,_,_) ->         
            if (isUnsafe x) 
            then Unsafe
            else Safe // TODO check
        | SynExpr.LongIdentSet(x,e,_) -> 
            if (isUnsafe x) 
            then Unsafe
            else analyzeExp e
        | SynExpr.Match(_,e,_,_,_) -> analyzeExp e
        | SynExpr.MatchLambda(_,_,_,_,_) -> Safe
        | SynExpr.NamedIndexedPropertySet(_,e1,e2,_) -> 
            if analyzeExp e1 = Unsafe then Unsafe
            elif analyzeExp e2 = Unsafe then Unsafe
            else Safe
        | SynExpr.New(_,_,e,_) -> analyzeExp e
        | SynExpr.Null(_) -> Safe
        | SynExpr.ObjExpr(_,e,_,_,_,_) -> 
            match e with
            | None -> Safe
            | Some(e,_) -> analyzeExp e
        | SynExpr.Paren(e,_,_,_) -> analyzeExp e
        | SynExpr.Quote(e1,_,e2,_,_) -> 
            if analyzeExp e1 = Unsafe then Unsafe
            elif analyzeExp e2 = Unsafe then Unsafe
            else Safe
        | SynExpr.Record(a,b,c,_) -> 
            let es = [
                match a with 
                | None -> ignore ()
                | Some(_,e1,_,_,_) -> 
                    yield e1
                match b with
                | None -> ignore ()
                | Some(e2,_) -> yield e2
                yield! (c |> Seq.map (fun (_,e,_) -> e) |> Seq.choose id) ]
            if (es |> Seq.exists (fun e -> analyzeExp e = Unsafe))
            then Unsafe
            else Safe
        | SynExpr.Sequential(_,_,e1,e2,_) -> 
            if analyzeExp e1 = Unsafe then Unsafe
            elif analyzeExp e2 = Unsafe then Unsafe
            else Safe
        | SynExpr.TraitCall(_,_,e,_) -> analyzeExp e
        | SynExpr.TryFinally(e1,e2,_,_,_) -> 
            if analyzeExp e1 = Unsafe then Unsafe
            elif analyzeExp e2 = Unsafe then Unsafe
            else Safe
        | SynExpr.TryWith(e,_,_,_,_,_,_) -> analyzeExp e
        | SynExpr.Tuple(es,_,_) -> 
            if (es |> Seq.exists (fun e -> analyzeExp e = Unsafe))
            then Unsafe
            else Safe
        | SynExpr.TypeApp(e,_,_,_,_,_,_) -> analyzeExp e
        | SynExpr.TypeTest(e,_,_) -> analyzeExp e
        | SynExpr.Typed(e,_,_) -> analyzeExp e
        | SynExpr.Upcast(e,_,_) -> analyzeExp e
        | SynExpr.While(_,e1,e2,_) -> 
            if analyzeExp e1 = Unsafe then Unsafe
            elif analyzeExp e2 = Unsafe then Unsafe
            else Safe
        | SynExpr.YieldOrReturn(_,e,_) -> analyzeExp e
        | SynExpr.YieldOrReturnFrom(_,e,_) -> analyzeExp e
        | SynExpr.LibraryOnlyILAssembly _ -> Unsafe
        | SynExpr.LibraryOnlyStaticOptimization _ -> Unsafe
        | SynExpr.LibraryOnlyUnionCaseFieldGet _ -> Unsafe
        | SynExpr.LibraryOnlyUnionCaseFieldSet _ -> Unsafe

    let security tree = 
        match tree with
        | ParsedInput.ImplFile(implFile) -> 
            [   let (ParsedImplFileInput(fn, script, name, _, _, modules, _)) = implFile
                for foo in modules do
                    let (SynModuleOrNamespace(lid, isMod, decls, _, attrs, _, _)) = foo
                    for decl in decls do
                        match decl with
                        | SynModuleDecl.DoExpr(_,exp,_) -> 
                            yield analyzeExp exp
                        | _ -> yield Unsafe ]
        | _ -> [ Unsafe ]
        
    let analyze (fsiSession:FsiEvaluationSession) (code:string) =
        let (pfr,_,_) = code |> fsiSession.ParseAndCheckInteraction
//        if pfr.ParseHadErrors
//        then EvaluationFailure
        let analysis = security (pfr.ParseTree.Value)
        if (analysis |> List.exists (fun r -> r = Unsafe))
        then Some(UnsafeCode)
        else None