[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

$legacyFiles = @(
    'src/OrderCreation.Api/Infrastructure/Infrastructure.cs'
)

$removed = 0
foreach ($relativePath in $legacyFiles) {
    $fullPath = Join-Path $repositoryRoot $relativePath
    if (Test-Path $fullPath) {
        Remove-Item -Force $fullPath
        Write-Host "Removed legacy file: $relativePath" -ForegroundColor Yellow
        $removed++
    }
}

if ($removed -eq 0) {
    Write-Host 'No legacy files found.' -ForegroundColor Green
} else {
    Write-Host "Removed $removed legacy file(s)." -ForegroundColor Green
}

Write-Host 'Run: git add -A' -ForegroundColor Cyan
