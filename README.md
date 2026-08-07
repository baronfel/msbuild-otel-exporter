# MSBuild Structured Log exporter for OpenTelemetry

## Table of Contents

- [About](#about)
- [Getting Started](#getting-started)
- [Usage](#usage)
- [Contributing](../CONTRIBUTING.md)

## About

This project provides an library for parsing MSBuild Structured logs, as well as a dotnet tool for performing that same task.

## Getting Started

### Prerequisites

You'll need the .NET 10 SDK or greater installed to run the dotnet tool.

### Installing

Install the tool with `dotnet tool install msbuild.otel.harness` (NAME TBD)

## Usage

See the `--help` output for details. You can export spans to the console or an OTLP-compatible endpoint:

* `--console`, to emit spans to stdout
* `--oltp-endpoint <endpoint>`, to emit spans to an OTLP-compatible instance
* `--oltp-header <oltp=header>`, to emit additional headers to an OTLP-compatible instance (for example authentication headers)

Full Help:

```
Description:
  Translates MSBuild structured log files to OpenTelemetry spans.

Usage:
  msbuild.otel.harness <logFile> [options]

Arguments:
  <logFile>  The MSBuild structured log file to parse

Options:
  --serviceName <serviceName>      The OpenTelemetry service name to use for the spans. [default: msbuild]
  --console                        Log the emitted spans to the console
  --oltp-endpoint <oltp-endpoint>  The OpenTelemetry endpoint to use for the spans.
  --oltp-header <oltp-header>      Allows for adding arbitrary headers in a key=value format. Use this option multiple times for multiple header values.
  -?, -h, --help                   Show help and usage information
```

## Publishing

Configure a NuGet trusted publishing policy for `.github/workflows/release.yml`.
Add the NuGet account name to the `NUGET_USER` repository secret.

1. Move the released changes into a versioned and dated `CHANGELOG.md` section.
2. Push a matching tag that uses the `v<version>` format, such as `v1.2.3`.

The workflow publishes both NuGet packages and creates a GitHub release.
