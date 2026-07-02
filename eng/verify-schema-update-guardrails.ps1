param(
    [string] $BaseRef = "main",
    [string] $Remote = "origin",
    [switch] $SkipFetch
)

$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Split-Path -Parent $scriptDirectory

function Invoke-Git {
    param([string[]] $Arguments)

    & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git command failed with exit code ${LASTEXITCODE}: git $($Arguments -join ' ')"
    }
}

function Invoke-GitOutput {
    param([string[]] $Arguments)

    $output = & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git command failed with exit code ${LASTEXITCODE}: git $($Arguments -join ' ')"
    }

    return $output
}

function Test-PathMatches {
    param(
        [string] $Path,
        [string[]] $Patterns
    )

    foreach ($pattern in $Patterns) {
        if ($Path -match $pattern) {
            return $true
        }
    }

    return $false
}

function Get-RequiredProperty {
    param(
        [object] $Object,
        [string] $Name,
        [string] $Path
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        throw "$Path is missing required property '$Name'."
    }

    return $property.Value
}

function Get-RequiredStringProperty {
    param(
        [object] $Object,
        [string] $Name,
        [string] $Path
    )

    $value = [string] (Get-RequiredProperty $Object $Name $Path)
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "$Path property '$Name' is required."
    }

    return $value
}

function Get-RequiredIntProperty {
    param(
        [object] $Object,
        [string] $Name,
        [string] $Path
    )

    $value = Get-RequiredProperty $Object $Name $Path
    if ($null -eq $value) {
        throw "$Path property '$Name' is required."
    }

    return [int] $value
}

