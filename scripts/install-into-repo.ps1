#Requires -Version 5.1
<#
.SYNOPSIS
  Install the e3a delivery pipeline into a target repository.

.DESCRIPTION
  D:\Personal\e3a is the source of truth. This projects the pipeline into a repo
  that actually has code to build.

  Copies (or links) .claude/agents, .claude/skills and .claude/hooks, merges the
  hooks and permissions blocks into the target's .claude/settings.json, and
  creates a fresh .e3a/ with its own conventions ledger. The ledger is
  deliberately NOT copied - conventions are per-codebase, and inheriting another
  repo's rules is how you end up rejecting good feedback with an irrelevant ID.

.PARAMETER TargetRepo
  Path to the repository to install into.

.PARAMETER Link
  Create directory junctions instead of copying, so edits in e3a take effect
  everywhere immediately. Do not use this for a repo you intend to commit the
  .claude directory into.

.PARAMETER IncludeCodeRabbit
  Also copy .coderabbit.yaml if the target does not already have one.

.PARAMETER Force
  Overwrite existing agents/skills/hooks in the target.

.EXAMPLE
  .\install-into-repo.ps1 -TargetRepo D:\Work\Morabh.Apis
.EXAMPLE
  .\install-into-repo.ps1 -TargetRepo D:\Work\Morabh.Apis -Link -IncludeCodeRabbit
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $TargetRepo,
    [switch] $Link,
    [switch] $IncludeCodeRabbit,
    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$SourceRoot = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path -LiteralPath $TargetRepo -PathType Container)) {
    throw "Target repo not found: $TargetRepo"
}
$TargetRepo = (Resolve-Path -LiteralPath $TargetRepo).Path
if ($TargetRepo -eq $SourceRoot) { throw "Target is the e3a source itself; nothing to do." }
if (-not (Test-Path (Join-Path $TargetRepo '.git'))) {
    Write-Warning "$TargetRepo is not a git repository. The loop needs git and gh to work."
}

function Install-Dir {
    param([string] $Relative)

    $src = Join-Path $SourceRoot $Relative
    $dst = Join-Path $TargetRepo $Relative
    if (-not (Test-Path -LiteralPath $src)) { throw "Missing source directory: $src" }

    if (Test-Path -LiteralPath $dst) {
        if (-not $Force) {
            Write-Host "  skip   $Relative (exists; pass -Force to overwrite)" -ForegroundColor DarkYellow
            return
        }
        # Remove-Item on a junction deletes the link, not the target.
        Remove-Item -LiteralPath $dst -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dst) | Out-Null

    if ($Link) {
        New-Item -ItemType Junction -Path $dst -Target $src | Out-Null
        Write-Host "  link   $Relative" -ForegroundColor Green
    }
    else {
        Copy-Item -LiteralPath $src -Destination $dst -Recurse -Force
        Write-Host "  copy   $Relative" -ForegroundColor Green
    }
}

