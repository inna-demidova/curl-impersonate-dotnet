<#
.SYNOPSIS
  Packs CurlImpersonate.Bindings.nupkg after verifying all native files are present.

.NOTES
  Run from the repo root: .\build\publish-nuget.ps1
  Pass -Push to also push to NuGet.org (requires NUGET_API_KEY env var).
#>

param(
    [switch]$Push,
    [string]$ApiKey = $env:NUGET_API_KEY
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot  = Split-Path $PSScriptRoot -Parent
$Csproj    = Join-Path $RepoRoot "src\CurlImpersonate.Bindings\CurlImpersonate.Bindings.csproj"
$OutputDir = Join-Path $RepoRoot "out\nuget"

# ---------------------------------------------------------------------------
# Gate: all native files must be present before packing
# ---------------------------------------------------------------------------
$Required = @(
    "src\CurlImpersonate.Bindings\runtimes\win-x64\native\cidn_wrapper.dll",
    "src\CurlImpersonate.Bindings\runtimes\win-x64\native\libcurl-impersonate.dll",
    "src\CurlImpersonate.Bindings\runtimes\linux-x64\native\libcidn_wrapper.so",
    "src\CurlImpersonate.Bindings\runtimes\linux-x64\native\libcurl-impersonate-chrome.so"
)

$missing = $Required | Where-Object { -not (Test-Path (Join-Path $RepoRoot $_)) }
if ($missing) {
    Write-Error @"
Missing native files — run the build scripts first:
  .\build\build-windows.ps1    (for cidn_wrapper.dll)
  .\build\fetch-upstream.ps1   (for upstream DLLs / .so)
  .\build\build-linux.ps1      (for libcidn_wrapper.so)

Missing:
$($missing -join "`n")
"@
    exit 1
}

Write-Host "All native files present." -ForegroundColor Green

# ---------------------------------------------------------------------------
# Pack
# ---------------------------------------------------------------------------
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Write-Host "`n-- dotnet pack --"
& dotnet pack $Csproj -c Release -o $OutputDir /p:IncludeSymbols=false
if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed" }

$nupkg = Get-ChildItem $OutputDir -Filter "*.nupkg" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host "  Packed: $($nupkg.FullName)" -ForegroundColor Cyan

# Quick inspect — list runtimes/ entries
Write-Host "`n-- Inspecting runtimes/ layout --"
& dotnet tool run nuget-package-explorer --list $nupkg.FullName 2>$null
& unzip -l $nupkg.FullName 2>$null | Select-String "runtimes"

if ($Push) {
    if (-not $ApiKey) { throw "Set NUGET_API_KEY env var or pass -ApiKey." }
    Write-Host "`n-- Pushing to NuGet.org --"
    & dotnet nuget push $nupkg.FullName --api-key $ApiKey --source https://api.nuget.org/v3/index.json
    if ($LASTEXITCODE -ne 0) { throw "nuget push failed" }
    Write-Host "[publish-nuget] Pushed!" -ForegroundColor Green
}

Write-Host "`n[publish-nuget] Done. Package at: $($nupkg.FullName)" -ForegroundColor Green
