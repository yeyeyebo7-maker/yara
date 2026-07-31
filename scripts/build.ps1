param(
    [switch]$SkipIcon
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

if (-not $SkipIcon) {
    Write-Host "==> Building icon"
    & (Join-Path $PSScriptRoot 'build-icon.ps1')
}

Write-Host "==> Publishing yara (self-contained, single file)"
dotnet publish (Join-Path $root 'yara.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -o (Join-Path $root 'publish')
if ($LASTEXITCODE -ne 0) { throw "yara publish failed" }

Write-Host "==> Compressing yara.exe for the installer"
$embedDir = Join-Path $root 'yara-installer\embed'
New-Item -ItemType Directory -Path $embedDir -Force | Out-Null
$src = [System.IO.File]::ReadAllBytes((Join-Path $root 'publish\yara.exe'))
$out = [System.IO.File]::Create((Join-Path $embedDir 'yara.exe.gz'))
$gz = New-Object System.IO.Compression.GZipStream($out, [System.IO.Compression.CompressionLevel]::Optimal)
$gz.Write($src, 0, $src.Length)
$gz.Close()
$out.Close()

Write-Host "==> Publishing yara-installer (self-contained, single file)"
dotnet publish (Join-Path $root 'yara-installer\yara-installer.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
    -o (Join-Path $root 'yara-installer\publish')
if ($LASTEXITCODE -ne 0) { throw "installer publish failed" }

Write-Host ""
Write-Host "Done. Artifacts:"
Write-Host "  " (Join-Path $root 'publish\yara.exe')
Write-Host "  " (Join-Path $root 'yara-installer\publish\yara-installer.exe')
