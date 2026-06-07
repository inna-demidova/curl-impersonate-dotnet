<#
.SYNOPSIS
  Downloads the upstream curl-impersonate prebuilt library for a single platform and stages it
  into src/CurlImpersonate.Bindings/runtimes/{rid}/native/.

.DESCRIPTION
  The release URL and platform are configurable. By default it fetches the Windows library
  (libcurl-impersonate.dll + its zlib.dll dependency). Inside the Linux Docker build we invoke it
  with `-Platform linux` so it pulls the .so instead of the .dll. Cross-platform: uses pwsh
  Invoke-WebRequest + tar, so it runs on a Windows host and inside the dotnet SDK container alike.

.PARAMETER Platform
  windows | win | win-x64  -> stages win-x64 (.dll)   [default]
  linux   | linux-x64      -> stages linux-x64 (.so)
  all                      -> stages both

.PARAMETER Version
  Upstream curl-impersonate release version (without the leading 'v'). Default 1.5.6.

.PARAMETER BaseUrl
  Release download base URL. Defaults to the lexiforest GitHub release for $Version. Override to
  pull from a mirror or a pinned artifact store.

.NOTES
  Run from anywhere:  pwsh ./build/fetch-upstream.ps1                # Windows (default)
                      pwsh ./build/fetch-upstream.ps1 -Platform linux
#>

[CmdletBinding()]
param(
    [ValidateSet("windows", "win", "win-x64", "linux", "linux-x64", "all")]
    [string]$Platform = "windows",
    [string]$Version  = "1.5.6",
    [string]$BaseUrl  = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    $BaseUrl = "https://github.com/lexiforest/curl-impersonate/releases/download/v$Version"
}

$RepoRoot  = Split-Path $PSScriptRoot -Parent
$StageBase = Join-Path $RepoRoot "src/CurlImpersonate.Bindings/runtimes"
$TmpDir    = Join-Path ([System.IO.Path]::GetTempPath()) "cidn-upstream-$Version"
New-Item -ItemType Directory -Force -Path $TmpDir | Out-Null

function Get-Asset([string]$Url, [string]$Dest) {
    if (Test-Path $Dest) {
        Write-Host "  (cached) $Dest"
    } else {
        Write-Host "  Downloading $Url"
        Invoke-WebRequest -Uri $Url -OutFile $Dest
    }
}

function Expand-Tarball([string]$Tarball, [string]$Into) {
    Remove-Item -Recurse -Force $Into -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $Into | Out-Null
    tar -xzf $Tarball -C $Into
    if ($LASTEXITCODE -ne 0) { throw "tar extraction failed for $Tarball" }
}

function Fetch-Windows {
    Write-Host "`n=== win-x64 ===" -ForegroundColor Cyan
    $asset   = "libcurl-impersonate-v$Version.x86_64-win32.tar.gz"
    $tarball = Join-Path $TmpDir $asset
    Get-Asset "$BaseUrl/$asset" $tarball

    $extract = Join-Path $TmpDir "win-x64"
    Expand-Tarball $tarball $extract

    $dll = Get-ChildItem $extract -Recurse -Filter "libcurl-impersonate.dll" | Select-Object -First 1
    if (-not $dll) { throw "libcurl-impersonate.dll not found in $asset" }

    $stage = Join-Path $StageBase "win-x64/native"
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    Copy-Item $dll.FullName (Join-Path $stage "libcurl-impersonate.dll") -Force
    Write-Host "  Staged libcurl-impersonate.dll"

    # libcurl-impersonate.dll has a runtime dependency on zlib.dll shipped alongside it; stage any
    # sibling DLLs so the OS loader resolves them from the same directory.
    Get-ChildItem (Split-Path $dll.FullName -Parent) -Filter "*.dll" |
        Where-Object { $_.Name -ne "libcurl-impersonate.dll" } |
        ForEach-Object {
            Copy-Item $_.FullName (Join-Path $stage $_.Name) -Force
            Write-Host "  + dependency $($_.Name)"
        }

    Write-Host "  Staged to $stage" -ForegroundColor Green
}

function Fetch-Linux {
    Write-Host "`n=== linux-x64 ===" -ForegroundColor Cyan
    $asset   = "libcurl-impersonate-v$Version.x86_64-linux-gnu.tar.gz"
    $tarball = Join-Path $TmpDir $asset
    Get-Asset "$BaseUrl/$asset" $tarball

    $extract = Join-Path $TmpDir "linux-x64"
    Expand-Tarball $tarball $extract

    # The tarball ships libcurl-impersonate.so -> .so.4 -> .so.4.8.0 (the last is the real binary;
    # the shorter names are symlinks). Pick the fully-versioned real file (longest name) and stage
    # it under the unversioned name the native loader globs for (libcurl-impersonate*.so*).
    $so = Get-ChildItem $extract -Recurse -Filter "libcurl-impersonate.so*" |
          Sort-Object { $_.Name.Length } -Descending | Select-Object -First 1
    if (-not $so) { throw "libcurl-impersonate.so not found in $asset" }

    $stage = Join-Path $StageBase "linux-x64/native"
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    Copy-Item $so.FullName (Join-Path $stage "libcurl-impersonate.so") -Force
    Write-Host "  Staged $($so.Name) -> libcurl-impersonate.so in $stage" -ForegroundColor Green
}

switch ($Platform) {
    { $_ -in "windows", "win", "win-x64" } { Fetch-Windows }
    { $_ -in "linux", "linux-x64" }        { Fetch-Linux }
    "all"                                   { Fetch-Windows; Fetch-Linux }
}

Write-Host "`n[fetch-upstream] Done ($Platform, v$Version)." -ForegroundColor Green
