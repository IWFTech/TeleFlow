param(
    [string] $Configuration = "Release",
    [string] $PackageVersion = "0.0.0-local",
    [string] $OutputDirectory = ""
)

$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Split-Path -Parent $scriptDirectory

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot "artifacts\release-packages\$PackageVersion"
}

$solutionPath = Join-Path $repositoryRoot "TeleFlow.sln"
$strictAnalyzerScriptPath = Join-Path $repositoryRoot "eng\verify-strict-analyzers.ps1"

$runtimePackages = @(
    @{ Id = "TeleFlow.Annotations"; Project = "src\TeleFlow.Annotations\TeleFlow.Annotations.csproj" },
    @{ Id = "TeleFlow.Core"; Project = "src\TeleFlow.Core\TeleFlow.Core.csproj" },
    @{ Id = "TeleFlow.Storage.Memory"; Project = "src\TeleFlow.Storage.Memory\TeleFlow.Storage.Memory.csproj" },
    @{ Id = "TeleFlow.Telegram.Schema"; Project = "src\TeleFlow.Telegram.Schema\TeleFlow.Telegram.Schema.csproj" },
    @{ Id = "TeleFlow.Telegram.Client"; Project = "src\TeleFlow.Telegram.Client\TeleFlow.Telegram.Client.csproj" },
    @{ Id = "TeleFlow.Telegram.Framework"; Project = "src\TeleFlow.Telegram.Framework\TeleFlow.Telegram.Framework.csproj" },
    @{ Id = "TeleFlow.Telegram.LongPolling"; Project = "src\TeleFlow.Telegram.LongPolling\TeleFlow.Telegram.LongPolling.csproj" },
    @{ Id = "TeleFlow.Telegram.Webhooks"; Project = "src\TeleFlow.Telegram.Webhooks\TeleFlow.Telegram.Webhooks.csproj" },
    @{ Id = "TeleFlow.Telegram.Framework.LongPolling"; Project = "src\TeleFlow.Telegram.Framework.LongPolling\TeleFlow.Telegram.Framework.LongPolling.csproj" },
    @{ Id = "TeleFlow.Telegram.Framework.Webhooks"; Project = "src\TeleFlow.Telegram.Framework.Webhooks\TeleFlow.Telegram.Framework.Webhooks.csproj" },
    @{ Id = "TeleFlow.Telegram"; Project = "src\TeleFlow.Telegram\TeleFlow.Telegram.csproj" }
)

$releaseAlignedToolingPackages = @(
    @{
        Id = "TeleFlow.Generators"
        Project = "src\TeleFlow.Generators\TeleFlow.Generators.csproj"
        AnalyzerPath = "analyzers/dotnet/cs/TeleFlow.Generators.dll"
        ProhibitLib = $true
    }
)

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Xml.Linq

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

