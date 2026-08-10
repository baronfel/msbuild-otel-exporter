[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $PackagePath,

    [Parameter(Mandatory)]
    [ValidateSet('linux-x64', 'linux-arm64', 'win-x64', 'win-arm64', 'osx-x64', 'osx-arm64')]
    [string] $RuntimeIdentifier,

    [string] $BinlogPath = (Join-Path $PSScriptRoot 'fixture.binlog')
)

$ErrorActionPreference = 'Stop'

$os = if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Windows)) {
    'win'
}
elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Linux)) {
    'linux'
}
elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::OSX)) {
    'osx'
}
else {
    throw 'Unsupported operating system.'
}

$architecture = switch ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) {
    ([System.Runtime.InteropServices.Architecture]::X64) { 'x64' }
    ([System.Runtime.InteropServices.Architecture]::Arm64) { 'arm64' }
    default { throw "Unsupported host architecture: $_" }
}

$hostRuntimeIdentifier = "$os-$architecture"
if ($RuntimeIdentifier -ne $hostRuntimeIdentifier) {
    throw "Cannot run $RuntimeIdentifier on the $hostRuntimeIdentifier host."
}

$package = (Resolve-Path $PackagePath).Path
$binlog = (Resolve-Path $BinlogPath).Path
$extractPath = Join-Path ([System.IO.Path]::GetTempPath()) "msbuild-otel-aot-$([guid]::NewGuid())"

try {
    [System.IO.Compression.ZipFile]::ExtractToDirectory($package, $extractPath)

    $executableName = if ($os -eq 'win') {
        'msbuild.otel.harness.exe'
    }
    else {
        'msbuild.otel.harness'
    }

    $executable = Join-Path $extractPath "tools/any/$RuntimeIdentifier/$executableName"
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "Native executable was not found in package: $executable"
    }

    if ($os -ne 'win') {
        & chmod +x $executable
        if ($LASTEXITCODE -ne 0) {
            throw "chmod failed with exit code $LASTEXITCODE."
        }
    }

    $output = (& $executable $binlog --console 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) {
        throw "Native AOT harness failed with exit code $LASTEXITCODE.`n$output"
    }

    $spanNames = [regex]::Matches($output, '(?m)^Activity\.DisplayName:\s*(.+?)\s*$') |
        ForEach-Object { $_.Groups[1].Value }

    $requiredSpanNames = @('AotSmoke', 'Message: Native AOT binlog ingestion fixture')
    $missingSpanNames = $requiredSpanNames | Where-Object { $_ -notin $spanNames }
    if ($missingSpanNames.Count -gt 0) {
        throw "Native AOT ingestion emitted $($spanNames.Count) span(s), but was missing: $($missingSpanNames -join ', ').`nEmitted spans: $($spanNames -join ', ')`n$output"
    }

    if ($spanNames.Count -le 1) {
        throw "Native AOT ingestion emitted only $($spanNames.Count) span(s)."
    }

    Write-Host "Native AOT ingestion emitted $($spanNames.Count) spans, including the fixture target and task."
}
finally {
    if (Test-Path -LiteralPath $extractPath) {
        Remove-Item -LiteralPath $extractPath -Recurse -Force
    }
}
