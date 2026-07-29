$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "ENTestTerminal\ENTestTerminal.csproj"
$publishDir = Join-Path $root "ENTestTerminal\publish"
$installerDir = $PSScriptRoot
$outDir = Join-Path $installerDir "bin"

if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
    Write-Host "Installing WiX Toolset CLI (dotnet tool)..."
    dotnet tool install --global wix --version 5.0.2
}

$installedExtensions = wix extension list --global
if ($installedExtensions -notmatch "WixToolset.UI.wixext") {
    Write-Host "Installing WiX UI extension..."
    wix extension add WixToolset.UI.wixext/5.0.2 --global
}

Write-Host "Publishing self-contained build..."
dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Write-Host "Building MSI..."
wix build (Join-Path $installerDir "Product.wxs") -ext WixToolset.UI.wixext -arch x64 -o (Join-Path $outDir "ENterm.msi")

Write-Host "Done: $outDir\ENterm.msi"
