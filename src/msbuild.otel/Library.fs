namespace msbuild.otel

open Microsoft.Build.Logging.StructuredLogger
open OpenTelemetry
open OpenTelemetry.Trace
open System
open System.Collections.Generic

module Ingest =
    open System.Diagnostics
    
    let read filePath =
        try
            Serialization.Read filePath
        with
        | ex ->
            let b = Build(Succeeded = false)
            b.AddChild(Error(Text = "Error when openening file: " + filePath))
            b.AddChild(Error(Text = ex.Message))
            b

    let analyze (b: Build) =
        BuildAnalyzer.AnalyzeBuild b
        b

    type NodeType = | Target | Import | NoImport | Task

    let inline startTime x = 
        (^a: (member StartTime: System.DateTime) (x))

    let inline endTime x = 
        (^a: (member EndTime: System.DateTime) (x))

    let inline column x = 
        (^a: (member Column: System.Int32) (x))

    let inline addTimeBounds x (a: Activity) = 
        a.SetStartTime((startTime x).ToUniversalTime())
         .SetEndTime((endTime x).ToUniversalTime())

    let start (a: Activity) = a.Start()
    
    let inline tag name (value: obj) (a: Activity) = a.AddTag(name, value)

    let rawFilePath p = 
        tag "code.filepath" p

    let sourceFilePath (x: #IHasSourceFile) =
        rawFilePath x.SourceFilePath
    
    let lineNumber (x: #IHasLineNumber) =
        tag "code.lineno" x.LineNumber

    let inline columnNumber x a = 
        tag "code.colno" (column x) a
    
    let setType (x: NodeType)= 
        tag "msbuild.type" (string x)

    let parseCondition (s: string) = 
        if s.StartsWith "false condition; (" then
            let parensedCondition = s.Substring("false condition; (".Length)
            match parensedCondition.IndexOf " was evaluated as " with
            | -1 -> 
                parensedCondition, None
            | n ->
                let condition = parensedCondition.Substring(0, n)
                let evaluated = 
                    let startPos = n + " was evaluated as ".Length
                    parensedCondition.Substring(startPos, parensedCondition.Length - startPos - 2)
                condition, Some evaluated
        else
            s, None

    let private MyActivitySource = new ActivitySource("msbuild")

    let private activityForBuild (t:Tracer) (b: Build) =
        let globalTags =
            Map [
                "code.filepath", b.LogFilePath
            ] : Map<string,obj>
        let a =
            MyActivitySource.CreateActivity("build", ActivityKind.Producer, ActivityContext(), tags = globalTags)
            |> addTimeBounds b
            |> start
        a.SetStatus(if b.Succeeded then Status.Ok else Status.Error)
        Activity.Current <- a
        a
    
    let rec private logImport e (i: Import) = 
        use iActivity = MyActivitySource.CreateActivity($"Importing {i.ImportedProjectFilePath}", ActivityKind.Producer) |> rawFilePath i.ImportedProjectFilePath |> lineNumber i |> columnNumber i |> addTimeBounds e |> setType Import |> start
        if i.HasChildren then
            logChildImports e i.Children
        ()  

    and private logNoImport e (i: NoImport) = 
        use iActivity = MyActivitySource.CreateActivity($"Skip importing {i.Name}", ActivityKind.Producer) |> sourceFilePath i |> lineNumber i |> columnNumber i|> addTimeBounds e |> setType NoImport|> start

        let condition, evaluated = parseCondition i.Text
        iActivity.AddTag("noimport.condition", condition) |> ignore
        evaluated |> Option.iter (fun e -> iActivity.AddTag("noimport.condition.evaluation", e) |> ignore)
        if i.HasChildren then 
            logChildImports e i.Children
        ()

    and private logChildImports e (nodes: IList<BaseNode>) = 
        for import in nodes do
            match import with
            | :? Import as i -> logImport e i
            | :? NoImport as ni -> logNoImport e ni
            | _ -> printfn $"unknown node type {import.TypeName}"

    let private logEvaluation (e: ProjectEvaluation) = 
        use eActivity = MyActivitySource.CreateActivity($"Evaluate {e.Name}", ActivityKind.Producer) |> addTimeBounds e |> sourceFilePath e |> start
        if e.ImportsFolder.HasChildren then
            logChildImports e e.ImportsFolder.Children

    [<Struct>]
    type TimeBounds =
        {
            StartTime: System.DateTime
            EndTime: System.DateTime
        }

    let private logEvaluations (e: NamedNode) =
        let evaluations = e.Children |> Seq.choose (function | :? ProjectEvaluation as p -> Some p | _ -> None)
        let firstEval = evaluations |> Seq.minBy (fun eval -> eval.StartTime)
        let lastEval = evaluations |> Seq.maxBy (fun eval -> eval.EndTime)
        let bounds = {StartTime = firstEval.StartTime; EndTime = lastEval.EndTime}
        use evalActivity = MyActivitySource.CreateActivity("Evaluation", ActivityKind.Producer) |> addTimeBounds bounds |> start
        for evaluation in evaluations do 
            logEvaluation evaluation
        ()

    type Project with 
        member x.Targets = 
            x.Children
            |> Seq.choose (function | :? Target as t -> Some t | _ -> None)

    let parseSkippedTask (s: string) = 
        if s.StartsWith "Task \"" then
            let skipStartHeader = s.Substring("Task \"".Length)
            match skipStartHeader.IndexOf "\"" with
            | -1 -> 
                None
            | n -> 
                let taskName = skipStartHeader.Substring(0, n)
                let skipFalseAnnouncement = skipStartHeader.Substring((taskName + " skipped, due to false condition; ").Length + 1)
                match skipFalseAnnouncement.IndexOf " was evaluated as " with
                | -1 -> 
                    None
                | n -> 
                    let condition = skipFalseAnnouncement.Substring(0, n)
                    let evaluated = 
                        let startPos = n + " was evaluated as ".Length
                        skipFalseAnnouncement.Substring(startPos, skipFalseAnnouncement.Length - startPos - 1)
                    Some (taskName, condition, evaluated)
        else 
            None

    type TargetTask = Task of Task | SkippedTask of name: string * condition: string * evaluated: string

    type Target with 
        member x.Tasks = 
            x.Children
            |> Seq.choose (function | :? Task as t -> Some (Task t)
                                    | :? Message as m -> 
                                        match parseSkippedTask m.Text with
                                        | Some (name, condition, eval) -> Some (SkippedTask(name, condition, eval))
                                        | _ -> None
                                    | _ -> None)


    let logTask (t: Target) (task: TargetTask) =
        use tActivity = 
            match task with
            | Task task ->
                MyActivitySource.CreateActivity($"{task.Name}", ActivityKind.Producer) |> addTimeBounds task |> sourceFilePath task |> lineNumber task |> setType NodeType.Task |> tag "task.assembly" task.FromAssembly |> start
            | SkippedTask (name, condition, eval) ->
                // the message task doesn't have a start time, so we use the start time of the target to prevent the display from going all wonky
                let fakeBounds = { StartTime = t.StartTime + TimeSpan.FromTicks 1; EndTime = t.StartTime + TimeSpan.FromTicks 2}
                MyActivitySource.CreateActivity($"{name}", ActivityKind.Producer) |> setType NodeType.Task |> tag "skiptask.condition" condition |> tag "skiptask.condition.evaluation" eval |> addTimeBounds fakeBounds |> start
        ()

    let logTarget (t: Target) = 
        use tActivity = MyActivitySource.CreateActivity($"{t.Name}", ActivityKind.Producer) |> addTimeBounds t |> sourceFilePath t |> setType Target |> start
        // todo: log inputs as tags?
        if not (Seq.isEmpty t.Tasks) then
            for task in t.Tasks do 
                logTask t task
        
    let logProject (p: Project) =        
        let targetName = 
            if String.IsNullOrEmpty p.TargetsText then 
                p.Targets
                |> Seq.filter (function t -> not (t.Name.StartsWith "_"))
                |> Seq.tryLast
                |> Option.map (fun t -> t.Name)
                |> Option.defaultValue "<Unknown>"
            else
                p.TargetsText
        let name = $"Execute {targetName} for {p.SourceFilePath}"

        use projectActivity = MyActivitySource.CreateActivity(name, ActivityKind.Producer) |> addTimeBounds p |> sourceFilePath p |> start
        // todo: log items as tags?
        // todo: log properties as tags?
        if not (Seq.isEmpty p.Targets) then
            for target in p.Targets do 
                logTarget target

    let private logExecution (p: Project seq) =
        let firstExec = p |> Seq.minBy (fun p -> p.StartTime)
        let lastExec = p |> Seq.maxBy (fun p -> p.EndTime)
        let bounds = { StartTime = firstExec.StartTime; EndTime = lastExec.EndTime }
        use execActivity = MyActivitySource.CreateActivity("Execution", ActivityKind.Producer) |> addTimeBounds bounds |> start
        for project in p do 
            logProject project
        ()


    let upload (t: Tracer) (b: Build) =
        use rootActivity = activityForBuild t b

        logEvaluations b.EvaluationFolder
        logExecution (b.Children |> Seq.choose (function | :? Project as p ->  Some p | _ -> None))
        // traverse the build, reporting things
        ()