function Merge-Settings {
    $srcPath = Join-Path $SourceRoot '.claude\settings.json'
    $dstPath = Join-Path $TargetRepo '.claude\settings.json'
    $src = Get-Content -Raw -LiteralPath $srcPath | ConvertFrom-Json

    if (Test-Path -LiteralPath $dstPath) {
        $dst = Get-Content -Raw -LiteralPath $dstPath | ConvertFrom-Json
        Copy-Item -LiteralPath $dstPath -Destination "$dstPath.e3a-backup" -Force
        Write-Host "  backup .claude/settings.json -> settings.json.e3a-backup" -ForegroundColor DarkGray
    }
    else {
        $dst = [pscustomobject]@{}
    }

    # --- hooks: append e3a's entries, replacing any previous e3a entries ---
    $dstHooks = if ($dst.PSObject.Properties.Name -contains 'hooks' -and $dst.hooks) { $dst.hooks } else { [pscustomobject]@{} }

    foreach ($evt in $src.hooks.PSObject.Properties.Name) {
        $incoming = @($src.hooks.$evt)
        $existing = @()
        if ($dstHooks.PSObject.Properties.Name -contains $evt -and $dstHooks.$evt) {
            # Drop prior e3a entries so re-running is idempotent.
            $existing = @($dstHooks.$evt | Where-Object {
                ($_ | ConvertTo-Json -Depth 10 -Compress) -notmatch 'e3a[\\/]?\.claude|gate-stop\.ps1|gate-commit\.ps1'
            })
        }
        $merged = @($existing) + @($incoming)
        if ($dstHooks.PSObject.Properties.Name -contains $evt) { $dstHooks.PSObject.Properties.Remove($evt) }
        $dstHooks | Add-Member -NotePropertyName $evt -NotePropertyValue $merged
    }
    if ($dst.PSObject.Properties.Name -contains 'hooks') { $dst.PSObject.Properties.Remove('hooks') }
    $dst | Add-Member -NotePropertyName 'hooks' -NotePropertyValue $dstHooks

    # --- permissions.allow: union ---
    $dstPerms = if ($dst.PSObject.Properties.Name -contains 'permissions' -and $dst.permissions) { $dst.permissions } else { [pscustomobject]@{} }
    $existingAllow = @()
    if ($dstPerms.PSObject.Properties.Name -contains 'allow' -and $dstPerms.allow) { $existingAllow = @($dstPerms.allow) }
    $union = @($existingAllow + @($src.permissions.allow) | Select-Object -Unique)
    if ($dstPerms.PSObject.Properties.Name -contains 'allow') { $dstPerms.PSObject.Properties.Remove('allow') }
    $dstPerms | Add-Member -NotePropertyName 'allow' -NotePropertyValue $union
    if ($dst.PSObject.Properties.Name -contains 'permissions') { $dst.PSObject.Properties.Remove('permissions') }
    $dst | Add-Member -NotePropertyName 'permissions' -NotePropertyValue $dstPerms

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dstPath) | Out-Null
    ($dst | ConvertTo-Json -Depth 20) | Out-File -LiteralPath $dstPath -Encoding utf8
    Write-Host "  merge  .claude/settings.json (hooks + permissions)" -ForegroundColor Green
}

Write-Host "e3a -> $TargetRepo" -ForegroundColor Cyan
Write-Host ""

Install-Dir '.claude\agents'
Install-Dir '.claude\skills'
Install-Dir '.claude\hooks'
Merge-Settings

# --- .e3a working directories, with a FRESH ledger ---
foreach ($d in @('.e3a\state', '.e3a\specs', '.e3a\reviews', '.e3a\triage')) {
    New-Item -ItemType Directory -Force -Path (Join-Path $TargetRepo $d) | Out-Null
}

$ledger = Join-Path $TargetRepo '.e3a\conventions.md'
if (-not (Test-Path -LiteralPath $ledger)) {
    $header = Get-Content -LiteralPath (Join-Path $SourceRoot '.e3a\conventions.md') -TotalCount 24
    $header += ''
    $header += '<!-- Fresh ledger for this repository. Add rules as escalations are resolved. -->'
    $header | Out-File -LiteralPath $ledger -Encoding utf8
    Write-Host "  new    .e3a/conventions.md (empty ledger)" -ForegroundColor Green
}
else {
    Write-Host "  keep   .e3a/conventions.md (already exists)" -ForegroundColor DarkYellow
}

if ($IncludeCodeRabbit) {
    $crDst = Join-Path $TargetRepo '.coderabbit.yaml'
    if ((Test-Path -LiteralPath $crDst) -and -not $Force) {
        Write-Host "  skip   .coderabbit.yaml (exists)" -ForegroundColor DarkYellow
    }
    else {
        Copy-Item -LiteralPath (Join-Path $SourceRoot '.coderabbit.yaml') -Destination $crDst -Force
        Write-Host "  copy   .coderabbit.yaml" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "Done. Next:" -ForegroundColor Cyan
Write-Host "  1. Add .e3a/state/ to .gitignore (loop state is local, not shared)."
Write-Host "  2. Check gh auth:  gh auth status"
Write-Host "  3. Open Claude Code in $TargetRepo on Opus 5, then:  /e3a-feature ""<description>"""
