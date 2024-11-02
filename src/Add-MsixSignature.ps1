param (
    [Parameter(Mandatory=$true)] [string] $CertificatePath,
    [Parameter(Mandatory=$true)] [string] $CertificatePassword
)

$ErrorPreference = 'Stop'

if (-not (Test-Path $CertificatePath)) {
	throw "Certificate not found!"
}

$root = Split-Path -Path $PSScriptRoot -Parent

# Windows SDK
$windowsSdkBaseDir = "C:\Program Files (x86)\Windows Kits\10\Redist"
$windowsSdkVersion = `
    Get-ChildItem -Path $windowsSdkBaseDir | 
    Where-Object { $_.Name -match '^10\.0\.\d+\.\d+$' } | 
    Sort-Object Name -Descending | 
    Select-Object -First 1 -ExpandProperty Name

$signtool = "C:\Program Files (x86)\Windows Kits\10\bin\$windowsSdkVersion\x64\signtool.exe"
if (Test-Path $signtool) {
    Write-Output "Signtool: $signtool"
} else {
	throw "Signtool not found!"
}

$msixFilePath = "$root\publish\jackpot.msix"

Write-Host "`n--- Start: SignTool sign ---"
& $signtool sign /f "$CertificatePath" /p "$CertificatePassword" /tr http://timestamp.sectigo.com /fd SHA256 /td SHA256 "$msixFilePath" | Write-Output
if ($LastExitCode -ne 0) {
    throw "Failed to sign MSIX file."
}
Write-Host "--- End: SignTool sign ---`n"
