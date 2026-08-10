# Changelog

## [0.0.3] - 2026-08-10

### Added

- Add Native AOT smoke tests that read a checked-in binlog on Linux, Windows, and macOS.

### Changed

- Publish the harness as Native AOT tool packages for Linux, Windows, and macOS on x64 and ARM64.
- Publish a RID-less tool package that selects the correct native package for the current platform.
- Build the Native AOT packages with the same workflow for pull requests and releases.
- Exclude native debug symbols from the Native AOT packages.
- Restore all packages from NuGet.org through package source mapping.
- Manage direct and transitive dependency versions in `Directory.Packages.props`.
- Update System.CommandLine and the Microsoft.Build dependencies.

### Fixed

- Preserve complete binlog data in Native AOT packages.

### Removed

- Stop publishing `msbuild.otel` as a separate NuGet package.

## [0.0.2] - 2026-08-07

### Added

- Add a GitHub Actions workflow that publishes tagged releases to NuGet and GitHub.

### Changed

- Target .NET 10.
- Manage direct dependency versions in `Directory.Packages.props`.
- Update MSBuild Structured Logger and OpenTelemetry dependencies.

### Fixed

- Support current MSBuild binary log formats.
- Preserve binary log parsing errors and handle logs without evaluation or execution phases.

### Removed

- Remove the deprecated Zipkin exporter.

## [0.0.1] - 2022-01-30

### Added

- Initial exporter with support for console, zipkin, and OLTP trace exports.
