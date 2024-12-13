$ErrorPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$root = Split-Path -Path $PSScriptRoot -Parent

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

# Restore for both architectures
dotnet restore "$root/src/J.App/J.App.csproj" --runtime "win-x64" --verbosity quiet
dotnet restore "$root/src/J.App/J.App.csproj" --runtime "win-arm64" --verbosity quiet

# Make arch-specific msix installers
$x64job = Start-Job -Name "x64" -ScriptBlock {
    param($root)
    & "$root\src\Publish-Msix.ps1" -Arch "x64"
} -ArgumentList $root

$arm64job = Start-Job -Name "arm64" -ScriptBlock {
    param($root)
    & "$root\src\Publish-Msix.ps1" -Arch "arm64"
} -ArgumentList $root

Receive-Job -Job $x64job -Wait -AutoRemoveJob
Receive-Job -Job $arm64job -Wait -AutoRemoveJob

# Make bundle
$msixBundleFilePath = "$root\publish\Jackpot.msixbundle"
if (Test-Path $msixBundleFilePath) { Remove-Item -Path $msixBundleFilePath -Force }

& "$makeappx" bundle /p "$msixBundleFilePath" /d "$bundleDir"
if ($LastExitCode -ne 0) {
	throw "Failed to create MSIX bundle."
}
