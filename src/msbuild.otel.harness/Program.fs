
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

let consoleOption = Option<bool>("--console", "Log the emitted spans to the console")
consoleOption.Arity <- ArgumentArity.Zero
root.AddOption consoleOption

let oltpEndpointOption = Option<string>("--oltp-endpoint", description = "The OpenTelemetry endpoint to use for the spans.")
oltpEndpointOption.Arity <- ArgumentArity.ExactlyOne
root.AddOption oltpEndpointOption

let zipkinEndpointOption = Option<string>("--zipkin-endpoint", description = "The Zipkin endpoint to use for the spans.")
zipkinEndpointOption.Arity <- ArgumentArity.ExactlyOne
root.AddOption zipkinEndpointOption

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
    let useConsole = ctx.ParseResult.GetValueForOption consoleOption
    let useZipkin = ctx.ParseResult.HasOption zipkinEndpointOption
    let useOltp = ctx.ParseResult.HasOption oltpEndpointOption

    let mutable builder = 
        Sdk.CreateTracerProviderBuilder()
            .AddSource("msbuild")
            .SetSampler(AlwaysOnSampler()) // want to send every span
            .SetResourceBuilder(
                ResourceBuilder.CreateDefault()
                    .AddService(serviceName = serviceName, serviceVersion = version))
    if useConsole then 
        builder <- builder.AddConsoleExporter()
    if useZipkin then 
        builder <- builder.AddZipkinExporter(fun o -> 
            o.Endpoint <- System.Uri (ctx.ParseResult.GetValueForOption zipkinEndpointOption)
            o.ExportProcessorType <- ExportProcessorType.Batch
        )
    if useOltp then 
        builder <- builder.AddOtlpExporter(fun o -> 
            o.Endpoint <- System.Uri (ctx.ParseResult.GetValueForOption oltpEndpointOption)
        )
    builder
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