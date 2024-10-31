$ErrorPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

$root = Split-Path -Path $PSScriptRoot -Parent

$publishDir = "$root\publish"
if (Test-Path $publishDir) { [System.IO.Directory]::Delete($publishDir, $true) | Out-Null }

$buildDir = "$root\publish\build"
[System.IO.Directory]::CreateDirectory($buildDir) | Out-Null

$downloadsDir = "$root\downloads"
[System.IO.Directory]::CreateDirectory($downloadsDir) | Out-Null

function Publish-PlatformRelease
{
	param
	(
		[Parameter(Mandatory = $true)]
		[string] $Arch
	)

	Write-Host "Publishing $Arch."
	$dir = "$buildDir\$Arch"
	[System.IO.Directory]::CreateDirectory($dir) | Out-Null
	dotnet publish "$root/src/J.App/J.App.csproj" --output "$dir" --self-contained --runtime "win-$Arch" --configuration Release
}

function Publish-Launcher
{
	dotnet publish "$root/src/J.Launcher/J.Launcher.csproj" --output "$buildDir" --self-contained --runtime "win-x64" --configuration Release
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
	}

	Write-Host "Extracting vlc/x64."
	$dstDir = "$buildDir\vlc\"
	[System.IO.Directory]::CreateDirectory($dstDir) | Out-Null
	[System.IO.Compression.ZipFile]::ExtractToDirectory($zipFilePath, $dstDir)

	# Get the only subdirectory of $dstDir.
	$subdir = Get-ChildItem -Path $dstDir -Directory | Select-Object -First 1

	# Move everything in $subdir to $dstDir.
	Move-Item -Path "$($subdir.FullName)\*" -Destination $dstDir -Force

	# Delete $subdir.
	Remove-Item -Path $subdir.FullName -Recurse -Force
}

function Copy-LicenseFiles
{
	Copy-Item -Path "$root\COPYING" -Destination "$buildDir\COPYING"
	Copy-Item -Path "$root\NOTICE" -Destination "$buildDir\NOTICE"
}

function New-ReleaseZip
{
	Write-Host "Creating release zip."
	$zipFilePath = "$root\publish\jackpot.zip"
	if (Test-Path $zipFilePath) { Remove-Item -Path $zipFilePath -Force }
	[System.IO.Compression.ZipFile]::CreateFromDirectory($buildDir, $zipFilePath)
}

Copy-LicenseFiles
Publish-Launcher
Publish-PlatformRelease -Arch "x64"
Publish-PlatformRelease -Arch "arm64"
Get-FfmpegX64
Get-FfmpegArm64
Get-Vlc
New-ReleaseZip
