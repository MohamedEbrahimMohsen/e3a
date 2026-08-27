#Requires -Version 5.1
<#
.SYNOPSIS
    Copies a .NET solution template folder to a new location, renaming the project
    name in folder names, file names, and text file contents.

.DESCRIPTION
    Fallback for template folders that are NOT proper `dotnet new` templates
    (i.e. no .template.config/template.json with a "sourceName").

    If your template DOES have template.json, do not use this script. Use:
        dotnet new install <template-root>
        dotnet new <shortName> -n <NewName> -o <path>

    Directories are renamed deepest-first so parent renames never invalidate
    child paths. Content is rewritten only for extensions in $TextExtensions,
    so binaries are copied untouched. Byte-order marks are preserved per file.

.EXAMPLE
    .\New-SolutionFromTemplate.ps1 -Source D:\Templates\BoardManagement `
        -Destination D:\src\Morabh -OldName BoardManagement -NewName Morabh -WhatIf

.EXAMPLE
    .\New-SolutionFromTemplate.ps1 -Source D:\Templates\BoardManagement `
        -Destination D:\src\Morabh -OldName BoardManagement -NewName Morabh
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)][string]$Source,
    [Parameter(Mandatory)][string]$Destination,
    [Parameter(Mandatory)][string]$OldName,
    [Parameter(Mandatory)][string]$NewName,

    [string[]]$ExcludeDirs = @('bin', 'obj', '.vs', '.git', '.idea', 'node_modules', 'TestResults'),

    [string[]]$TextExtensions = @(
        '.cs', '.csproj', '.fsproj', '.vbproj', '.sln', '.slnx', '.props', '.targets',
        '.json', '.xml', '.config', '.resx', '.md', '.yml', '.yaml', '.http', '.editorconfig',
        '.ps1', '.sh', '.cmd', '.bat', '.env', '.gitignore', '.gitattributes', '.dockerignore',
        '.razor', '.cshtml', '.ts', '.js', '.sql'
    ),

    [switch]$Force
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Source -PathType Container)) {
    throw "Source not found or not a directory: $Source"
}
if ($OldName -eq $NewName) {
    throw "OldName and NewName are identical: '$OldName'"
}
if ($NewName -notmatch '^[A-Za-z_][A-Za-z0-9_.]*$') {
    throw "NewName must be a valid .NET identifier/namespace segment. Got: '$NewName'"
}

$templateJson = Get-ChildItem -LiteralPath $Source -Recurse -Filter 'template.json' -File -ErrorAction SilentlyContinue |
    Where-Object { $_.DirectoryName -like '*.template.config' } | Select-Object -First 1
