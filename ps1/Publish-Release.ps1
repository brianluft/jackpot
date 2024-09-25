$ErrorPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

$root = Split-Path -Path $PSScriptRoot -Parent

$publishDir = "$root\publish"
if (Test-Path $publishDir) { [System.IO.Directory]::Delete($publishDir, $true) | Out-Null }

function Publish-PlatformRelease
{
	param
	(
		[Parameter(Mandatory = $true)]
		[string] $Arch
	)

	Write-Host "Publishing $Arch."
	$dir = "$root\publish\$Arch"
	[System.IO.Directory]::CreateDirectory($dir) | Out-Null
	dotnet publish "$root/src/J.App/J.App.csproj" --output "$dir" --self-contained --runtime "win-$Arch" --configuration Release

	Write-Host "Zipping $Arch."
	$zip = "$root\publish\jackpot-$Arch.zip"
	[System.IO.Compression.ZipFile]::CreateFromDirectory($dir, $zip) | Out-Null
}

Publish-PlatformRelease -Arch "x64"
Publish-PlatformRelease -Arch "arm64"