function Read-PackageMetadata {
    param([string] $PackagePath)

    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)

    try {
        $nuspecEntries = @($archive.Entries | Where-Object { $_.FullName.EndsWith(".nuspec", [System.StringComparison]::Ordinal) })

        if ($nuspecEntries.Count -ne 1) {
            throw "Expected exactly one nuspec entry in $PackagePath, found $($nuspecEntries.Count)."
        }

        $stream = $nuspecEntries[0].Open()

        try {
            $document = [System.Xml.Linq.XDocument]::Load($stream)
        }
        finally {
            $stream.Dispose()
        }

        $metadataElements = @($document.Descendants() | Where-Object { $_.Name.LocalName -eq "metadata" })

        if ($metadataElements.Count -ne 1) {
            throw "Expected exactly one metadata element in $PackagePath, found $($metadataElements.Count)."
        }

        $entryNames = @($archive.Entries | Select-Object -ExpandProperty FullName)

        return @{
            Id = Read-PackageMetadataValue $metadataElements[0] "id"
            Version = Read-PackageMetadataValue $metadataElements[0] "version"
            Description = Read-PackageMetadataValue $metadataElements[0] "description"
            Authors = Read-PackageMetadataValue $metadataElements[0] "authors"
            Tags = Read-PackageMetadataValue $metadataElements[0] "tags"
            Readme = Read-PackageMetadataValue $metadataElements[0] "readme"
            EntryNames = $entryNames
            HasReadmeFile = @($entryNames | Where-Object { $_ -eq "README.md" }).Count -eq 1
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Read-PackageMetadataValue {
    param(
        [System.Xml.Linq.XElement] $Metadata,
        [string] $Name
    )

    $elements = @($Metadata.Elements() | Where-Object { $_.Name.LocalName -eq $Name })

    if ($elements.Count -ne 1) {
        throw "Expected exactly one '$Name' metadata element, found $($elements.Count)."
    }

    return [string] $elements[0].Value
}

function Assert-ExpectedPackageArchive {
    param(
        [hashtable] $Package,
        [string] $ExpectedVersion
    )

    $packagePath = Join-Path $OutputDirectory "$($Package.Id).$ExpectedVersion.nupkg"

    if (-not (Test-Path -LiteralPath $packagePath)) {
        throw "Expected package was not produced: $packagePath"
    }

    $metadata = Read-PackageMetadata $packagePath

    if ($metadata.Id -ne $Package.Id) {
        throw "Package id mismatch for $packagePath. Expected '$($Package.Id)', got '$($metadata.Id)'."
    }

    if ($metadata.Version -ne $ExpectedVersion) {
        throw "Package version mismatch for $packagePath. Expected '$ExpectedVersion', got '$($metadata.Version)'."
    }

    if ($metadata.Authors -ne "TeleFlow") {
        throw "Package authors mismatch for $packagePath. Expected 'TeleFlow', got '$($metadata.Authors)'."
    }

    if ([string]::IsNullOrWhiteSpace($metadata.Description)) {
        throw "Package description is required for $packagePath."
    }

    if ($metadata.Tags -notmatch "(^|[;,\s])teleflow([;,\s]|$)") {
        throw "Package tags must include 'teleflow' for $packagePath. Current tags: '$($metadata.Tags)'."
    }

    if ($metadata.Readme -ne "README.md") {
        throw "Package readme metadata mismatch for $packagePath. Expected 'README.md', got '$($metadata.Readme)'."
    }

    if (-not $metadata.HasReadmeFile) {
        throw "Package README.md entry is required for $packagePath."
    }

    if ($Package.ContainsKey("AnalyzerPath") -and $metadata.EntryNames -notcontains $Package["AnalyzerPath"]) {
        throw "Package analyzer entry '$($Package["AnalyzerPath"])' is required for $packagePath."
    }

    if ($Package.ContainsKey("ProhibitLib") -and $Package["ProhibitLib"]) {
        $libEntries = @($metadata.EntryNames | Where-Object { $_.StartsWith("lib/", [System.StringComparison]::OrdinalIgnoreCase) })

        if ($libEntries.Count -gt 0) {
            throw "Tooling package must not expose runtime lib assets: $packagePath."
        }
    }
}

function Assert-NoUnexpectedReleasePackages {
    param(
        [hashtable[]] $ExpectedPackages,
        [string] $ExpectedVersion
    )

    $expectedIds = @{}
    $seenIds = @{}

    foreach ($package in $ExpectedPackages) {
        $expectedIds[$package.Id] = $true
    }

    $packageFiles = @(Get-ChildItem -LiteralPath $OutputDirectory -Filter "*.nupkg" -File)

    foreach ($packageFile in $packageFiles) {
        $metadata = Read-PackageMetadata $packageFile.FullName

        if (-not $expectedIds.ContainsKey($metadata.Id)) {
            throw "Unexpected package was found in the release output directory: $($packageFile.FullName)"
        }

        if ($metadata.Version -ne $ExpectedVersion) {
            throw "Package version mismatch in release output directory for $($packageFile.FullName). Expected '$ExpectedVersion', got '$($metadata.Version)'."
        }

        if ($seenIds.ContainsKey($metadata.Id)) {
            throw "Duplicate package id '$($metadata.Id)' was found in the release output directory."
        }

        $seenIds[$metadata.Id] = $true
    }
}

Set-Location $repositoryRoot

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

Invoke-Step "Restore solution" {
    Invoke-CheckedDotNet @("restore", $solutionPath)
}

Invoke-Step "Verify whitespace formatting" {
    Invoke-CheckedDotNet @("format", "whitespace", $solutionPath, "--verify-no-changes", "--no-restore", "--verbosity", "minimal")
}

Invoke-Step "Verify style formatting" {
    Invoke-CheckedDotNet @("format", "style", $solutionPath, "--verify-no-changes", "--no-restore", "--verbosity", "minimal")
}

Invoke-Step "Build solution" {
    Invoke-CheckedDotNet @("build", $solutionPath, "-c", $Configuration, "--no-restore", "/nodeReuse:false")
}

Invoke-Step "Verify strict analyzer baseline" {
    & $strictAnalyzerScriptPath -Configuration $Configuration -NoRestore
}

Invoke-Step "Test solution" {
    Invoke-CheckedDotNet @("test", $solutionPath, "-c", $Configuration, "--no-build", "--no-restore", "/nodeReuse:false", "--logger", "console;verbosity=minimal")
}

Invoke-Step "Pack release packages" {
    foreach ($package in $runtimePackages + $releaseAlignedToolingPackages) {
        $projectPath = Join-Path $repositoryRoot $package.Project

        Invoke-CheckedDotNet @(
            "pack",
            $projectPath,
            "-c",
            $Configuration,
            "--no-build",
            "--no-restore",
            "-o",
            $OutputDirectory,
            "/p:PackageVersion=$PackageVersion",
            "/nodeReuse:false"
        )
    }
}

Invoke-Step "Verify package outputs" {
    $expectedPackages = $runtimePackages + $releaseAlignedToolingPackages

    foreach ($package in $expectedPackages) {
        Assert-ExpectedPackageArchive $package $PackageVersion
    }

    Assert-NoUnexpectedReleasePackages $expectedPackages $PackageVersion
}

Write-Host ""
Write-Host "Release verification completed. Packages: $OutputDirectory"
