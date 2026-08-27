#requires -Version 5.1
<#
.SYNOPSIS
  Packs every Core library to a local folder feed.
.EXAMPLE
  ./pack.ps1 -Output D:\Personal\Packages
#>
param(
    [string]$Output = "$PSScriptRoot/../../Packages",
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $Output | Out-Null

Write-Host "Packing Core libraries to $Output" -ForegroundColor Cyan
dotnet pack "$PSScriptRoot/Core.slnx" -c $Configuration -o $Output

Write-Host ""
Write-Host "Done. Each pack carries a unique local version suffix, so consumers" -ForegroundColor Green
Write-Host "pick up your changes instead of a cached build." -ForegroundColor Green
Write-Host ""
Write-Host "In the consuming solution, set the version in Directory.Packages.props" -ForegroundColor Cyan
Write-Host "to the one just produced, or use a floating version while iterating." -ForegroundColor Cyan