if ($templateJson) {
    Write-Warning "Source contains $($templateJson.FullName)."
    Write-Warning "This is a real 'dotnet new' template. Prefer: dotnet new install `"$Source`" ; dotnet new <shortName> -n $NewName"
}

if (Test-Path -LiteralPath $Destination) {
    $existing = Get-ChildItem -LiteralPath $Destination -Force -ErrorAction SilentlyContinue
    if ($existing -and -not $Force) {
        throw "Destination is not empty: $Destination  (pass -Force to overwrite)"
    }
}

# ---- 1. copy tree, skipping excluded directories -----------------------------

Write-Host "Copying $Source -> $Destination" -ForegroundColor Cyan

$sourceRoot = (Resolve-Path -LiteralPath $Source).Path.TrimEnd('\')
$excludePattern = ($ExcludeDirs | ForEach-Object { [regex]::Escape($_) }) -join '|'
$excludeRegex = "(^|\\)($excludePattern)(\\|$)"

$filesToCopy = Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Force |
    Where-Object { $_.FullName.Substring($sourceRoot.Length) -notmatch $excludeRegex }

foreach ($file in $filesToCopy) {
    $relative = $file.FullName.Substring($sourceRoot.Length).TrimStart('\')
    $target = Join-Path $Destination $relative
    $targetDir = Split-Path -Parent $target
    if ($PSCmdlet.ShouldProcess($target, 'Copy')) {
        if (-not (Test-Path -LiteralPath $targetDir)) {
            New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        }
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
    }
}

if ($WhatIfPreference) {
    Write-Host "`n-WhatIf: copy simulated. Rename/replace steps below are estimated from the SOURCE tree." -ForegroundColor Yellow
    $scanRoot = $sourceRoot
} else {
    $scanRoot = (Resolve-Path -LiteralPath $Destination).Path.TrimEnd('\')
}

# ---- 2. rename directories, deepest first ------------------------------------

$dirs = Get-ChildItem -LiteralPath $scanRoot -Recurse -Directory -Force |
    Where-Object { $_.Name -like "*$OldName*" } |
    Sort-Object { $_.FullName.Split('\').Count } -Descending

foreach ($dir in $dirs) {
    $newDirName = $dir.Name -replace [regex]::Escape($OldName), $NewName
    $newPath = Join-Path $dir.Parent.FullName $newDirName
    if ($PSCmdlet.ShouldProcess($dir.FullName, "Rename directory -> $newDirName")) {
        Rename-Item -LiteralPath $dir.FullName -NewName $newDirName -Force
    }
    Write-Verbose "dir : $($dir.FullName) -> $newPath"
}

# ---- 3. rename files ---------------------------------------------------------

$files = Get-ChildItem -LiteralPath $scanRoot -Recurse -File -Force |
    Where-Object { $_.Name -like "*$OldName*" }

foreach ($file in $files) {
    $newFileName = $file.Name -replace [regex]::Escape($OldName), $NewName
    if ($PSCmdlet.ShouldProcess($file.FullName, "Rename file -> $newFileName")) {
        Rename-Item -LiteralPath $file.FullName -NewName $newFileName -Force
    }
    Write-Verbose "file: $($file.FullName) -> $newFileName"
}

# ---- 4. replace content in text files ----------------------------------------

$oldLower = $OldName.ToLowerInvariant()
$newLower = $NewName.ToLowerInvariant()
$changed = 0

$textFiles = Get-ChildItem -LiteralPath $scanRoot -Recurse -File -Force |
    Where-Object { $TextExtensions -contains $_.Extension -or $TextExtensions -contains $_.Name }

foreach ($file in $textFiles) {
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    if ($bytes.Length -eq 0) { continue }

    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    if ($hasBom) { $text = $text.Substring(1) }

    $updated = $text -replace [regex]::Escape($OldName), $NewName
    if ($oldLower -cne $OldName) {
        $updated = $updated -creplace [regex]::Escape($oldLower), $newLower
    } else {
        $updated = $updated -creplace [regex]::Escape($oldLower), $newLower
    }

    if ($updated -ceq $text) { continue }

    if ($PSCmdlet.ShouldProcess($file.FullName, 'Replace content')) {
        $encoding = New-Object System.Text.UTF8Encoding($hasBom)
        [System.IO.File]::WriteAllText($file.FullName, $updated, $encoding)
    }
    $changed++
    Write-Verbose "edit: $($file.FullName)"
}

# ---- 5. report ---------------------------------------------------------------

Write-Host ""
Write-Host "Directories renamed : $($dirs.Count)"
Write-Host "Files renamed       : $($files.Count)"
Write-Host "Files edited        : $changed"

$leftovers = @()
if (-not $WhatIfPreference) {
    $leftovers = Get-ChildItem -LiteralPath $scanRoot -Recurse -File -Force |
        Where-Object { $TextExtensions -contains $_.Extension } |
        Select-String -Pattern ([regex]::Escape($OldName)) -SimpleMatch -List -ErrorAction SilentlyContinue
}

if ($leftovers) {
    Write-Warning "'$OldName' still present in $($leftovers.Count) file(s):"
    $leftovers | ForEach-Object { Write-Warning "  $($_.Path):$($_.LineNumber)" }
} elseif (-not $WhatIfPreference) {
    Write-Host "No occurrences of '$OldName' remain in text files." -ForegroundColor Green
}

Write-Host ""
Write-Host "Next:" -ForegroundColor Cyan
Write-Host "  cd `"$Destination`""
Write-Host "  dotnet build"
Write-Host "  git init && git add -A && git commit -m `"chore: scaffold $NewName from template`""
