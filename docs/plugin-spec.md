# e3a Plugin Spec

## Naming

Plugin name: `e3a-{githublogin}-{item-slug}` — globally unique, attributable.

## Engineer plugin layout

```
.claude-plugin/plugin.json    # name, version, description, author { name: "@login", url }
agents/{engineer}.md          # persona (creator-authored or generated default)
skills/{skill-slug}/SKILL.md  # + subsidiary files
commands/{engineer}.md        # thin dispatch command for the engineer agent
```

## Team plugin layout

One plugin bundling member engineers at their **pinned versions** (snapshots taken at
engineer publish time — teams are immutable until the team owner republishes):

```
.claude-plugin/plugin.json
agents/{member-slug}.md                       # one per member
skills/{member-slug}--{skill-slug}/SKILL.md   # double-hyphen namespacing avoids collisions
commands/{teamslug}.md                        # team overview/dispatch
```

## Skill normalization

All ingestion paths (upload, GitHub link, catalog reference) converge to: folder with
`SKILL.md` at root; frontmatter `name`/`description` validated or injected; kebab-case
slug; caps: 5 MB per skill, 40 files, text + png/jpg/svg only; zip extraction is
path-traversal-safe; no symlinks, no binaries.

## marketplace.json

Regenerated in full from the DB on every publish; written atomically to Blob. Entries:

```json
{
  "name": "e3a-mohamed-dive-backend-engineer",
  "description": "…",
  "version": "3.0.0",
  "author": { "name": "@mohamed", "url": "https://github.com/mohamed" },
  "keywords": ["backend", "dotnet"],
  "source": {
    "type": "archive",
    "url": "https://<domain>/z/e3a-mohamed-dive-backend-engineer/3.0.0.zip",
    "sha256": "<hex>"
  }
}
```

Only latest published versions are listed; older zips remain at their immutable URLs.
`archive` sources are used because relative paths do not resolve for URL-added
marketplaces (per Claude Code docs).
