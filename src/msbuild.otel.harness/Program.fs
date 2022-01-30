
open msbuild.otel
open OpenTelemetry
open OpenTelemetry.Exporter
open OpenTelemetry.Resources
open OpenTelemetry.Trace


let file = System.IO.Path.Combine (__SOURCE_DIRECTORY__,  "msbuild.binlog")

let tracerProvider = 
    Sdk.CreateTracerProviderBuilder()
        .AddSource("msbuild")
        .SetSampler(AlwaysOnSampler())
        .SetResourceBuilder(
            ResourceBuilder.CreateDefault()
                .AddService(serviceName = "msbuild", serviceVersion = ""))
        .AddConsoleExporter()
        .AddZipkinExporter(fun o -> 
            o.Endpoint <- System.Uri "http://localhost:9411/api/v2/spans"
            o.ExportProcessorType <- ExportProcessorType.Simple
        ).Build()

[<EntryPoint>]
let main argv =
    let b = Ingest.read file
    printfn "%A" b
    
    let t = tracerProvider.GetTracer("build-log")
    b
    |> Ingest.analyze
    |> Ingest.upload t
    0