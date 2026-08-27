# e3a Security Scan

Every publish runs a rule-based scan over all text files in the composed plugin
**before** anything becomes downloadable. Context: independent research (Snyk
ToxicSkills, 2026) found prompt injection in ~36% of publicly shared agent skills.

> Since the upload-only pivot (2026-08-23), uploads may include **hook scripts**
> (`.sh`, `.ps1`, `.js`, `.py`) that Claude Code executes automatically. These get a
> dedicated **script tier**: all markdown-tier rules plus script-specific rules
> (network calls to non-allowlisted hosts, credential-file reads, encoded payload
> execution, process/persistence manipulation), a lower Block threshold, and a
> mandatory "includes N auto-running hooks" warning on the catalog detail page even
> when the scan passes. The sanitize step strips `settings.local.json`, `.env*`,
> memory/session files before anything reaches storage.

## Rule categories

1. **Credential exfiltration** — reads of `~/.ssh`, `~/.aws/credentials`, `.env`,
   `.npmrc`, `.netrc` combined with network sends; `env`/`printenv` piped to
   `curl`/`wget`/`Invoke-WebRequest`; posts to webhook.site, pastebin, ngrok,
   requestbin, raw-IP URLs.
2. **Encoded payloads** — `base64 -d` piped to a shell; `Invoke-Expression` on
   decoded/downloaded strings; base64 walls > 500 chars inside instructions.
3. **Dangerous commands** — `rm -rf /`(and `~`), fork bombs, `mkfs`,
   `reg delete HKLM`, disabling Defender, `curl … | sh` one-liners.
4. **Instruction injection** — "ignore previous instructions" + exfiltration verbs;
   instructions to hide actions from the user; instructions to read files outside
   the workspace and transmit them.
5. **Hygiene blocks** — executables/binaries, oversize files, absolute-path zip
   entries, symlinks.

## Outcomes

- **Block** → version `Rejected`; creator sees per-file, per-line reasons.
- **Warn** → published, flagged for review (ambiguous single hits).
- Every rule has corpus fixtures in `E3a.Core.Tests`.
- The report button on every catalog item is the human backstop; reported items can
  be pulled from `marketplace.json` immediately.

This scan is pattern-based and intentionally pragmatic — it raises the cost of casual
abuse; it does not claim to catch a determined adversary. Defense in depth comes from
immutable versioning (sha256), attribution, and fast takedown.
