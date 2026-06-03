<#
.SYNOPSIS
  Builds libcidn_wrapper.so inside a gcc:13 Docker container (glibc, not musl)
  and copies the result back to the host staging directory.

.NOTES
  Run from the repo root: .\build\build-linux.ps1
  Requires: Docker Desktop running with Linux containers.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path $PSScriptRoot -Parent
$StageDir = Join-Path $RepoRoot "src\CurlImpersonate.Bindings\runtimes\linux-x64\native"

$docker = Get-Command docker -ErrorAction SilentlyContinue
if (-not $docker) { throw "Docker not found on PATH. Install Docker Desktop." }

Write-Host "-- Building Docker image (linux build) ..."
& docker build -f (Join-Path $PSScriptRoot "Dockerfile.linux") -t cidn-linux-build $RepoRoot
if ($LASTEXITCODE -ne 0) { throw "docker build failed" }

Write-Host "-- Extracting libcidn_wrapper.so from container ..."
$containerId = & docker create cidn-linux-build 2>&1
if ($LASTEXITCODE -ne 0) { throw "docker create failed" }

try {
    New-Item -ItemType Directory -Force -Path $StageDir | Out-Null
    & docker cp "${containerId}:/export/libcidn_wrapper.so" (Join-Path $StageDir "libcidn_wrapper.so")
    if ($LASTEXITCODE -ne 0) { throw "docker cp failed" }
} finally {
    & docker rm $containerId | Out-Null
}

Write-Host "[build-linux] libcidn_wrapper.so staged to $StageDir" -ForegroundColor Green
