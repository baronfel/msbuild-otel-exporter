
open msbuild.otel

open OpenTelemetry
open OpenTelemetry.Exporter
open OpenTelemetry.Resources
open OpenTelemetry.Trace
open System.CommandLine
open System.CommandLine.Parsing
open System.IO

let file = System.IO.Path.Combine (__SOURCE_DIRECTORY__,  "msbuild.binlog")


let root = RootCommand("Translates MSBuild structured log files to OpenTelemetry spans.")

let fileArgument = Argument<FileInfo>("logFile")
fileArgument.Description <- "The MSBuild structured log file to parse"
fileArgument.Arity <- ArgumentArity.ExactlyOne
root.Arguments.Add fileArgument

let serviceNameOption = Option<string>("--serviceName", [||])
serviceNameOption.Description <- "The OpenTelemetry service name to use for the spans."
serviceNameOption.DefaultValueFactory <- fun _ -> "msbuild"
root.Options.Add serviceNameOption

let consoleOption = Option<bool>("--console", [||])
consoleOption.Description <- "Log the emitted spans to the console"
consoleOption.Arity <- ArgumentArity.Zero
root.Options.Add consoleOption

let oltpEndpointOption = Option<string>("--oltp-endpoint", [||])
oltpEndpointOption.Description <- "The OpenTelemetry endpoint to use for the spans."
oltpEndpointOption.Arity <- ArgumentArity.ExactlyOne
root.Options.Add oltpEndpointOption

let oltpHeaderOption = Option<string[]>("--oltp-header", [||])
oltpHeaderOption.Description <- "Allows for adding arbitrary headers in a key=value format. Use this option multiple times for multiple header values."
oltpHeaderOption.Arity <- ArgumentArity.ZeroOrMore
oltpHeaderOption.AllowMultipleArgumentsPerToken <- false
root.Options.Add oltpHeaderOption


let version = 
    string (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version)

let upload (filePath: FileInfo) (serviceName: string) (tracerP: TracerProvider) = 
    if not filePath.Exists then failwithf $"File {filePath.FullName} does not exist"

    let tracer = tracerP.GetTracer(serviceName, version)
    Ingest.read filePath.FullName
    |> Ingest.analyze
    |> Ingest.upload tracer

    tracerP.ForceFlush() |> ignore

let makeTracer (parseResult: ParseResult) =
    let serviceName = parseResult.GetValue serviceNameOption
    let useConsole = parseResult.GetValue consoleOption
    let useOltp = not (isNull (parseResult.GetResult oltpEndpointOption))
    let oltpHeaders =
        match parseResult.GetValue oltpHeaderOption with
        | null -> [||]
        | headers -> headers

    let mutable builder = 
        Sdk.CreateTracerProviderBuilder()
            .AddSource("msbuild")
            .SetSampler(AlwaysOnSampler()) // want to send every span
            .SetResourceBuilder(
                ResourceBuilder.CreateDefault()
                    .AddService(serviceName = serviceName, serviceVersion = version))

    if useConsole then 
        builder <- builder.AddConsoleExporter()
    if useOltp then 
        builder <- builder.AddOtlpExporter(fun o -> 
            o.Endpoint <- System.Uri (parseResult.GetValue oltpEndpointOption)
            o.Headers <- String.concat "," oltpHeaders
        )

    builder.Build()

root.SetAction(fun parseResult ->
    let filePath = parseResult.GetRequiredValue fileArgument
    let serviceName = parseResult.GetValue serviceNameOption
    use tracerProvider = makeTracer parseResult
    upload filePath serviceName tracerProvider)

[<EntryPoint>]
let main argv =
    root.Parse(argv).Invoke()