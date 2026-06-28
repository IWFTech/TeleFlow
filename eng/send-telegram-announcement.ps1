param(
    [string] $EventPath = "",
    [string] $Kind = "",
    [string] $Title = "",
    [string] $Number = "",
    [string] $State = "",
    [string] $Url = "",
    [string] $Body = "",
    [string] $Tag = "",
    [string] $Repository = "",
    [string] $BotToken = $env:TELEGRAM_NOTIFY_BOT_TOKEN,
    [string] $ChatId = $env:TELEGRAM_NOTIFY_CHAT_ID,
    [string] $ReleasesThreadId = $env:TELEGRAM_RELEASES_THREAD_ID,
    [string] $IssuesThreadId = $env:TELEGRAM_ISSUES_THREAD_ID,
    [string] $PullRequestsThreadId = $env:TELEGRAM_PULL_REQUESTS_THREAD_ID,
    [int] $MaximumMessageLength = 4096,
    [int] $IssueBodyPreviewLength = 600,
    [int] $PullRequestBodyPreviewLength = 360,
    [switch] $DryRun
)

$ErrorActionPreference = "Stop"

if ($MaximumMessageLength -lt 512) {
    throw "MaximumMessageLength must be at least 512."
}

if ($IssueBodyPreviewLength -lt 0) {
    throw "IssueBodyPreviewLength must not be negative."
}

if ($PullRequestBodyPreviewLength -lt 0) {
    throw "PullRequestBodyPreviewLength must not be negative."
}

function Read-GitHubEvent {
    param([string] $Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $null
    }

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "GitHub event payload was not found: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Select-FirstText {
    param([AllowEmptyString()][string[]] $Values)

    foreach ($value in $Values) {
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value
        }
    }

    return ""
}

function ConvertTo-HtmlText {
    param([AllowEmptyString()][string] $Text)

    return [System.Net.WebUtility]::HtmlEncode($Text)
}

function ConvertTo-PlainTextPreview {
    param(
        [AllowEmptyString()][string] $Text,
        [int] $MaximumLength
    )

    if ($MaximumLength -eq 0 -or [string]::IsNullOrWhiteSpace($Text)) {
        return ""
    }

    $normalized = $Text.Trim()
    $normalized = [System.Text.RegularExpressions.Regex]::Replace($normalized, '```[\s\S]*?```', '[code block]')
    $normalized = [System.Text.RegularExpressions.Regex]::Replace($normalized, '`([^`]+)`', '$1')
    $normalized = [System.Text.RegularExpressions.Regex]::Replace($normalized, '\[([^\]]+)\]\([^)]+\)', '$1')
    $normalized = [System.Text.RegularExpressions.Regex]::Replace($normalized, '(?m)^\s{0,3}#{1,6}\s*', "")
    $normalized = [System.Text.RegularExpressions.Regex]::Replace($normalized, '(?m)^\s*[-*+]\s+', "- ")
    $normalized = [System.Text.RegularExpressions.Regex]::Replace($normalized, "\r\n?", "`n")
    $normalized = [System.Text.RegularExpressions.Regex]::Replace($normalized, "`n{3,}", "`n`n")

    if ($normalized.Length -le $MaximumLength) {
        return $normalized
    }

    return $normalized.Substring(0, $MaximumLength).TrimEnd() + "..."
}

function Get-BodyPreviewLength {
    param([string] $AnnouncementKind)

    switch ($AnnouncementKind) {
        "issue" { return $IssueBodyPreviewLength }
        "pull_request" { return $PullRequestBodyPreviewLength }
        default { return -1 }
    }
}

