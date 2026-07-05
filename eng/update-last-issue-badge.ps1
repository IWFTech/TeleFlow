param(
    [string] $Repository = "IWFTech/TeleFlow",
    [string] $OutputPath = "docs/badges/last-issue.json",
    [datetimeoffset] $Now = [datetimeoffset]::UtcNow
)

$ErrorActionPreference = "Stop"

function Invoke-GitHubApi {
    param([string] $Uri)

    $headers = @{
        "Accept" = "application/vnd.github+json"
        "User-Agent" = "TeleFlowLastIssueBadge"
        "X-GitHub-Api-Version" = "2022-11-28"
    }

    if (-not [string]::IsNullOrWhiteSpace($env:GH_TOKEN)) {
        $headers["Authorization"] = "Bearer $env:GH_TOKEN"
    }

    Invoke-RestMethod -Uri $Uri -Headers $headers
}

function Format-RelativeDate {
    param(
        [datetimeoffset] $CreatedAt,
        [datetimeoffset] $CurrentTime
    )

    $createdDate = $CreatedAt.UtcDateTime.Date
    $currentDate = $CurrentTime.UtcDateTime.Date
    $days = [int]($currentDate - $createdDate).TotalDays

    if ($days -le 0) {
        return "today"
    }

    if ($days -eq 1) {
        return "yesterday"
    }

    if ($days -lt 30) {
        return "$days days ago"
    }

    $months = [int][Math]::Floor($days / 30)

    if ($months -lt 12) {
        if ($months -eq 1) {
            return "1 month ago"
        }

        return "$months months ago"
    }

    $years = [int][Math]::Floor($days / 365)

    if ($years -eq 1) {
        return "1 year ago"
    }

    return "$years years ago"
}

function ConvertTo-UtcDateTimeOffset {
    param([object] $Value)

    if ($Value -is [datetime]) {
        $utcDateTime = if ($Value.Kind -eq [System.DateTimeKind]::Utc) {
            $Value
        }
        else {
            $Value.ToUniversalTime()
        }

        return [datetimeoffset]::new($utcDateTime)
    }

    return [datetimeoffset]::Parse(
        [string] $Value,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::AssumeUniversal -bor [System.Globalization.DateTimeStyles]::AdjustToUniversal)
}

function Resolve-BadgeColor {
    param([int] $Days)

    if ($Days -le 7) {
        return "brightgreen"
    }

    if ($Days -le 30) {
        return "green"
    }

    if ($Days -le 90) {
        return "yellowgreen"
    }

    return "yellow"
}

$encodedQuery = [System.Uri]::EscapeDataString("repo:$Repository is:issue")
$uri = "https://api.github.com/search/issues?q=$encodedQuery&sort=created&order=desc&per_page=1"
$response = Invoke-GitHubApi -Uri $uri

if ($response.total_count -eq 0 -or $response.items.Count -eq 0) {
    $badge = [ordered]@{
        schemaVersion = 1
        label = "last issue"
        message = "none"
        color = "lightgrey"
    }
}
else {
    $issue = $response.items[0]
    $createdAt = ConvertTo-UtcDateTimeOffset -Value $issue.created_at
    $days = [int]($Now.UtcDateTime.Date - $createdAt.UtcDateTime.Date).TotalDays

    $badge = [ordered]@{
        schemaVersion = 1
        label = "last issue"
        message = Format-RelativeDate -CreatedAt $createdAt -CurrentTime $Now
        color = Resolve-BadgeColor -Days $days
        namedLogo = "github"
    }
}

$fullOutputPath = Join-Path (Get-Location) $OutputPath
$outputDirectory = Split-Path -Parent $fullOutputPath
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

$json = $badge | ConvertTo-Json -Depth 4
Set-Content -Path $fullOutputPath -Value $json -Encoding utf8
