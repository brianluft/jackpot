param
(
	[Parameter(Mandatory = $true)] [string] $Arch
)

$ErrorPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

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

function Get-FfmpegX64
{
	$url = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl-shared.zip"
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
	$dstDir = "$buildDir\ffmpeg\"
	[System.IO.Directory]::CreateDirectory($dstDir) | Out-Null
	[System.IO.Compression.ZipFile]::ExtractToDirectory($zipFilePath, $dstDir)

	# Delete stuff we don't need
	Remove-Item -Path "$dstDir\ffplay.exe" -Force
}

function Copy-MiscFiles
{
	Copy-Item -Path "$root\COPYING" -Destination "$buildDir\COPYING"
	Copy-Item -Path "$root\NOTICE" -Destination "$buildDir\NOTICE"
	
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
	param
	(
		[Parameter(Mandatory = $true)] [string] $AppxManifestSuffix
	)

	Write-Host "Creating MSIX package ($AppxManifestSuffix)."

	$dir = "$bundleDir\$AppxManifestSuffix"
	[System.IO.Directory]::CreateDirectory($dir) | Out-Null

	$msixFilePath = "$dir\Jackpot-$Arch.msix"
	if (Test-Path $msixFilePath) { Remove-Item -Path $msixFilePath -Force }

	$manifest = [System.IO.File]::ReadAllText("$srcDir\AppxManifest-$AppxManifestSuffix.xml")
	$manifest = $manifest.Replace('(ARCH)', $Arch)
	[System.IO.File]::WriteAllText("$buildDir\AppxManifest.xml", $manifest)

	Write-Host "`n--- Start: MakeAppx pack ($AppxManifestSuffix) ---"
	& "$makeappx" pack /d "$buildDir" /p "$msixFilePath"
	if ($LastExitCode -ne 0) {
		throw "Failed to create MSIX package ($AppxManifestSuffix)."
	}
	Write-Host "--- End: MakeAppx pack ($AppxManifestSuffix) ---`n"
}

Copy-MiscFiles
Publish-App
if ($Arch -eq "x64") {
	Get-FfmpegX64
} elseif ($Arch -eq "arm64") {
	Get-FfmpegArm64
}
New-Msix -AppxManifestSuffix "Store"
New-Msix -AppxManifestSuffix "Sideload"

Write-Host "=== End $Arch ==="