function Select-MarkdownSection {
    param(
        [AllowEmptyString()][string] $Text,
        [string] $Heading
    )

    if ([string]::IsNullOrWhiteSpace($Text) -or [string]::IsNullOrWhiteSpace($Heading)) {
        return $Text
    }

    $escapedHeading = [System.Text.RegularExpressions.Regex]::Escape($Heading)
    $pattern = "(?ms)^\s*#{1,6}\s+$escapedHeading\s*$\s*(.*?)(?=^\s*#{1,6}\s+\S|\z)"
    $match = [System.Text.RegularExpressions.Regex]::Match($Text, $pattern)

    if (-not $match.Success) {
        return $Text
    }

    return $match.Groups[1].Value.Trim()
}

function ConvertTo-Announcement {
    param([object] $Payload)

    if ($null -eq $Payload) {
        return @{
            Kind = $Kind
            Title = $Title
            Number = $Number
            State = $State
            Url = $Url
            Body = $Body
            Tag = $Tag
            Repository = $Repository
        }
    }

    if ($null -ne $Payload.release) {
        return @{
            Kind = "release"
            Title = Select-FirstText @($Title, $Payload.release.name, $Payload.release.tag_name)
            Number = ""
            State = "published"
            Url = Select-FirstText @($Url, $Payload.release.html_url)
            Body = Select-FirstText @($Body, $Payload.release.body)
            Tag = Select-FirstText @($Tag, $Payload.release.tag_name)
            Repository = Select-FirstText @($Repository, $Payload.repository.full_name)
        }
    }

    if ($null -ne $Payload.issue) {
        return @{
            Kind = "issue"
            Title = Select-FirstText @($Title, $Payload.issue.title)
            Number = Select-FirstText @($Number, [string] $Payload.issue.number)
            State = Select-FirstText @($State, $Payload.action, $Payload.issue.state)
            Url = Select-FirstText @($Url, $Payload.issue.html_url)
            Body = Select-FirstText @($Body, $Payload.issue.body)
            Tag = ""
            Repository = Select-FirstText @($Repository, $Payload.repository.full_name)
        }
    }

    if ($null -ne $Payload.pull_request) {
        $pullRequestState = Select-FirstText @($State, $Payload.action, $Payload.pull_request.state)

        if ($Payload.action -eq "closed" -and $Payload.pull_request.merged -eq $true) {
            $pullRequestState = "merged"
        }

        return @{
            Kind = "pull_request"
            Title = Select-FirstText @($Title, $Payload.pull_request.title)
            Number = Select-FirstText @($Number, [string] $Payload.pull_request.number)
            State = $pullRequestState
            Url = Select-FirstText @($Url, $Payload.pull_request.html_url)
            Body = Select-FirstText @($Body, $Payload.pull_request.body)
            Tag = ""
            Repository = Select-FirstText @($Repository, $Payload.repository.full_name)
        }
    }

    throw "Unsupported GitHub event payload."
}

function Get-ThreadIdForAnnouncement {
    param([string] $AnnouncementKind)

    switch ($AnnouncementKind) {
        "release" { return $ReleasesThreadId }
        "issue" { return $IssuesThreadId }
        "pull_request" { return $PullRequestsThreadId }
        default { throw "Unsupported announcement kind: '$AnnouncementKind'." }
    }
}

function Get-Heading {
    param(
        [string] $AnnouncementKind,
        [string] $AnnouncementState
    )

    switch ($AnnouncementKind) {
        "release" { return "TeleFlow release" }
        "issue" {
            switch ($AnnouncementState) {
                "opened" { return "TeleFlow issue opened" }
                "reopened" { return "TeleFlow issue reopened" }
                "closed" { return "TeleFlow issue closed" }
                default { return "TeleFlow issue update" }
            }
        }
        "pull_request" {
            switch ($AnnouncementState) {
                "opened" { return "TeleFlow pull request opened" }
                "reopened" { return "TeleFlow pull request reopened" }
                "ready_for_review" { return "TeleFlow pull request ready for review" }
                "merged" { return "TeleFlow pull request merged" }
                "closed" { return "TeleFlow pull request closed" }
                default { return "TeleFlow pull request update" }
            }
        }
        default { throw "Unsupported announcement kind: '$AnnouncementKind'." }
    }
}

