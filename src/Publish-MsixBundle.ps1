$ErrorPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$root = Split-Path -Path $PSScriptRoot -Parent

& $root\src\Clean.ps1

function Mirror-Directory
{
    param (
        [string] $Source,
        [string] $Destination
    )

    Write-Host "Mirroring $Source to $Destination."

    $sourceDir = Get-Item -Path $source
    [System.IO.Directory]::CreateDirectory($Destination) | Out-Null
    $destinationDir = Get-Item -Path $destination

    $sourceFiles = Get-ChildItem -Path $source -Recurse
    foreach ($sourceFile in $sourceFiles) {
        $destinationFile = $sourceFile.FullName.Replace($sourceDir.FullName, $destinationDir.FullName)
        $destinationFileDir = Split-Path -Path $destinationFile -Parent
        if (!(Test-Path $destinationFileDir)) {
            [System.IO.Directory]::CreateDirectory($destinationFileDir) | Out-Null
        }
        Copy-Item -Path $sourceFile.FullName -Destination $destinationFile -Force
    }
}

Mirror-Directory "$root\src" "$root\x64"
Mirror-Directory "$root\src" "$root\arm64"

$bundleDir = "$root\bundle"
if (Test-Path $bundleDir) {
	[System.IO.Directory]::Delete($bundleDir, $true) | Out-Null
}

# Windows SDK
$windowsSdkBaseDir = "C:\Program Files (x86)\Windows Kits\10\Redist"
$windowsSdkVersion = `
    Get-ChildItem -Path $windowsSdkBaseDir | 
    Where-Object { $_.Name -match '^10\.0\.\d+\.\d+$' } | 
    Sort-Object Name -Descending | 
    Select-Object -First 1 -ExpandProperty Name

$makeappx = "C:\Program Files (x86)\Windows Kits\10\bin\$windowsSdkVersion\x64\makeappx.exe"
if (Test-Path $makeappx) {
    Write-Output "MakeAppx: $makeappx"
} else {
	throw "MakeAppx not found!"
}

# Make arch-specific msix installers
$x64job = Start-Job -Name "x64" -ScriptBlock {
    param($root)
    & "$root\x64\Publish-Msix.ps1" -Arch "x64"
} -ArgumentList $root

$arm64job = Start-Job -Name "arm64" -ScriptBlock {
    param($root)
    & "$root\arm64\Publish-Msix.ps1" -Arch "arm64"
} -ArgumentList $root

Receive-Job -Job $x64job -Wait -AutoRemoveJob
Receive-Job -Job $arm64job -Wait -AutoRemoveJob

if (-not (Test-Path "$bundleDir\Jackpot-x64.msix")) {
    throw "Failed to create x64 MSIX."
}

if (-not (Test-Path "$bundleDir\Jackpot-arm64.msix")) {
    throw "Failed to create arm64 MSIX."
}

# Make bundle
$msixBundleFilePath = "$root\publish\Jackpot.msixbundle"
if (Test-Path $msixBundleFilePath) { Remove-Item -Path $msixBundleFilePath -Force }

& "$makeappx" bundle /p "$msixBundleFilePath" /d "$bundleDir"
if ($LastExitCode -ne 0) {
	throw "Failed to create MSIX bundle."
}