function Read-JsonFile {
    param([string] $Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required file was not found: $Path"
    }

    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Get-TrackedGeneratedFiles {
    $trackedFiles = @(Invoke-GitOutput @(
            "ls-files",
            "--",
            "src/TeleFlow.Telegram.Schema",
            "src/TeleFlow.Telegram.Client/Generated"
        ) |
        ForEach-Object { $_.Replace("\", "/", [System.StringComparison]::Ordinal).Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

    return @($trackedFiles | Where-Object {
            $_ -match "^src/TeleFlow\.Telegram\.Schema/.+\.g\.cs$" -or
            $_ -match "^src/TeleFlow\.Telegram\.Client/Generated/.+\.g\.cs$"
        } | Sort-Object -Unique)
}

function Assert-GeneratedHeaders {
    param(
        [string[]] $Files,
        [string] $TelegramBotApiVersion,
        [string] $TelegramBotApiRelease,
        [string] $TelegramBotApiChangelogUrl
    )

    foreach ($relativePath in $Files) {
        $path = Join-Path $repositoryRoot ($relativePath.Replace("/", [string] [System.IO.Path]::DirectorySeparatorChar))
        $contents = Get-Content -Raw -LiteralPath $path

        $expectedVersionLine = "//   Telegram Bot API version: $TelegramBotApiVersion"
        $expectedReleaseLine = "//   Telegram Bot API release: $TelegramBotApiRelease"
        $expectedChangelogLine = "//   Telegram Bot API changelog: $TelegramBotApiChangelogUrl"

        if (-not $contents.Contains($expectedVersionLine, [System.StringComparison]::Ordinal)) {
            throw "Generated file '$relativePath' has a Telegram Bot API version header that does not match manifest version '$TelegramBotApiVersion'."
        }

        if (-not $contents.Contains($expectedReleaseLine, [System.StringComparison]::Ordinal)) {
            throw "Generated file '$relativePath' has a Telegram Bot API release header that does not match manifest release '$TelegramBotApiRelease'."
        }

        if (-not $contents.Contains($expectedChangelogLine, [System.StringComparison]::Ordinal)) {
            throw "Generated file '$relativePath' has a Telegram Bot API changelog header that does not match manifest changelog '$TelegramBotApiChangelogUrl'."
        }
    }
}

Push-Location $repositoryRoot
try {
    $baseBranch = "$Remote/$BaseRef"
    if (-not $SkipFetch) {
        Invoke-Git @("fetch", "--quiet", $Remote, "+refs/heads/$BaseRef`:refs/remotes/$Remote/$BaseRef")
    }

    $mergeBase = (Invoke-GitOutput @("merge-base", "HEAD", $baseBranch) | Select-Object -First 1).Trim()
    if ([string]::IsNullOrWhiteSpace($mergeBase)) {
        throw "Could not resolve merge base between HEAD and $baseBranch."
    }

    $changedFiles = @(
        Invoke-GitOutput @("diff", "--name-only", $mergeBase, "HEAD", "--")
        Invoke-GitOutput @("diff", "--name-only", "HEAD", "--")
    ) |
        ForEach-Object { $_.Replace("\", "/", [System.StringComparison]::Ordinal).Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique

    if ($changedFiles.Count -eq 0) {
        Write-Host "No changed files against $baseBranch. Schema update guardrails skipped."
        return
    }

    $generatedSurfacePatterns = @(
        "^src/TeleFlow\.Telegram\.Schema/.+\.g\.cs$",
        "^src/TeleFlow\.Telegram\.Client/Generated/.+\.g\.cs$",
        "^src/TeleFlow\.Telegram\.Schema/telegram-bot-api\.manifest\.json$"
    )
    $schemaMetadataPatterns = @(
        "^docs/badges/telegram-bot-api\.json$"
    )

    $generatedSurfaceChanged = $false
    $schemaMetadataChanged = $false

    foreach ($changedFile in $changedFiles) {
        if (Test-PathMatches $changedFile $generatedSurfacePatterns) {
            $generatedSurfaceChanged = $true
        }

        if (Test-PathMatches $changedFile $schemaMetadataPatterns) {
            $schemaMetadataChanged = $true
        }
    }

    if (-not $generatedSurfaceChanged -and -not $schemaMetadataChanged) {
        Write-Host "No generated Telegram schema/client or schema metadata files changed. Schema update guardrails skipped."
        return
    }

    $manifestPath = Join-Path $repositoryRoot "src/TeleFlow.Telegram.Schema/telegram-bot-api.manifest.json"
    $manifest = Read-JsonFile $manifestPath
    $source = Get-RequiredProperty $manifest "source" "manifest"
    $telegramBotApi = Get-RequiredProperty $manifest "telegramBotApi" "manifest"
    $pipeline = Get-RequiredProperty $manifest "pipeline" "manifest"

    [void] (Get-RequiredIntProperty $manifest "manifestVersion" "manifest")
    [void] (Get-RequiredStringProperty $source "url" "manifest.source")
    [void] (Get-RequiredStringProperty $source "capturedAtUtc" "manifest.source")
    $sourceSha256 = Get-RequiredStringProperty $source "sha256" "manifest.source"
    $telegramBotApiVersion = Get-RequiredStringProperty $telegramBotApi "version" "manifest.telegramBotApi"
    $telegramBotApiRelease = Get-RequiredStringProperty $telegramBotApi "releasedAt" "manifest.telegramBotApi"
    $telegramBotApiChangelogUrl = Get-RequiredStringProperty $telegramBotApi "changelogUrl" "manifest.telegramBotApi"
    [void] (Get-RequiredIntProperty $pipeline "schemaVersion" "manifest.pipeline")
    [void] (Get-RequiredIntProperty $pipeline "generatorVersion" "manifest.pipeline")

    if ($sourceSha256 -notmatch "^[0-9a-f]{64}$") {
        throw "manifest.source.sha256 must be a 64-character lowercase hexadecimal SHA-256 value."
    }

    $badgePath = Join-Path $repositoryRoot "docs/badges/telegram-bot-api.json"
    $badge = Read-JsonFile $badgePath
    $badgeMessage = Get-RequiredStringProperty $badge "message" "telegram-bot-api badge"

    if ($badgeMessage -ne $telegramBotApiVersion) {
        throw "Telegram Bot API badge message '$badgeMessage' does not match manifest version '$telegramBotApiVersion'."
    }

    $generatedFiles = Get-TrackedGeneratedFiles
    if ($generatedFiles.Count -eq 0) {
        throw "No tracked generated Telegram schema/client files were found."
    }

    Assert-GeneratedHeaders `
        -Files $generatedFiles `
        -TelegramBotApiVersion $telegramBotApiVersion `
        -TelegramBotApiRelease $telegramBotApiRelease `
        -TelegramBotApiChangelogUrl $telegramBotApiChangelogUrl

    if ($generatedSurfaceChanged) {
        $changelogChanged = $changedFiles -contains "CHANGELOG.md"
        if (-not $changelogChanged) {
            throw "Generated Telegram schema/client output changed, but CHANGELOG.md was not updated."
        }

        $changelogPath = Join-Path $repositoryRoot "CHANGELOG.md"
        $changelog = Get-Content -Raw -LiteralPath $changelogPath
        $escapedVersion = [regex]::Escape($telegramBotApiVersion)
        $versionMentionPattern = "Telegram Bot API(?: schema version)?\s+$escapedVersion"

        if ($changelog -notmatch $versionMentionPattern) {
            throw "CHANGELOG.md does not mention Telegram Bot API $telegramBotApiVersion."
        }
    }

    Write-Host "Telegram schema update guardrails passed."
    Write-Host "Telegram Bot API version: $telegramBotApiVersion"
    Write-Host "Tracked generated files checked: $($generatedFiles.Count)"
}
finally {
    Pop-Location
}