function Split-PlainTextForHtmlMessage {
    param(
        [AllowEmptyString()][string] $Text,
        [int] $MaximumEncodedLength
    )

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return @()
    }

    if ($MaximumEncodedLength -lt 256) {
        throw "Maximum encoded body chunk length must be at least 256."
    }

    $chunks = [System.Collections.Generic.List[string]]::new()
    $remaining = $Text

    while ((ConvertTo-HtmlText $remaining).Length -gt $MaximumEncodedLength) {
        $candidateLength = [Math]::Min($remaining.Length, $MaximumEncodedLength)

        while ($candidateLength -gt 1 -and (ConvertTo-HtmlText $remaining.Substring(0, $candidateLength)).Length -gt $MaximumEncodedLength) {
            $candidateLength = [Math]::Max(1, [int] [Math]::Floor($candidateLength * 0.8))
        }

        $splitAt = $remaining.LastIndexOf("`n", $candidateLength - 1, $candidateLength)

        if ($splitAt -lt 128) {
            $splitAt = $candidateLength
        }

        $chunk = $remaining.Substring(0, $splitAt).TrimEnd()

        if (-not [string]::IsNullOrWhiteSpace($chunk)) {
            $chunks.Add($chunk)
        }

        $remaining = $remaining.Substring($splitAt).TrimStart()
    }

    if (-not [string]::IsNullOrWhiteSpace($remaining)) {
        $chunks.Add($remaining)
    }

    return $chunks.ToArray()
}

function New-AnnouncementHeaderText {
    param([hashtable] $Announcement)

    $lines = [System.Collections.Generic.List[string]]::new()
    $heading = Get-Heading `
        -AnnouncementKind $Announcement.Kind `
        -AnnouncementState $Announcement.State

    $lines.Add("<b>$(ConvertTo-HtmlText $heading)</b>")

    if (-not [string]::IsNullOrWhiteSpace($Announcement.Title)) {
        $title = ConvertTo-HtmlText $Announcement.Title

        if ([string]::IsNullOrWhiteSpace($Announcement.Number)) {
            $lines.Add($title)
        }
        else {
            $number = ConvertTo-HtmlText $Announcement.Number
            $lines.Add("#${number}: $title")
        }
    }

    if ($Announcement.Kind -eq "release" -and -not [string]::IsNullOrWhiteSpace($Announcement.Tag)) {
        $lines.Add("<b>Tag:</b> $(ConvertTo-HtmlText $Announcement.Tag)")
    }

    if (-not [string]::IsNullOrWhiteSpace($Announcement.Repository)) {
        $lines.Add("<b>Repository:</b> $(ConvertTo-HtmlText $Announcement.Repository)")
    }

    if (-not [string]::IsNullOrWhiteSpace($Announcement.Url)) {
        $url = ConvertTo-HtmlText $Announcement.Url
        $lines.Add("<a href=""$url"">Open on GitHub</a>")
    }

    return ($lines -join "`n")
}

function New-AnnouncementText {
    param([hashtable] $Announcement)

    $header = New-AnnouncementHeaderText $Announcement
    $previewLength = Get-BodyPreviewLength $Announcement.Kind
    $normalizedBody = ([string] $Announcement.Body).Trim()

    if ($Announcement.Kind -eq "pull_request") {
        $normalizedBody = Select-MarkdownSection `
            -Text $normalizedBody `
            -Heading "Summary"
    }

    if ($previewLength -ge 0) {
        $normalizedBody = ConvertTo-PlainTextPreview `
            -Text $normalizedBody `
            -MaximumLength $previewLength
    }

    if ([string]::IsNullOrWhiteSpace($normalizedBody)) {
        return @($header)
    }

    $quotePrefix = "`n`n<blockquote expandable>"
    $quoteSuffix = "</blockquote>"
    $availableBodyLength = $MaximumMessageLength - $header.Length - $quotePrefix.Length - $quoteSuffix.Length

    if ($availableBodyLength -lt 256) {
        throw "Announcement header is too long to fit into a Telegram message."
    }

    $bodyChunks = @(Split-PlainTextForHtmlMessage `
            -Text $normalizedBody `
            -MaximumEncodedLength $availableBodyLength)

    $messages = [System.Collections.Generic.List[string]]::new()

    for ($index = 0; $index -lt $bodyChunks.Count; $index++) {
        $messageHeader = $header

        if ($index -gt 0) {
            $messageHeader = $header + "`nPart $($index + 1)"
        }

        $messages.Add($messageHeader + $quotePrefix + (ConvertTo-HtmlText $bodyChunks[$index]) + $quoteSuffix)
    }

    return $messages.ToArray()
}

