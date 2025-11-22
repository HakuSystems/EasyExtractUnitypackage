<#
.SYNOPSIS
    Packages Rider/Avalonia publish outputs for Windows, Linux, and macOS into distributable archives.

.DESCRIPTION
    Run this script from the publish root (e.g. bin\Release\net9.0\publish) after Rider finishes publishing.
    It creates platform-specific archives under an artifacts directory and emits SHA256 hashes for verification.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$AppName = "EasyExtractCrossPlatform",

    [string]$PublishRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step
{
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function New-CleanDirectory
{
    param([string]$Path)
    if (Test-Path $Path)
    {
        Remove-Item -Path $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path | Out-Null
}

function Invoke-WindowsPackage
{
    param(
        [string]$SourceDir,
        [string]$DestinationDir,
        [string]$AppName,
        [string]$Version
    )

    $zipPath = Join-Path $DestinationDir "$AppName-$Version-win-x64.zip"
    Write-Step "Packaging Windows build -> $zipPath"
    Compress-Archive -Path (Join-Path $SourceDir '*') -DestinationPath $zipPath -Force
    return $zipPath
}

function Invoke-LinuxPackage
{
    param(
        [string]$SourceDir,
        [string]$DestinationDir,
        [string]$AppName,
        [string]$Version
    )

    $tarPath = Join-Path $DestinationDir "$AppName-$Version-linux-x64.tar.gz"
    Write-Step "Packaging Linux build -> $tarPath"
    $tarExe = Get-Command tar -ErrorAction SilentlyContinue
    if (-not $tarExe)
    {
        throw "tar is required but was not found in PATH."
    }
    & $tarExe.Path -czf $tarPath -C $SourceDir .
    return $tarPath
}

function Invoke-MacPackage
{
    param(
        [string]$SourceDir,
        [string]$DestinationDir,
        [string]$AppName,
        [string]$Version
    )

    $workDir = Join-Path $DestinationDir "_macOS_work"
    New-CleanDirectory -Path $workDir

    $bundlePath = Join-Path $workDir "$AppName.app"
    $contentsDir = Join-Path $bundlePath "Contents"
    $macOSDir = Join-Path $contentsDir "MacOS"
    $resourcesDir = Join-Path $contentsDir "Resources"

    foreach ($dir in @($bundlePath, $contentsDir, $macOSDir, $resourcesDir))
    {
        New-Item -ItemType Directory -Path $dir | Out-Null
    }

    Copy-Item -Path (Join-Path $SourceDir '*') -Destination $macOSDir -Recurse -Force

    $bundleId = ("com.easyextract.{0}" -f ($AppName -replace '[^A-Za-z0-9]', '')).ToLower()
    $plistPath = Join-Path $contentsDir "Info.plist"
    $plist = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleExecutable</key>
    <string>$AppName</string>
    <key>CFBundleIdentifier</key>
    <string>$bundleId</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>$AppName</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>$Version</string>
    <key>CFBundleVersion</key>
    <string>$Version</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
</dict>
</plist>
"@
    Set-Content -Path $plistPath -Value $plist -Encoding UTF8

    $zipPath = Join-Path $DestinationDir "$AppName-$Version-macOS-arm64.zip"
    Write-Step "Packaging macOS bundle -> $zipPath"
    Compress-Archive -Path $bundlePath -DestinationPath $zipPath -Force

    Remove-Item -Path $workDir -Recurse -Force
    return $zipPath
}

if ( [string]::IsNullOrWhiteSpace($PublishRoot))
{
    $publishDir = (Get-Location).ProviderPath
}
else
{
    $publishDir = (Resolve-Path $PublishRoot).Path
}

Write-Host "Publish root: $publishDir"
$artifactDir = Join-Path $publishDir "artifacts"
New-CleanDirectory -Path $artifactDir

$packages = @()
$winDir = Join-Path $publishDir "win-x64"
if (Test-Path $winDir)
{
    $packages += Invoke-WindowsPackage -SourceDir $winDir -DestinationDir $artifactDir -AppName $AppName -Version $Version
}
else
{
    Write-Warning "win-x64 folder not found; skipping Windows packaging."
}

$linuxDir = Join-Path $publishDir "linux-x64"
if (Test-Path $linuxDir)
{
    $packages += Invoke-LinuxPackage -SourceDir $linuxDir -DestinationDir $artifactDir -AppName $AppName -Version $Version
}
else
{
    Write-Warning "linux-x64 folder not found; skipping Linux packaging."
}

$macDir = Join-Path $publishDir "osx-arm64"
if (Test-Path $macDir)
{
    $packages += Invoke-MacPackage -SourceDir $macDir -DestinationDir $artifactDir -AppName $AppName -Version $Version
}
else
{
    Write-Warning "osx-arm64 folder not found; skipping macOS packaging."
}

if (-not $packages)
{
    throw "Nothing was packaged. Ensure Rider publish folders (win-x64/linux-x64/osx-arm64) exist."
}

Write-Step "Calculating SHA256 hashes"
$hashLines = foreach ($package in $packages)
{
    $hash = Get-FileHash -Path $package -Algorithm SHA256
    "{0}  {1}" -f $hash.Hash, (Split-Path $package -Leaf)
}
$hashFile = Join-Path $artifactDir "$AppName-$Version-sha256.txt"
Set-Content -Path $hashFile -Value $hashLines -Encoding ASCII
$packages += $hashFile

Write-Step "Artifacts ready"
$packages | ForEach-Object { Write-Host (Resolve-Path $_).Path }
