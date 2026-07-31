param(
    [string]$VcpkgRoot = $env:VCPKG_INSTALLATION_ROOT,
    [switch]$SkipIcon
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$triplet = 'x64-mingw-dynamic'

if (-not $SkipIcon) {
    Write-Host "==> Building icon"
    & (Join-Path $PSScriptRoot 'build-icon.ps1')
}

if (-not $VcpkgRoot -or -not (Test-Path (Join-Path $VcpkgRoot 'vcpkg.exe'))) {
    foreach ($c in @('C:\vcpkg', "$env:USERPROFILE\vcpkg")) {
        if (Test-Path (Join-Path $c 'vcpkg.exe')) {
            $VcpkgRoot = $c
            break
        }
    }
}
if (-not $VcpkgRoot -or -not (Test-Path (Join-Path $VcpkgRoot 'vcpkg.exe'))) {
    throw 'vcpkg.exe not found. Set VCPKG_INSTALLATION_ROOT or pass -VcpkgRoot.'
}
$toolchain = Join-Path $VcpkgRoot 'scripts\buildsystems\vcpkg.cmake'
$installed = Join-Path $VcpkgRoot "installed\$triplet"

Write-Host "==> Installing vcpkg dependencies (sdl2, glew, fftw3)"
& (Join-Path $VcpkgRoot 'vcpkg.exe') install sdl2 glew fftw3 --triplet $triplet --classic
if ($LASTEXITCODE -ne 0) { throw 'vcpkg install failed' }

Write-Host "==> Configuring CMake build (MinGW + vcpkg)"
$buildDir = Join-Path $root 'build'
cmake -S $root -B $buildDir -G 'MinGW Makefiles' -DCMAKE_BUILD_TYPE=Release `
    "-DCMAKE_TOOLCHAIN_FILE=$toolchain" "-DVCPKG_TARGET_TRIPLET=$triplet" `
    '-DVCPKG_MANIFEST_MODE=OFF' "-DCMAKE_EXE_LINKER_FLAGS=-L$installed/lib"
if ($LASTEXITCODE -ne 0) { throw 'cmake configure failed' }

Write-Host "==> Building cava (full SDL/GLEW visualizer)"
cmake --build $buildDir --parallel
if ($LASTEXITCODE -ne 0) { throw 'cmake build failed' }

$cavaExe = Join-Path $buildDir 'cava.exe'
if (-not (Test-Path $cavaExe)) { throw 'cava.exe was not produced' }

Write-Host "==> Renaming to yara.exe"
$yaraExe = Join-Path $buildDir 'yara.exe'
Copy-Item $cavaExe $yaraExe -Force

$gccDir = Split-Path (Get-Command gcc -ErrorAction Stop).Source

$stageFiles = New-Object System.Collections.Generic.List[string]
$stageFiles.Add($yaraExe)
foreach ($dll in 'libfftw3.dll', 'glew32.dll', 'SDL2.dll') {
    $stageFiles.Add((Join-Path $installed "bin\$dll"))
}
$stageFiles.Add((Join-Path $gccDir 'libwinpthread-1.dll'))

Write-Host "==> Staging runtime files"
$stageDir = Join-Path $root 'publish'
New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
Get-ChildItem $stageDir -File | Remove-Item -Force
foreach ($f in $stageFiles) {
    if (-not (Test-Path $f)) { throw "Missing runtime file: $f" }
    Copy-Item $f (Join-Path $stageDir (Split-Path $f -Leaf)) -Force
}

Write-Host "==> Compressing embedded files for the installer"
$embedDir = Join-Path $root 'yara-installer\embed'
New-Item -ItemType Directory -Path $embedDir -Force | Out-Null
Get-ChildItem $embedDir -Filter '*.gz' | Remove-Item -Force
foreach ($f in $stageFiles) {
    $name = Split-Path $f -Leaf
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
Get-ChildItem $stageDir | ForEach-Object { Write-Host "  " $_.FullName }
Write-Host "  " (Join-Path $root 'yara-installer\publish\yara-installer.exe')
