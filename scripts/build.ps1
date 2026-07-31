param(
    [switch]$SkipIcon
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$fftwUrl = 'https://fftw.org/pub/fftw/fftw-3.3.5-dll64.zip'
$fftwRoot = Join-Path $env:TEMP 'opencode-fftw'
$fftwDir = $fftwRoot
$fftwLib = Join-Path $fftwDir 'libfftw3-3.dll'

if (-not $SkipIcon) {
    Write-Host "==> Building icon"
    & (Join-Path $PSScriptRoot 'build-icon.ps1')
}

Write-Host "==> Ensuring FFTW3 is available"
if (-not (Test-Path $fftwLib)) {
    $zip = Join-Path $fftwRoot 'fftw.zip'
    New-Item -ItemType Directory -Path $fftwRoot -Force | Out-Null
    Write-Host "    downloading $fftwUrl"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $fftwUrl -OutFile $zip -UserAgent 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'
    Expand-Archive -Path $zip -DestinationPath $fftwRoot -Force
    $found = Get-ChildItem $fftwRoot -Recurse -Filter 'libfftw3-3.dll' | Select-Object -First 1
    if (-not $found) { throw 'libfftw3-3.dll not found after extraction' }
    $fftwDir = $found.DirectoryName
    $fftwLib = $found.FullName
}
Write-Host "    using $fftwLib"

Write-Host "==> Configuring CMake build (MinGW, terminal-only)"
$buildDir = Join-Path $root 'build'
cmake -S $root -B $buildDir -G 'MinGW Makefiles' -DCMAKE_BUILD_TYPE=Release `
    "-DFFTW3_INCLUDE_DIR=$fftwDir" "-DFFTW3_LIB=$fftwLib"
if ($LASTEXITCODE -ne 0) { throw 'cmake configure failed' }

cmake --build $buildDir --parallel
if ($LASTEXITCODE -ne 0) { throw 'cmake build failed' }

$yaraExe = Join-Path $buildDir 'yara.exe'
if (-not (Test-Path $yaraExe)) { throw 'yara.exe was not produced' }

Write-Host "==> Staging runtime files"
$stageDir = Join-Path $root 'publish'
New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
Copy-Item $yaraExe (Join-Path $stageDir 'yara.exe') -Force
Copy-Item $fftwLib (Join-Path $stageDir 'libfftw3-3.dll') -Force

Write-Host "==> Compressing embedded files for the installer"
$embedDir = Join-Path $root 'yara-installer\embed'
New-Item -ItemType Directory -Path $embedDir -Force | Out-Null
foreach ($name in 'yara.exe', 'libfftw3-3.dll') {
    $src = [System.IO.File]::ReadAllBytes((Join-Path $stageDir $name))
    $out = [System.IO.File]::Create((Join-Path $embedDir ($name + '.gz')))
    $gz = New-Object System.IO.Compression.GZipStream($out, [System.IO.Compression.CompressionLevel]::Optimal)
    $gz.Write($src, 0, $src.Length)
    $gz.Close()
    $out.Close()
}

Write-Host "==> Publishing yara-installer (self-contained, single file)"
dotnet publish (Join-Path $root 'yara-installer\yara-installer.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
    -o (Join-Path $root 'yara-installer\publish')
if ($LASTEXITCODE -ne 0) { throw 'installer publish failed' }

Write-Host ""
Write-Host "Done. Artifacts:"
Write-Host "  " (Join-Path $stageDir 'yara.exe')
Write-Host "  " (Join-Path $stageDir 'libfftw3-3.dll')
Write-Host "  " (Join-Path $root 'yara-installer\publish\yara-installer.exe')
