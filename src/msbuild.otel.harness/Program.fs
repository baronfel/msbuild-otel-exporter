
open msbuild.otel

open OpenTelemetry
open OpenTelemetry.Exporter
open OpenTelemetry.Resources
open OpenTelemetry.Trace
open System.CommandLine
open System.CommandLine.Binding
open System.CommandLine.Builder
open System.CommandLine.Parsing
open System.IO

let file = System.IO.Path.Combine (__SOURCE_DIRECTORY__,  "msbuild.binlog")


let root = RootCommand("Translates MSBuild structured log files to OpenTelemetry spans.")

let fileArgument = Argument<FileInfo>("logFile", "The MSBuild structured log file to parse")
fileArgument.Arity <- ArgumentArity.ExactlyOne
root.AddArgument fileArgument

let serviceNameOption = Option<string>("--serviceName", description = "The OpenTelemetry service name to use for the spans.", getDefaultValue = fun () -> "msbuild")
root.AddOption serviceNameOption

let version = 
    string (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version)

let upload (filePath: FileInfo) (tracer: Tracer) = 
    Ingest.read filePath.FullName
    |> Ingest.analyze
    |> Ingest.upload tracer


let binder f = 
    { new BinderBase<'t>() with 
        override _.GetBoundValue ctx = 
            f ctx }

let makeTracer (ctx: BindingContext) =
    let serviceName = ctx.ParseResult.GetValueForOption serviceNameOption

    Sdk.CreateTracerProviderBuilder()
        .AddSource("msbuild")
        .SetSampler(AlwaysOnSampler()) // want to send every span
        .SetResourceBuilder(
            ResourceBuilder.CreateDefault()
                .AddService(serviceName = serviceName, serviceVersion = version))
        // todo: remove this
        .AddConsoleExporter()
        .AddZipkinExporter(fun o -> 
            o.Endpoint <- System.Uri "http://localhost:9411/api/v2/spans"
            o.ExportProcessorType <- ExportProcessorType.Batch
            o.BatchExportProcessorOptions <- BatchExportProcessorOptions()
        )
        .Build()
        .GetTracer(serviceName, version)

root.SetHandler(upload, fileArgument, binder makeTracer)

[<EntryPoint>]
let main argv =
    CommandLineBuilder(root)
        .UseParseErrorReporting(1)
        .UseSuggestDirective()
        .CancelOnProcessTermination()
        .RegisterWithDotnetSuggest()
        .UseHelp()
        .Build()
        .Parse(argv)
        .Invoke()