param(
    [Parameter(Mandatory = $true)]
    [string] $CoveragePath,

    [double] $MinimumLineCoverage = 85.0,

    [double] $MinimumBranchCoverage = 75.0
)

$ErrorActionPreference = "Stop"

function ConvertTo-CoveragePercentage {
    param(
        [long] $Covered,
        [long] $Valid,
        [string] $MetricName
    )

    if ($Valid -le 0) {
        throw "Coverage report contains no valid $MetricName entries."
    }

    [Math]::Floor(($Covered / $Valid) * 10000) / 100
}

$resolvedCoveragePath = Resolve-Path -LiteralPath $CoveragePath
[xml] $coverageReport = Get-Content -LiteralPath $resolvedCoveragePath
$coverage = $coverageReport.coverage

if ($null -eq $coverage) {
    throw "Coverage report '$resolvedCoveragePath' does not contain a Cobertura coverage root."
}

$linesCovered = [long]$coverage."lines-covered"
$linesValid = [long]$coverage."lines-valid"
$branchesCovered = [long]$coverage."branches-covered"
$branchesValid = [long]$coverage."branches-valid"

$lineCoverage = ConvertTo-CoveragePercentage `
    -Covered $linesCovered `
    -Valid $linesValid `
    -MetricName "line"
$branchCoverage = ConvertTo-CoveragePercentage `
    -Covered $branchesCovered `
    -Valid $branchesValid `
    -MetricName "branch"
$lineCoverageText = $lineCoverage.ToString(
    "0.00",
    [System.Globalization.CultureInfo]::InvariantCulture)
$branchCoverageText = $branchCoverage.ToString(
    "0.00",
    [System.Globalization.CultureInfo]::InvariantCulture)
$minimumLineCoverageText = $MinimumLineCoverage.ToString(
    "0.00",
    [System.Globalization.CultureInfo]::InvariantCulture)
$minimumBranchCoverageText = $MinimumBranchCoverage.ToString(
    "0.00",
    [System.Globalization.CultureInfo]::InvariantCulture)

$summary = @"
## Aggregate coverage gate

| Metric | Coverage | Required | Result |
| --- | ---: | ---: | :---: |
| Lines | $lineCoverageText% ($linesCovered / $linesValid) | $minimumLineCoverageText% | $(if ($lineCoverage -ge $MinimumLineCoverage) { "pass" } else { "fail" }) |
| Branches | $branchCoverageText% ($branchesCovered / $branchesValid) | $minimumBranchCoverageText% | $(if ($branchCoverage -ge $MinimumBranchCoverage) { "pass" } else { "fail" }) |
"@

Write-Host $summary

if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $summary -Encoding utf8
}

$failures = @()

if ($lineCoverage -lt $MinimumLineCoverage) {
    $failures += "Line coverage $lineCoverageText% is below $minimumLineCoverageText%."
}

if ($branchCoverage -lt $MinimumBranchCoverage) {
    $failures += "Branch coverage $branchCoverageText% is below $minimumBranchCoverageText%."
}

if ($failures.Count -gt 0) {
    throw ($failures -join " ")
}

Write-Host "Aggregate coverage thresholds passed."
