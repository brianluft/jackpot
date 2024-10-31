param (
    [Parameter(Mandatory=$true)] [string] $CertificatePath,
    [Parameter(Mandatory=$true)] [string] $CertificatePassword
)

$ErrorPreference = 'Stop'

if (-not (Test-Path $CertificatePath)) {
	throw "Certificate not found!"
}

# Windows SDK 10.0.*.*
$windowsSdkBaseDir = "C:\Program Files (x86)\Windows Kits\10\Redist"
$windowsSdkVersion = `
    Get-ChildItem -Path $windowsSdkBaseDir | 
    Where-Object { $_.Name -match '^10\.0\.\d+\.\d+$' } | 
    Sort-Object Name -Descending | 
    Select-Object -First 1 -ExpandProperty Name
Write-Output "Windows SDK version: $windowsSdkVersion"
$windowsSdkPlatform = $Platform
if ($Platform -eq 'arm64') {
    $windowsSdkPlatform = 'arm'
}
$windowsSdkDir = Join-Path -Path $windowsSdkBaseDir -ChildPath "$windowsSdkVersion\ucrt\DLLs\$windowsSdkPlatform"
if (-not (Test-Path $windowsSdkDir)) {
    throw "Windows 10 SDK $windowsSdkVersion not found!"
}

$signtool = "C:\Program Files (x86)\Windows Kits\10\bin\$windowsSdkVersion\x64\signtool.exe"
if (-not (Test-Path $signtool)) {
	throw "Signtool not found!"
}

$msixFilePath = "$root\publish\jackpot.msix"

& $signtool sign /f "$CertificatePath" /p "$CertificatePassword" /tr http://timestamp.sectigo.com /fd SHA256 /td SHA256 "$msixFilePath" | Write-Output
if ($LastExitCode -ne 0) {
    throw "Failed to sign MSIX file."
}
