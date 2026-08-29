# e3a Plugin Spec

> Revised 2026-08-23: engineer creation is **upload-only** — a creator uploads their whole
> `.claude` folder (as folder or zip, optionally including the repo-root `CLAUDE.md`), and
> e3a normalizes it into a Claude Code plugin. The earlier skill-picking composer is
> deferred; this reflects reality: real environments are heterogeneous (skills, agents,
> hooks, rules/, conventions/, settings) and the working setup itself is the artifact.

## Naming

Plugin name: `e3a-{slug}` for engineers and `e3a-team-{slug}` for teams — the creator types the slug when creating the item; it is unique within its own table, editable only while the item has never been published, and permanently frozen afterwards. Engineer and team slugs are **separate namespaces**: because the `team-` segment namespaces every team plugin, a team slug may repeat an engineer slug without any collision. GitHub login is no longer part of the plugin name; attribution lives in the `author` field.

## Ingestion: the `.claude` → plugin mapping

Verified against official Claude Code plugin docs. Every upload produces an **import
manifest** shown to the creator before publish — three columns, nothing silently dropped:

**Imported (1:1):**

| From upload | To plugin |
|---|---|
| `skills/` | `skills/` |
| `agents/` | `agents/` |
| `commands/` | `commands/` |
| hooks (settings.json `hooks` section + script files) | `hooks/hooks.json` + scripts (identical format) |
| `.mcp.json` | `.mcp.json` |
| `.lsp.json` | `.lsp.json` |
| `output-styles/` | `output-styles/` |
| monitors / `bin/` executables / themes | `monitors/` / `bin/` / `themes/` |

**Converted:**

- `CLAUDE.md` + freeform `rules/`, `conventions/`, `docs/` folders → a generated
  **`house-rules` skill** with a strong trigger description (plugins cannot inject
  always-on instructions), plus a **CLAUDE.md snippet** surfaced on the detail page as an
  optional install step ("add this line to your project's CLAUDE.md") to restore
  always-on semantics.

**Skipped (listed in manifest with reasons):**

- `settings.json` permissions / env vars / model selection / statusline — no plugin
  equivalent; permissions are shown on the detail page as recommended settings.
- Path-scoped rules' auto-scoping (content still converts to skills; scoping is lost).
- `settings.local.json`, auto-memory, session state — machine-local; auto-stripped by the
  sanitize step (never uploaded to storage).

## Hooks policy (v0.1)

Hooks ARE imported — they map format-identically — but under the strictest handling:

1. Hook scripts get the **script-tier security scan** (see docs/security-scan.md), not
   just the markdown rules. Any Block finding rejects the publish.
2. The catalog detail page shows a prominent warning: "⚠ includes N hooks that run
   automatically" with the hook events and commands listed for inspection.
3. The import manifest lists every hook with its trigger event before the creator publishes.

## Engineer plugin layout (generated)

```
.claude-plugin/plugin.json     # name, version, description, author { name, url } — see marketplace.json
agents/…                       # uploaded agents (a default persona is generated only if none exist)
skills/…                       # uploaded skills + generated house-rules skill
commands/…                     # uploaded commands
hooks/hooks.json (+ scripts)   # when present in the upload
.mcp.json / .lsp.json / output-styles/ / monitors/ / bin/ / themes/   # when present
```

## Team plugin layout

One plugin bundling member engineers at **pinned versions** (snapshots taken at engineer
publish time — teams are immutable until the team owner republishes). The ordered roster is
**frozen into the team version row** at publish time, so a member engineer publishing a newer
version cannot alter an already-published team. Republishing on its own does **not** adopt newer
member versions: a member with no explicit `pinnedVersionId` falls back to its existing pin, so the
owner must first send the new `pinnedVersionId` to `PUT /api/teams/{teamId}/members` and then
republish. Prompting the owner automatically when a member has a newer version is deferred to the
`team-compile-merge` slice. Merge rules:

**Merged today (the `teams` slice):**

- `agents/`, `commands/`: merged. On a file-name collision **every** colliding member's file is
  prefixed `{member-slug}--`, not just the later one, so the output does not depend on member
  order. Non-colliding names stay unprefixed.
- `skills/`: merged as `skills/{member-slug}--{skill-slug}/` (double-hyphen namespacing),
  applied **unconditionally**, whether or not the skill name collides.

**Deferred to the `team-compile-merge` slice — not merged today.** A member's hooks, `.mcp.json`,
`.lsp.json`, `output-styles/`, `monitors/`, `bin/` and `themes/` are dropped from the team plugin
in the current build; only the three roots above are carried. The target rules are:

- hooks: concatenated into one `hooks/hooks.json`; the team page carries the combined
  hook warning listing which member each hook came from.
- `.mcp.json` / `.lsp.json`: merged by server name; name collisions are prefixed with the
  member slug.
- Settings-derived items are never merged (they were never imported).

## marketplace.json

Regenerated in full from the DB on every publish; written atomically to Blob. Claude Code
requires a wrapper around the entries:

```json
{
  "name": "e3a",
  "owner": { "name": "e3a", "url": "https://<domain>" },
  "plugins": [
    {
      "name": "e3a-mmohsen",
      "description": "…",
      "version": "3.0.0",
      "author": { "name": "mmohsen", "url": "https://<domain>/e/mmohsen" },
      "keywords": ["backend", "dotnet"],
      "source": {
        "source": "archive",
        "url": "https://<domain>/z/e3a-mmohsen/3.0.0.zip",
        "sha256": "<hex>"
      }
    }
  ]
}
```

Published **teams are listed alongside engineers**, under their `e3a-team-{slug}` names and with
`author.url` pointing at `https://<domain>/t/{slug}`; the combined list is ordered ordinally by
plugin name. Only latest published versions are listed; unlisted engineers drop out of the root
document while their zips and pinned marketplaces keep resolving, so existing installs never break.
Older zips remain at immutable URLs, and each version also gets a pinned single-plugin
marketplace at `/m/{plugin}/{version}/marketplace.json` — identical wrapper, one-element
`plugins` array. `archive` sources are used because relative paths do not resolve for
URL-added marketplaces.

Attribution before GitHub OAuth: `author.name` is the creator's Identity `UserName`, falling
back to the engineer or team slug when it is empty, and `author.url` is the e3a catalog page
`https://<domain>/e/{slug}` for an engineer and `https://<domain>/t/{slug}` for a team. The GitHub login and GitHub profile URL arrive with the OAuth slice.

## Upload constraints

Zip or folder upload; sanitize step strips `settings.local.json`, `.env*`, memory/session
files, and OS junk before storage. Caps: 20 MB per upload, 400 files; text +
png/jpg/svg + hook scripts (`.sh`, `.ps1`, `.js`, `.py`) only; path-traversal-safe
extraction; no symlinks.
