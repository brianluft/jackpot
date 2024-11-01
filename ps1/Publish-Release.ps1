$ErrorPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

$root = Split-Path -Path $PSScriptRoot -Parent
Write-Host "Root: $root"

$publishDir = "$root\publish"
if (Test-Path $publishDir) { [System.IO.Directory]::Delete($publishDir, $true) | Out-Null }

$buildDir = "$root\publish\build"
[System.IO.Directory]::CreateDirectory($buildDir) | Out-Null

$downloadsDir = "$root\downloads"
[System.IO.Directory]::CreateDirectory($downloadsDir) | Out-Null

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

function Publish-App
{
	param
	(
		[Parameter(Mandatory = $true)] [string] $Arch
	)

	Write-Host "Publishing $Arch."
	$dir = "$buildDir\$Arch"
	[System.IO.Directory]::CreateDirectory($dir) | Out-Null
	dotnet publish "$root/src/J.App/J.App.csproj" --output "$dir" --self-contained --runtime "win-$Arch" --configuration Release --verbosity quiet
	Remove-Item -Path "$dir\*.pdb" -Force
}

function Publish-Launcher
{
	dotnet publish "$root/src/J.Launcher/J.Launcher.csproj" --output "$buildDir" --self-contained --runtime "win-x64" --configuration Release --verbosity quiet
	Remove-Item -Path "$buildDir\*.pdb" -Force
}

function Get-FfmpegX64
{
	$url = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
	$zipFilePath = "$downloadsDir\ffmpeg-x64.zip"
	if (-not (Test-Path $zipFilePath))
	{
		Write-Host "Downloading ffmpeg/x64."
		& curl.exe -Lso "$zipFilePath" "$url" | Out-Host
		if ($LastExitCode -ne 0) {
			throw "Failed to download ffmpeg/x64."
		}
	}

	Write-Host "Extracting ffmpeg/x64."
	$dstDir = "$buildDir\x64\ffmpeg\"
	[System.IO.Directory]::CreateDirectory($dstDir) | Out-Null
	[System.IO.Compression.ZipFile]::ExtractToDirectory($zipFilePath, $dstDir)

	# Get the only subdirectory of $dstDir.
	$ffmpegDir = Get-ChildItem -Path $dstDir -Directory | Select-Object -First 1

	# Move everything in $ffmpegDir\bin\ to $dstDir.
	$ffmpegBinDir = "$($ffmpegDir.FullName)\bin"
	Move-Item -Path "$ffmpegBinDir\*" -Destination $dstDir -Force

	Remove-Item -Path $ffmpegDir.FullName -Recurse -Force
}

function Get-FfmpegArm64
{
	$url = "https://github.com/dvhh/ffmpeg-wos-arm64-build/releases/download/main/ffmpeg-wos-arm64.zip"
	$zipFilePath = "$downloadsDir\ffmpeg-arm64.zip"
	if (-not (Test-Path $zipFilePath))
	{
		Write-Host "Downloading ffmpeg/arm64."
		& curl.exe -Lso "$zipFilePath" "$url" | Out-Host
		if ($LastExitCode -ne 0) {
			throw "Failed to download ffmpeg/arm64."
		}
	}

	Write-Host "Extracting ffmpeg/arm64."
	$dstDir = "$buildDir\arm64\ffmpeg\"
	[System.IO.Directory]::CreateDirectory($dstDir) | Out-Null
	[System.IO.Compression.ZipFile]::ExtractToDirectory($zipFilePath, $dstDir)
}

