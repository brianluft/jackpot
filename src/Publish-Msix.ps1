param
(
	[Parameter(Mandatory = $true)] [string] $Arch
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

# --- FFmpeg mirror configuration (pinned versions) ---

# x64: https://github.com/BtbN/FFmpeg-Builds/releases
$ffmpegX64Url = "https://brianluft-mirror.com/BtbN/FFmpeg-Builds/ffmpeg-master-latest-win64-gpl-shared-20260306.zip"
$ffmpegX64Hash = "045F21FDF6411BD141CF7CDBA0659414D7ACA1ADCF66038B0B83243AD6F11501"

# arm64: https://github.com/tordona/ffmpeg-win-arm64/releases
$ffmpegArm64Url = "https://brianluft-mirror.com/tordona/ffmpeg-win-arm64/ffmpeg-master-latest-essentials-shared-win-arm64-20260307.7z"
$ffmpegArm64Hash = "CC3579C815A06C73AA771EB58CE1953C603707CFF8FBF455C63D4C1987BF2333"

# 7-Zip 9.20 (x86, runs on all architectures): https://www.7-zip.org/download.html
$7zaOldUrl = "https://brianluft-mirror.com/7zip/7za920.zip"
$7zaOldHash = "2A3AFE19C180F8373FA02FF00254D5394FEC0349F5804E0AD2F6067854FF28AC"

# 7-Zip 26.00 Extra (needed for modern 7z compression): https://www.7-zip.org/download.html
$7zaNewUrl = "https://brianluft-mirror.com/7zip/7z2600-extra.7z"
$7zaNewHash = "1CC38A9E3777CE0E4BBF84475672888A581D400633B0448FD973A7A6AA56CFDC"

# --- End configuration ---

Write-Host "=== Start $Arch ==="

$srcDir = $PSScriptRoot
$root = Split-Path -Path $PSScriptRoot -Parent

$buildDir = "$root\$Arch\build"
if (Test-Path $buildDir) {
	[System.IO.Directory]::Delete($buildDir, $true) | Out-Null
}
[System.IO.Directory]::CreateDirectory($buildDir) | Out-Null

$downloadsDir = "$root\downloads"
[System.IO.Directory]::CreateDirectory($downloadsDir) | Out-Null

$bundleDir = "$root\bundle"
[System.IO.Directory]::CreateDirectory($bundleDir) | Out-Null

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

$makepri = "C:\Program Files (x86)\Windows Kits\10\bin\$windowsSdkVersion\x64\makepri.exe"
if (Test-Path $makepri) {
    Write-Output "MakePri: $makepri"
} else {
	throw "MakePri not found!"
}

function Publish-App
{
	Write-Host "Publishing $Arch."
	& dotnet publish "$srcDir/J.App/J.App.csproj" `
		--output "$buildDir" `
		--self-contained `
		--runtime "win-$Arch" `
		--configuration Release `
		--verbosity quiet | Out-Host
	if ($LastExitCode -ne 0) {
		throw "Failed to publish $Arch."
	}
}

function Get-CachedDownload([string] $Url, [string] $ExpectedHash)
{
	$fileName = [System.IO.Path]::GetFileName([System.Uri]::new($Url).AbsolutePath)
	$filePath = "$downloadsDir\$fileName"
	if (-not (Test-Path $filePath))
	{
		Write-Host "Downloading $fileName."
		& curl.exe -Lso "$filePath" "$Url" | Out-Host
		if ($LastExitCode -ne 0) {
			throw "Failed to download $Url"
		}
	}
	$actualHash = (Get-FileHash -Path $filePath -Algorithm SHA256).Hash
	if ($actualHash -ne $ExpectedHash) {
		throw "Hash mismatch for $fileName. Expected: $ExpectedHash, Actual: $actualHash"
	}
	return $filePath
}

function Get-FfmpegX64
{
	$zipFilePath = Get-CachedDownload $ffmpegX64Url $ffmpegX64Hash

	Write-Host "Extracting ffmpeg/x64."
	$dstDir = "$buildDir\ffmpeg\"
	[System.IO.Directory]::CreateDirectory($dstDir) | Out-Null
	[System.IO.Compression.ZipFile]::ExtractToDirectory($zipFilePath, $dstDir)

	# Get the only subdirectory of $dstDir.
	$ffmpegDir = Get-ChildItem -Path $dstDir -Directory | Select-Object -First 1

	# Move everything in $ffmpegDir\bin\ to $dstDir.
	$ffmpegBinDir = "$($ffmpegDir.FullName)\bin"
	Move-Item -Path "$ffmpegBinDir\*" -Destination $dstDir -Force

	Remove-Item -Path $ffmpegDir.FullName -Recurse -Force

	# Delete stuff we don't need
	Remove-Item -Path "$dstDir\ffplay.exe" -Force
}

function Get-FfmpegArm64
{
	# Step 1: Get old 7za (distributed as a .zip we can extract natively).
	$7zaOldZip = Get-CachedDownload $7zaOldUrl $7zaOldHash
	$7zaOldDir = "$buildDir\7za-old"
	[System.IO.Directory]::CreateDirectory($7zaOldDir) | Out-Null
	[System.IO.Compression.ZipFile]::ExtractToDirectory($7zaOldZip, $7zaOldDir)
	$7zaOld = "$7zaOldDir\7za.exe"

	# Step 2: Get newer 7za (distributed as .7z, extract with old 7za). Use x64 build for simplicity.
	$7zaNew7z = Get-CachedDownload $7zaNewUrl $7zaNewHash
	$7zaNewDir = "$buildDir\7za-new"
	[System.IO.Directory]::CreateDirectory($7zaNewDir) | Out-Null
	& "$7zaOld" x "$7zaNew7z" -o"$7zaNewDir" -y | Out-Null
	if ($LastExitCode -ne 0) {
		throw "Failed to extract 7z2600-extra."
	}
	$7zaNew = "$7zaNewDir\x64\7za.exe"

	# Step 3: Extract ffmpeg arm64 .7z with the newer 7za.
	$ffmpeg7z = Get-CachedDownload $ffmpegArm64Url $ffmpegArm64Hash
	$ffmpegTempDir = "$buildDir\ffmpeg-temp"
	[System.IO.Directory]::CreateDirectory($ffmpegTempDir) | Out-Null
	Write-Host "Extracting ffmpeg/arm64."
	& "$7zaNew" x "$ffmpeg7z" -o"$ffmpegTempDir" -y | Out-Null
	if ($LastExitCode -ne 0) {
		throw "Failed to extract ffmpeg/arm64."
	}

	# Move the bin contents to the final ffmpeg directory.
	$dstDir = "$buildDir\ffmpeg\"
	[System.IO.Directory]::CreateDirectory($dstDir) | Out-Null
	$ffmpegDir = Get-ChildItem -Path $ffmpegTempDir -Directory | Select-Object -First 1
	$ffmpegBinDir = "$($ffmpegDir.FullName)\bin"
	Move-Item -Path "$ffmpegBinDir\*" -Destination $dstDir -Force

	# Clean up temp directories
	Remove-Item -Path $ffmpegTempDir -Recurse -Force
	Remove-Item -Path $7zaOldDir -Recurse -Force
	Remove-Item -Path $7zaNewDir -Recurse -Force

	# Delete stuff we don't need
	Remove-Item -Path "$dstDir\ffplay.exe" -Force
}

function Copy-MiscFiles
{
	Copy-Item -Path "$root\LICENSE" -Destination "$buildDir\LICENSE"

	[System.IO.Directory]::CreateDirectory("$buildDir\assets") | Out-Null
	Copy-Item -Path "$srcDir\J.App\Resources\App.png" -Destination "$buildDir\assets\App.png"
	Copy-Item -Path "$srcDir\J.App\Resources\App310x150.png" -Destination "$buildDir\assets\App310x150.png"
	Copy-Item -Path "$srcDir\J.App\Resources\App150x150.png" -Destination "$buildDir\assets\App150x150.png"
	Copy-Item -Path "$srcDir\J.App\Resources\App44x44.png" -Destination "$buildDir\assets\App44x44.png"
	Copy-Item -Path "$srcDir\J.App\Resources\App44x44.png" -Destination "$buildDir\assets\App44x44.targetsize-44_altform-unplated.png"

	foreach ($x in 16, 24, 32, 48, 256)
	{
		Copy-Item -Path "$srcDir\J.App\Resources\App${x}x${x}.png" -Destination "$buildDir\assets\App44x44.targetsize-${x}.png"
		Copy-Item -Path "$srcDir\J.App\Resources\App${x}x${x}.png" -Destination "$buildDir\assets\App44x44.altform-unplated_targetsize-${x}.png"
	}

	Push-Location $buildDir
	try
	{
		Write-Host "`n--- Start: MakePri createconfig ---"
		& "$makepri" createconfig /cf "priconfig.xml" /dq en-US | Out-Host
		if ($LastExitCode -ne 0) {
			throw "MakePri createconfig failed."
		}
		Write-Host "--- End: MakePri createconfig ---`n"

		Write-Host "--- Start: MakePri new ---"
		& "$makepri" new /pr "$buildDir" /cf "priconfig.xml" | Out-Host
		if ($LastExitCode -ne 0) {
			throw "MakePri new failed."
		}
		Write-Host "--- End: MakePri new ---`n"
	}
	finally
	{
		Pop-Location
	}
}

function New-Msix
{
	Write-Host "Creating MSIX package."

	$msixFilePath = "$bundleDir\Jackpot-$Arch.msix"
	if (Test-Path $msixFilePath) { Remove-Item -Path $msixFilePath -Force }

	$manifest = [System.IO.File]::ReadAllText("$srcDir\AppxManifest.xml")
	$manifest = $manifest.Replace('(ARCH)', $Arch)
	[System.IO.File]::WriteAllText("$buildDir\AppxManifest.xml", $manifest)

	Write-Host "`n--- Start: MakeAppx pack ---"
	& "$makeappx" pack /d "$buildDir" /p "$msixFilePath"
	if ($LastExitCode -ne 0) {
		throw "Failed to create MSIX package."
	}
	Write-Host "--- End: MakeAppx pack ---`n"
}

Copy-MiscFiles
Publish-App
if ($Arch -eq "x64") {
	Get-FfmpegX64
} elseif ($Arch -eq "arm64") {
	Get-FfmpegArm64
}
New-Msix

Write-Host "=== End $Arch ==="
