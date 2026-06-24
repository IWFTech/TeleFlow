param(
    [string] $Configuration = "Release",
    [string] $RuntimeIdentifier = "win-x64",
    [string] $PackageVersion = "",
    [string] $PackageSource = "",
    [switch] $UseProjectReference
)

$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Split-Path -Parent $scriptDirectory
$tempDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "teleflow-client-aot-$([System.Guid]::NewGuid().ToString("N"))"

function Invoke-CheckedDotNet {
    param([string[]] $Arguments)

    Write-Host "dotnet $($Arguments -join ' ')"
    & dotnet @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Test-NativeAotToolchainFailure {
    param([string] $Output)

    return $Output.IndexOf("Platform linker not found", [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $Output.IndexOf("NativeAOT prerequisites", [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $Output.IndexOf("Desktop Development for C++", [System.StringComparison]::OrdinalIgnoreCase) -ge 0
}

function Invoke-CheckedDotNetWithAotDiagnostics {
    param([string[]] $Arguments)

    Write-Host "dotnet $($Arguments -join ' ')"

    $output = & dotnet @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $outputText = $output | Out-String

    if (-not [string]::IsNullOrWhiteSpace($outputText)) {
        Write-Host $outputText
    }

    if ($exitCode -ne 0) {
        $combinedOutput = $outputText

        if (Test-NativeAotToolchainFailure $combinedOutput) {
            throw "Client-only AOT smoke reached NativeAOT publishing but the platform toolchain is missing. Install the required NativeAOT prerequisites for '$RuntimeIdentifier' and rerun this command."
        }

        throw "dotnet $($Arguments -join ' ') failed with exit code $exitCode."
    }
}

function New-ProjectReferenceItemGroup {
    $projectPath = Join-Path $repositoryRoot "src\TeleFlow.Telegram\TeleFlow.Telegram.csproj"
    return @"
  <ItemGroup>
    <ProjectReference Include="$projectPath" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
  </ItemGroup>
"@
}

function New-PackageReferenceItemGroup {
    if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
        throw "PackageVersion is required unless UseProjectReference is set."
    }

    if ([string]::IsNullOrWhiteSpace($PackageSource)) {
        throw "PackageSource is required unless UseProjectReference is set."
    }

    return @"
  <ItemGroup>
    <PackageReference Include="TeleFlow.Telegram" Version="$PackageVersion" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
  </ItemGroup>
"@
}

try {
    New-Item -ItemType Directory -Path $tempDirectory | Out-Null

    $itemGroup = if ($UseProjectReference) {
        New-ProjectReferenceItemGroup
    }
    else {
        New-PackageReferenceItemGroup
    }

    $projectFile = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PublishAot>true</PublishAot>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>$RuntimeIdentifier</RuntimeIdentifier>
  </PropertyGroup>

$itemGroup
</Project>
"@

    Set-Content -LiteralPath (Join-Path $tempDirectory "TeleFlow.ClientAotSmoke.csproj") -Value $projectFile -Encoding UTF8

    $programFile = @'
using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Telegram;

var services = new ServiceCollection();
services.AddTelegramClient(options =>
{
    options.Token = "token";
    options.Defaults.ParseMode = TelegramParseMode.Html;
});

using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<ITelegramClient>();

Console.WriteLine($"{client.Defaults.ParseMode?.Value}:{TelegramUpdateType.Message.Value}");
'@

    Set-Content -LiteralPath (Join-Path $tempDirectory "Program.cs") -Value $programFile -Encoding UTF8

    $publishArguments = @(
        "publish",
        $tempDirectory,
        "-c",
        $Configuration,
        "-r",
        $RuntimeIdentifier,
        "-v:minimal",
        "/warnaserror:IL2026",
        "/warnaserror:IL3050",
        "/warnaserror:IL3053",
        "/warnaserror:IL3054",
        "/p:RestoreNoCache=true",
        "/nodeReuse:false"
    )

    if (-not $UseProjectReference) {
        $publishArguments += @("--source", $PackageSource, "--source", "https://api.nuget.org/v3/index.json")
    }

    Invoke-CheckedDotNetWithAotDiagnostics $publishArguments

    Write-Host ""
    Write-Host "Client-only AOT smoke completed."
}
catch {
    Write-Host ""
    Write-Host "Client-only AOT smoke failed."
    throw
}
finally {
    Remove-Item -LiteralPath $tempDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
