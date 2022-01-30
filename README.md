# MSBuild Structured Log exporter for OpenTelemetry

## Table of Contents

- [About](#about)
- [Getting Started](#getting_started)
- [Usage](#usage)
- [Contributing](../CONTRIBUTING.md)

## About <a name = "about"></a>

This project provides an library for parsing MSBuild Structured logs, as well as a dotnet tool for performing that same task.

## Getting Started <a name = "getting_started"></a>

### Prerequisites

You'll need the dotnet SDK version 6.0 or greater installed to run the dotnet tool.

### Installing

Install the tool with `dotnet tool install msbuild.otel.harness` (NAME TBD)

## Usage <a name = "usage"></a>

See the `--help` output for details, but the gist is that you can specify any of 3 exporters:

* `--console`, to emit spans to stdout
* `--zipkin-endpoint <endpoint>`, to emit spans to a zipkin instance, or
* `--oltp-endpoint <endpoint>`, to emit spans to an OLTP-compatible instance


Full Help:

```
Description:
  Translates MSBuild structured log files to OpenTelemetry spans.

Usage:
  msbuild.otel.harness <logFile> [options]

Arguments:
  <logFile>  The MSBuild structured log file to parse

Options:
  --serviceName <serviceName>          The OpenTelemetry service name to use for the spans. [default: msbuild]
  --console                            Log the emitted spans to the console
  --oltp-endpoint <oltp-endpoint>      The OpenTelemetry endpoint to use for the spans.
  --zipkin-endpoint <zipkin-endpoint>  The Zipkin endpoint to use for the spans.
  -?, -h, --help                       Show help and usage information
```