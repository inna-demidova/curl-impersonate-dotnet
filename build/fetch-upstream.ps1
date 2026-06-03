<#
.SYNOPSIS
  Downloads curl-impersonate v1.5.6 prebuilt tarballs, inspects their exports
  to confirm curl_easy_impersonate is present, and stages the libraries into
  src/CurlImpersonate.Bindings/runtimes/{rid}/native/.

.NOTES
  Run from the repo root: .\build\fetch-upstream.ps1
  Requires: curl (built-in since Win10 1803), 7-Zip or tar, and either
  dumpbin (MSVC) or objdump to inspect exports.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Version   = "1.5.6"
$BaseUrl   = "https://github.com/lexiforest/curl-impersonate/releases/download/v$Version"
$RepoRoot  = Split-Path $PSScriptRoot -Parent
$StageBase = Join-Path $RepoRoot "src\CurlImpersonate.Bindings\runtimes"
$TmpDir    = Join-Path $env:TEMP "cidn-upstream-$Version"

New-Item -ItemType Directory -Force -Path $TmpDir | Out-Null

# ---------------------------------------------------------------------------
# Helper: download a file if not already cached
# ---------------------------------------------------------------------------
function Fetch([string]$url, [string]$dest) {
    if (Test-Path $dest) {
        Write-Host "  (cached) $dest"
    } else {
        Write-Host "  Downloading $url ..."
        curl.exe -fsSL -o $dest $url
    }
}

# ---------------------------------------------------------------------------
# Windows: curl-impersonate-v{ver}-x86_64-win32.zip
# ---------------------------------------------------------------------------
Write-Host "`n=== Windows x64 ===" -ForegroundColor Cyan

$WinZip  = Join-Path $TmpDir "curl-impersonate-win-x64.zip"
$WinFile = "curl-impersonate-v$Version-x86_64-win32.zip"
Fetch "$BaseUrl/$WinFile" $WinZip

$WinExtract = Join-Path $TmpDir "win-x64"
if (-not (Test-Path $WinExtract)) {
    New-Item -ItemType Directory -Force -Path $WinExtract | Out-Null
    tar.exe -xf $WinZip -C $WinExtract
}

# Inspect exports — look for libcurl*.dll and confirm curl_easy_impersonate
$WinDlls = Get-ChildItem $WinExtract -Recurse -Filter "libcurl*.dll"
if (-not $WinDlls) {
    $WinDlls = Get-ChildItem $WinExtract -Recurse -Filter "*.dll" | Where-Object { $_.Name -notmatch 'cidn' }
}
if (-not $WinDlls) { throw "Could not find upstream DLL in Windows tarball at $WinExtract" }

$WinUpstreamDll = $WinDlls[0].FullName
Write-Host "  Upstream DLL: $WinUpstreamDll"

# Confirm curl_easy_impersonate is exported (requires dumpbin from MSVC)
$dumpbin = Get-Command dumpbin -ErrorAction SilentlyContinue
if ($dumpbin) {
    $exports = & dumpbin /exports $WinUpstreamDll 2>&1 | Select-String "curl_easy_impersonate"
    if (-not $exports) {
        throw "GATE FAILED: curl_easy_impersonate not found in exports of $WinUpstreamDll"
    }
    Write-Host "  [OK] curl_easy_impersonate confirmed in exports" -ForegroundColor Green
} else {
    Write-Warning "  dumpbin not found — skipping export inspection (install MSVC tools to enable gate)"
}

# Stage
$WinStage = Join-Path $StageBase "win-x64\native"
New-Item -ItemType Directory -Force -Path $WinStage | Out-Null
Copy-Item $WinUpstreamDll (Join-Path $WinStage "libcurl-impersonate.dll") -Force
Write-Host "  Staged to $WinStage"

# ---------------------------------------------------------------------------
# Linux: curl-impersonate-v{ver}-x86_64-linux-gnu.tar.gz
# ---------------------------------------------------------------------------
Write-Host "`n=== Linux x64 (for Docker build) ===" -ForegroundColor Cyan

$LinuxTar  = Join-Path $TmpDir "curl-impersonate-linux-x64.tar.gz"
$LinuxFile = "curl-impersonate-v$Version-x86_64-linux-gnu.tar.gz"
Fetch "$BaseUrl/$LinuxFile" $LinuxTar

$LinuxExtract = Join-Path $TmpDir "linux-x64"
if (-not (Test-Path $LinuxExtract)) {
    New-Item -ItemType Directory -Force -Path $LinuxExtract | Out-Null
    tar.exe -xzf $LinuxTar -C $LinuxExtract
}

# Find the upstream .so
$LinuxSos = Get-ChildItem $LinuxExtract -Recurse |
    Where-Object { $_.Name -match "libcurl-impersonate" -and $_.Name -match "\.so" }
if (-not $LinuxSos) { throw "Could not find upstream .so in Linux tarball at $LinuxExtract" }
$LinuxUpstreamSo = $LinuxSos[0].FullName
Write-Host "  Upstream .so: $LinuxUpstreamSo"

# Stage (Docker image will also have these; this is for local Linux test if WSL2 is used)
$LinuxStage = Join-Path $StageBase "linux-x64\native"
New-Item -ItemType Directory -Force -Path $LinuxStage | Out-Null
Copy-Item $LinuxUpstreamSo (Join-Path $LinuxStage "libcurl-impersonate-chrome.so") -Force
Write-Host "  Staged to $LinuxStage"

Write-Host "`n[fetch-upstream] Done." -ForegroundColor Green
