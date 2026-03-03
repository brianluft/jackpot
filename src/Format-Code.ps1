$ErrorPreference = 'Stop'

$root = Split-Path -Path $PSScriptRoot -Parent
$src = "$root\src"
Push-Location $src
try
{
    dotnet tool run csharpier format .
}
finally
{
    Pop-Location
}

