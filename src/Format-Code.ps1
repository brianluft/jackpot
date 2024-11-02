$ErrorPreference = 'Stop'

$root = Split-Path -Path $PSScriptRoot -Parent
$src = "$root\src"
Push-Location $src
try
{
    dotnet tool run dotnet-csharpier .
}
finally
{
    Pop-Location
}

