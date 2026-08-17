#Requires -Version 5.1
<#
.SYNOPSIS
  e3a commit gate. Denies `git commit` while the active feature's latest review
  says changes_requested.

.DESCRIPTION
  PreToolUse(Bash) hook. This is the structural half of "return to Opus in case
  of any issues" - without it, the handoff holds only while the model chooses to
  comply.

  Allows the commit when:
    - the command is not a git commit
    - there is no in-flight e3a feature (normal work in the repo)
    - the feature has no review yet (round 1 has not been reviewed)
    - the newest review for that feature says "pass"
    - the feature is halted (the human is now driving)

  Emits a PreToolUse JSON decision on stdout. Any unexpected failure allows the
  call - a broken gate must not block ordinary git use.

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

function Write-Decision {
    param([string] $Decision, [string] $Reason)
    $out = @{
        hookSpecificOutput = @{
            hookEventName            = 'PreToolUse'
            permissionDecision       = $Decision
            permissionDecisionReason = $Reason
        }
    }
    [Console]::Out.WriteLine(($out | ConvertTo-Json -Depth 6 -Compress))
}

try {
    $raw = [Console]::In.ReadToEnd()
    if (-not $raw -or -not $raw.Trim()) { exit 0 }
    $payload = $null
    try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }

    $toolName = Get-Prop $payload 'tool_name' ''
    if ($toolName -ne 'Bash' -and $toolName -ne 'PowerShell') { exit 0 }

    $cmd = [string](Get-Prop (Get-Prop $payload 'tool_input') 'command' '')
    if (-not $cmd) { exit 0 }

    # Match `git commit` anywhere in a compound command, but not `git commit-tree`
    # and not a message that merely mentions the words.
    if ($cmd -notmatch '(?<![\w-])git\s+(?:-[^\s]+\s+|--[^\s]+(?:=\S+)?\s+)*commit(?![\w-])') { exit 0 }

    $projectDir = $env:CLAUDE_PROJECT_DIR
    if (-not $projectDir) { $projectDir = (Get-Location).Path }

    $stateDir = Join-Path $projectDir '.e3a\state'
    if (-not (Test-Path $stateDir)) { exit 0 }

    $reviewDir = Join-Path $projectDir '.e3a\reviews'
    if (-not (Test-Path $reviewDir)) { exit 0 }

    foreach ($f in @(Get-ChildItem -Path $stateDir -Filter '*.json' -File -ErrorAction SilentlyContinue)) {
        $s = $null
        try { $s = Get-Content -Raw -Path $f.FullName | ConvertFrom-Json } catch { continue }
        if (-not $s) { continue }

        if (Get-Prop $s 'halt' $false) { continue }   # human is driving, allow

        $phase = Get-Prop $s 'phase'
        if (-not $phase -or $phase -eq 'done') { continue }

        $slug = Get-Prop $s 'slug' $f.BaseName

        # Newest review for this slug, by round number rather than mtime.
        $latest = Get-ChildItem -Path $reviewDir -Filter "$slug-r*.json" -File -ErrorAction SilentlyContinue |
            Sort-Object { [int]($_.BaseName -replace '^.*-r', '') } -Descending |
            Select-Object -First 1
        if (-not $latest) { continue }   # not reviewed yet - allow

        $review = $null
        try { $review = Get-Content -Raw -Path $latest.FullName | ConvertFrom-Json } catch { continue }
        if (-not $review) { continue }

        if ((Get-Prop $review 'verdict') -ne 'changes_requested') { continue }

        $blocking = @(Get-Prop $review 'blocking' @())

        $detail = ($blocking | Select-Object -First 3 | ForEach-Object {
            "  - {0} ({1}): {2}" -f (Get-Prop $_ 'id' '?'), (Get-Prop $_ 'file' '?'), (Get-Prop $_ 'claim' '')
        }) -join "`n"

        $reason = @(
            "e3a: commit blocked. The latest review for '$slug' ($($latest.Name)) is changes_requested with $($blocking.Count) blocking item(s)."
            $detail
            ''
            'Go back to the implement phase, fix these, re-run the build and tests, and get a passing review. Do not commit around this gate.'
        ) -join "`n"

        Write-Decision -Decision 'deny' -Reason $reason
        exit 0
    }

    exit 0
}
catch {
    [Console]::Error.WriteLine("e3a gate-commit: non-fatal hook error: $($_.Exception.Message)")
    exit 0
}
