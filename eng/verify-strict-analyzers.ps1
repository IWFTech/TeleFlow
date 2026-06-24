param(
    [string] $Configuration = "Release",
    [switch] $NoRestore
)

$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Split-Path -Parent $scriptDirectory

$solutionPath = Join-Path $repositoryRoot "TeleFlow.sln"

function Invoke-CheckedDotNet {
    param([string[]] $Arguments)

    Write-Host "dotnet $($Arguments -join ' ')"
    & dotnet @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Invoke-Step {
    param(
        [string] $Name,
        [scriptblock] $Command
    )

    Write-Host ""
    Write-Host "==> $Name"
    & $Command
}

Set-Location $repositoryRoot

if (-not $NoRestore) {
    Invoke-Step "Restore solution" {
        Invoke-CheckedDotNet @("restore", $solutionPath)
    }
}

Invoke-Step "Build solution with strict analyzers" {
    Invoke-CheckedDotNet @(
        "build",
        $solutionPath,
        "-c",
        $Configuration,
        "--no-restore",
        "/nodeReuse:false",
        "-p:AnalysisMode=AllEnabledByDefault"
    )
}

Write-Host ""
Write-Host "Strict analyzer verification completed."