function Get-Vlc
{
	$url = "https://get.videolan.org/vlc/last/win64/"
	$htmlFilePath = "$downloadsDir\vlc.html"
	if (-not (Test-Path $htmlFilePath))
	{
		Write-Host "Finding current release of vlc/x64."
		& curl.exe -Lso "$htmlFilePath" "$url" | Out-Host
		if ($LastExitCode -ne 0) {
			throw "Failed to download vlc.html."
		}
	}

	# Read the html file, find the first line that contains ".zip"
	$line = Select-String -Path $htmlFilePath -Pattern "\.zip" | Select-Object -First 1

	# $line is like: <a href="vlc-3.0.21-win64.zip">vlc-3.0.21-win64.zip</a>
	# Parse out the .zip filename between the quotes.
	$zipFilename = $line -replace '.*href="([^"]+)".*', '$1'

	$url = "https://get.videolan.org/vlc/last/win64/$zipFilename"
	$zipFilePath = "$downloadsDir\vlc.zip"
	if (-not (Test-Path $zipFilePath))
	{
		Write-Host "Downloading vlc/x64."
		& curl.exe -Lso "$zipFilePath" "$url" | Out-Host
		if ($LastExitCode -ne 0) {
			throw "Failed to download vlc/x64."
		}
	}

	Write-Host "Extracting vlc/x64."
	$dstDir = "$buildDir\vlc\"
	[System.IO.Directory]::CreateDirectory($dstDir) | Out-Null
	[System.IO.Compression.ZipFile]::ExtractToDirectory($zipFilePath, $dstDir)

	# Get the only subdirectory of $dstDir.
	$subdir = Get-ChildItem -Path $dstDir -Directory | Select-Object -First 1

	# Confirm $subdir is the directory with vlc.exe in it.
	if (-not (Test-Path "$($subdir.FullName)\vlc.exe")) {
		throw "Failed to find vlc.exe in extracted directory."
	}

	# Move everything in $subdir to $dstDir.
	Move-Item -Path "$($subdir.FullName)\*" -Destination $dstDir -Force

	# Delete $subdir.
	Remove-Item -Path $subdir.FullName -Recurse -Force
}

function Copy-MiscFiles
{
	Copy-Item -Path "$root\COPYING" -Destination "$buildDir\COPYING"
	Copy-Item -Path "$root\NOTICE" -Destination "$buildDir\NOTICE"
	Copy-Item -Path "$root\src\AppxManifest.xml" -Destination "$buildDir\AppxManifest.xml"

	[System.IO.Directory]::CreateDirectory("$buildDir\assets") | Out-Null
	Copy-Item -Path "$root\src\J.App\Resources\App.png" -Destination "$buildDir\assets\App.png"
	Copy-Item -Path "$root\src\J.App\Resources\App150x150.png" -Destination "$buildDir\assets\App150x150.png"
	Copy-Item -Path "$root\src\J.App\Resources\App44x44.png" -Destination "$buildDir\assets\App44x44.png"
}

function New-Msix
{
	Write-Host "Creating MSIX package."
	$msixFilePath = "$root\publish\Jackpot.msix"
	if (Test-Path $msixFilePath) { Remove-Item -Path $msixFilePath -Force }
	& "$makeappx" pack /d "$buildDir" /p "$msixFilePath"
	if ($LastExitCode -ne 0) {
		throw "Failed to create MSIX package."
	}
}

function Add-TemporarySignature
{
	Write-Host "Signing MSIX package with self-signed certificate."

	$cert = New-SelfSignedCertificate `
		-Type Custom `
		-Subject "CN=Brian Luft" `
		-KeyUsage DigitalSignature `
		-KeyAlgorithm RSA `
		-KeyLength 2048 `
		-CertStoreLocation "Cert:\CurrentUser\My" `
		-NotAfter (Get-Date).AddDays(7) `
		-FriendlyName "Temporary CI Build Certificate" `
		-TextExtension @("2.5.29.19={text}CA=false&pathlength=0")

	try
	{
		# Export to .pfx (public key + private key).
		$password = ConvertTo-SecureString -String "password" -Force -AsPlainText
		Export-PfxCertificate -Cert $cert -FilePath "$publishDir\private.pfx" -Password $password | Out-Null

		# Export to .cer (public key only).
		Export-Certificate -Cert $cert -FilePath "$publishDir\ci-certificate.cer" | Out-Null

		# Sign the .msix.
		& "$root\ps1\Add-MsixSignature.ps1" -CertificatePath "$publishDir\private.pfx" -CertificatePassword "password"
	}
	finally
	{
		# Destroy the private certificate.
		Remove-Item -Path $cert.PSPath

		if (Test-Path "$publishDir\private.pfx") {
			Remove-Item -Path "$publishDir\private.pfx" -Force
		}
	}
}

Copy-MiscFiles
Publish-Launcher
Publish-App -Arch "x64"
Publish-App -Arch "arm64"
Get-FfmpegX64
Get-FfmpegArm64
Get-Vlc
New-Msix
Add-TemporarySignature