function Assert-TelegramConfiguration {
    param(
        [string] $Token,
        [string] $TargetChatId,
        [string] $TargetThreadId
    )

    if ([string]::IsNullOrWhiteSpace($Token)) {
        throw "TELEGRAM_NOTIFY_BOT_TOKEN is required unless DryRun is enabled."
    }

    if ([string]::IsNullOrWhiteSpace($TargetChatId)) {
        throw "TELEGRAM_NOTIFY_CHAT_ID is required unless DryRun is enabled."
    }

    if ([string]::IsNullOrWhiteSpace($TargetThreadId)) {
        throw "The matching Telegram topic thread id variable is required unless DryRun is enabled."
    }

    $parsedThreadId = 0

    if (-not [int]::TryParse($TargetThreadId, [ref] $parsedThreadId) -or $parsedThreadId -le 0) {
        throw "Telegram topic thread id must be a positive integer."
    }

    return $parsedThreadId
}

function Send-TelegramMessage {
    param(
        [string] $Token,
        [string] $TargetChatId,
        [int] $TargetThreadId,
        [string] $Text
    )

    $uri = "https://api.telegram.org/bot$Token/sendMessage"
    $payload = @{
        chat_id = $TargetChatId
        message_thread_id = $TargetThreadId
        text = $Text
        parse_mode = "HTML"
        disable_web_page_preview = $true
    }

    try {
        Invoke-RestMethod `
            -Method Post `
            -Uri $uri `
            -ContentType "application/json; charset=utf-8" `
            -Body ($payload | ConvertTo-Json -Depth 4) `
            -ErrorAction Stop | Out-Null
    }
    catch {
        throw "Telegram sendMessage request failed. Check bot token, chat id, topic id, and bot permissions."
    }
}

$payload = Read-GitHubEvent $EventPath
$announcement = ConvertTo-Announcement $payload
$announcement.Kind = Select-FirstText @($announcement.Kind, $Kind)
$announcement.Title = Select-FirstText @($announcement.Title, $Title, "Untitled")
$threadId = Get-ThreadIdForAnnouncement $announcement.Kind
$messages = @(New-AnnouncementText $announcement)

if ($DryRun) {
    Write-Host "Dry run enabled. Telegram messages were not sent."
    Write-Host "Announcement kind: $($announcement.Kind)"
    Write-Host "Message count: $($messages.Count)"

    for ($index = 0; $index -lt $messages.Count; $index++) {
        Write-Host ""
        Write-Host "==> Telegram message $($index + 1)"
        Write-Host $messages[$index]
    }

    return
}

$parsedThreadId = Assert-TelegramConfiguration `
    -Token $BotToken `
    -TargetChatId $ChatId `
    -TargetThreadId $threadId

foreach ($message in $messages) {
    Send-TelegramMessage `
        -Token $BotToken `
        -TargetChatId $ChatId `
        -TargetThreadId $parsedThreadId `
        -Text $message
}

Write-Host "Telegram announcement sent. Kind: $($announcement.Kind). Message count: $($messages.Count)"
