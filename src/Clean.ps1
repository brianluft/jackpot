$ErrorPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$root = Split-Path -Path $PSScriptRoot -Parent

function Remove-Directory($path) {
    if (Test-Path $path) {
        [System.IO.Directory]::Delete($path, $true) | Out-Null
    }
}

Remove-Directory "$root\x64"
Remove-Directory "$root\arm64"
Remove-Directory "$root\bundle"
Remove-Directory "$root\publish"
Remove-Directory "$root\src\J.App\bin"
Remove-Directory "$root\src\J.App\obj"
Remove-Directory "$root\src\J.Base\bin"
Remove-Directory "$root\src\J.Base\obj"
Remove-Directory "$root\src\J.Core\bin"
Remove-Directory "$root\src\J.Core\obj"
Remove-Directory "$root\src\J.Server\bin"
Remove-Directory "$root\src\J.Server\obj"
Remove-Directory "$root\src\J.Test\bin"
Remove-Directory "$root\src\J.Test\obj"
