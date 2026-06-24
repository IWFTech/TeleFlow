param(
    [string] $Configuration = "Release",
    [string] $RequiredSdkVersionPrefix = ""
)

$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Split-Path -Parent $scriptDirectory
$testProject = Join-Path $repositoryRoot "tests\TeleFlow.ArchitectureTests\TeleFlow.ArchitectureTests.csproj"
$testFilter = "FullyQualifiedName~TeleFlow.ArchitectureTests.PackageSmokeTests.ReleaseAlignedToolingPackages_LoadAsAnalyzersFromPackageReference"

function Invoke-CheckedDotNet {
    param([string[]] $Arguments)

    Write-Host "dotnet $($Arguments -join ' ')"
    & dotnet @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Set-Location $repositoryRoot

$sdkVersion = (& dotnet --version).Trim()
Write-Host "Using .NET SDK $sdkVersion"

if (-not [string]::IsNullOrWhiteSpace($RequiredSdkVersionPrefix) -and
    -not $sdkVersion.StartsWith($RequiredSdkVersionPrefix, [System.StringComparison]::Ordinal)) {
    throw "Expected .NET SDK version to start with '$RequiredSdkVersionPrefix', but current SDK is '$sdkVersion'."
}

Invoke-CheckedDotNet @(
    "test",
    $testProject,
    "-c",
    $Configuration,
    "--filter",
    $testFilter,
    "/nodeReuse:false",
    "--logger",
    "console;verbosity=minimal"
)
