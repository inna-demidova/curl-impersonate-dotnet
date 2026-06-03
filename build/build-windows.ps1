<#
.SYNOPSIS
  Builds cidn_wrapper.dll on Windows using CMake + MSVC (VS 2022 x64).
  Stages the output into src/CurlImpersonate.Bindings/runtimes/win-x64/native/.

.NOTES
  Run from the repo root: .\build\build-windows.ps1
  Requires: CMake 3.20+, Visual Studio 2022 with C++ workload.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path $PSScriptRoot -Parent
$NativeDir = Join-Path $RepoRoot "native"
$BuildDir  = Join-Path $RepoRoot "out\win-x64"
$StageDir  = Join-Path $RepoRoot "src\CurlImpersonate.Bindings\runtimes\win-x64\native"

# ---------------------------------------------------------------------------
# Pre-flight: CMake
# ---------------------------------------------------------------------------
$cmake = Get-Command cmake -ErrorAction SilentlyContinue
if (-not $cmake) {
    Write-Error @"
CMake not found on PATH.
Install it via one of:
  winget install Kitware.CMake
  choco install cmake --installargs 'ADD_CMAKE_TO_PATH=System'
Then re-open your terminal and retry.
"@
    exit 1
}

$cmakeVersion = & cmake --version 2>&1 | Select-String '\d+\.\d+\.\d+' | ForEach-Object { $_.Matches[0].Value }
Write-Host "  CMake $cmakeVersion found" -ForegroundColor Cyan

# ---------------------------------------------------------------------------
# Configure & build (Release, x64)
# ---------------------------------------------------------------------------
New-Item -ItemType Directory -Force -Path $BuildDir | Out-Null

Write-Host "`n-- CMake configure (VS2022 x64 Release) --"
& cmake -S $NativeDir -B $BuildDir `
    -G "Visual Studio 17 2022" -A x64 `
    -DCMAKE_BUILD_TYPE=Release
if ($LASTEXITCODE -ne 0) { throw "cmake configure failed" }

Write-Host "`n-- CMake build --"
& cmake --build $BuildDir --config Release
if ($LASTEXITCODE -ne 0) { throw "cmake build failed" }

# ---------------------------------------------------------------------------
# Stage
# ---------------------------------------------------------------------------
$built = Join-Path $BuildDir "staging\cidn_wrapper.dll"
if (-not (Test-Path $built)) {
    # MSVC Release subfolder
    $built = Join-Path $BuildDir "staging\Release\cidn_wrapper.dll"
}
if (-not (Test-Path $built)) { throw "cidn_wrapper.dll not found after build — check CMake output in $BuildDir" }

New-Item -ItemType Directory -Force -Path $StageDir | Out-Null
Copy-Item $built (Join-Path $StageDir "cidn_wrapper.dll") -Force
Write-Host "`n[build-windows] cidn_wrapper.dll staged to $StageDir" -ForegroundColor Green
