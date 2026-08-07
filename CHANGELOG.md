# Changelog

## [0.0.2] - 2026-08-07

### Added

- Add a GitHub Actions workflow that publishes tagged releases to NuGet and GitHub.

### Changed

- Publish the harness as Native AOT tool packages for Linux, Windows, and macOS on x64 and ARM64.
- Build all Native AOT tool packages for pull requests.
- Target .NET 10.
- Manage direct dependency versions in `Directory.Packages.props`.
- Update MSBuild Structured Logger and OpenTelemetry dependencies.

### Fixed

- Support current MSBuild binary log formats.
- Preserve binary log parsing errors and handle logs without evaluation or execution phases.

### Removed

- Stop publishing `msbuild.otel` as a separate NuGet package.
- Remove the deprecated Zipkin exporter.

## [0.0.1] - 2022-01-30

### Added

- Initial exporter with support for console, zipkin, and OLTP trace exports.
