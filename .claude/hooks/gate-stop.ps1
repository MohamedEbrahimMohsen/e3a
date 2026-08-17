#Requires -Version 5.1
<#
.SYNOPSIS
  e3a Stop gate. Refuses to let the session end while a feature loop is mid-flight.

.DESCRIPTION
  Reads every .e3a/state/*.json. If any has phase != "done" and halt != true,
  exits 2 to block the stop and tells Claude which phase to resume.

  halt:true is the escape valve. Without it this hook is an infinite loop, so it
  is checked first and always wins.

  Exit codes: 0 = allow stop, 2 = block stop (message on stderr goes to Claude).
  Any unexpected failure exits 0 - a broken gate must not wedge the session.

  Two Windows/PowerShell 5.1 constraints this file must keep honouring:
    - ASCII only. A BOM-less .ps1 is read as ANSI, so a stray UTF-8 character
      breaks the parse.
    - Never use $obj.PSObject.Properties.Name under Set-StrictMode: on an object
      with no properties (ConvertFrom-Json '{}') it throws. Use Get-Prop below.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Prop {
    param($Object, [string] $Name, $Default = $null)
    if ($null -eq $Object) { return $Default }
    $p = $Object.PSObject.Properties[$Name]
    if ($null -eq $p) { return $Default }
    return $p.Value
}

try {
    # Claude Code passes the hook payload as JSON on stdin.
    $raw = [Console]::In.ReadToEnd()
    $payload = $null
    if ($raw -and $raw.Trim()) {
        try { $payload = $raw | ConvertFrom-Json } catch { $payload = $null }
    }

    # Never re-block a stop we already blocked; that is the infinite-loop shape.
    if (Get-Prop $payload 'stop_hook_active' $false) { exit 0 }

    $projectDir = $env:CLAUDE_PROJECT_DIR
    if (-not $projectDir) { $projectDir = (Get-Location).Path }
    $stateDir = Join-Path $projectDir '.e3a\state'
    if (-not (Test-Path $stateDir)) { exit 0 }

    $stateFiles = @(Get-ChildItem -Path $stateDir -Filter '*.json' -File -ErrorAction SilentlyContinue)
    if ($stateFiles.Count -eq 0) { exit 0 }

    $blockers = @()
    foreach ($f in $stateFiles) {
        $s = $null
        try { $s = Get-Content -Raw -Path $f.FullName | ConvertFrom-Json } catch { continue }
        if (-not $s) { continue }

        if (Get-Prop $s 'halt' $false) { continue }   # human is driving, allow

        $phase = Get-Prop $s 'phase'
        if (-not $phase)       { continue }           # malformed, do not wedge
        if ($phase -eq 'done') { continue }

        $blockers += [pscustomobject]@{
            Slug  = Get-Prop $s 'slug'  $f.BaseName
            Phase = $phase
            Round = Get-Prop $s 'round' '?'
        }
    }

    if ($blockers.Count -eq 0) { exit 0 }

    $lines = @('e3a: the delivery loop is still in flight, so this turn cannot end yet.', '')
    foreach ($b in $blockers) {
        $lines += ("  - {0}: phase '{1}' (round {2})" -f $b.Slug, $b.Phase, $b.Round)
    }
    $lines += ''
    $lines += 'Resume at that phase in .claude/skills/e3a-feature/SKILL.md.'
    $lines += 'If the loop genuinely cannot proceed, set "halt": true with a "halt_reason" in the state file and tell the user why.'

    [Console]::Error.WriteLine(($lines -join "`n"))
    exit 2
}
catch {
    # A failing gate must never trap the user in a session they cannot exit.
    [Console]::Error.WriteLine("e3a gate-stop: non-fatal hook error: $($_.Exception.Message)")
    exit 0
}
