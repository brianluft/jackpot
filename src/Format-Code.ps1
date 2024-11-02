$ErrorPreference = 'Stop'

$root = Split-Path -Path $PSScriptRoot -Parent
$src = "$root\src"
dotnet tool run dotnet-csharpier "$src"
