# Claude Design Prompt — e3a v0.1

Design a complete dark-mode web app UI for **e3a — "Engineer as an Agent"**: a free community catalog where developers compose **AI engineers** (bundles of skills + a persona, packaged as a Claude Code plugin) and **teams of engineers**, publish them publicly with versions, and anyone installs them into Claude Code by copying one command. Tone: developer-tool, credible, npm/Vercel-grade polish — not playful, not enterprise-stiff.

## Design system

- **Background** `#0b0b0f` (near-black); **surface/cards** `#16161a`; **elevated surface** `#1d1d23`; **borders** `#26262c` (1px, subtle).
- **Primary**: electric violet `#8b5cf6` with gradient accent `#bd34fe → #646cff` for hero moments only. **Accent**: cyan `#22d3ee` (used sparingly: active states, sparklines). **Success** `#34d399`, **danger** `#f87171`, **warning** `#fbbf24`.
- **Text**: `#f5f5f7` primary, `#a1a1aa` secondary, `#6b6b74` muted.
- **Fonts**: Inter for UI; JetBrains Mono for install commands, code, slugs, versions.
- **Shape language**: rounded-xl cards (12-16px radius), pill-shaped buttons and filter chips with small leading icons, soft 1px borders instead of shadows; generous spacing; max-width ~1200px centered.
- Buttons: primary = violet filled; secondary = dark surface with border; ghost for tertiary. Copy-to-clipboard buttons everywhere commands appear (with a "copied ✓" state).

## Signature components (reused across pages)

1. **Install command block** — dark mono block with two lines (`/plugin marketplace add https://e3a.dev/marketplace.json`, `/plugin install e3a-mmohsen@e3a`), each with its own copy button; compact variant for cards.
2. **Engineer card** — avatar-style identicon or emoji tile, engineer name, `@author` with tiny GitHub avatar, one-line description, tag chips, version badge (mono, e.g. `v3.0.0`), install count ("1,204 installs"; below 50 installs show plain number only — NO chart; above threshold show a small 12-week cyan sparkline npm-style), "Team" variant with stacked member avatars.
3. **Version history row** — version badge, published date, size, sha256 (truncated, mono), and a "pin this version" copy command that expands the pinned-marketplace command (`/plugin marketplace add https://e3a.dev/m/{plugin}/{version}/marketplace.json`).
4. **Scan report panel** — rejection state for publishing: red-tinted card listing per-file, per-line findings (rule id chip like `EXF001`, severity, file path in mono, excerpt); warnings variant in amber.
5. **Limits meter** — slim progress bar + label ("12 / 50 engineers"), violet fill, red near cap.
6. **Plugin structure preview** — read-only file tree (`.claude-plugin/plugin.json`, `agents/…`, `skills/…/SKILL.md`, `commands/…`) in a bordered mono panel.

## Pages (design each as its own artboard, desktop-first 1440px)

1. **Home** — sticky top nav (e3a logo, Catalog, How it works, GitHub icon, "Sign in with GitHub" button). Hero: headline "Hire an AI engineering team in one command", subline, the install command block, gradient glow behind. Below: stats strip (engineers · teams · installs), "Featured engineers" card row, "Featured teams" row, footer.
2. **Catalog** — search input + filter row (Engineers/Teams segmented toggle, tag chips, sort dropdown: Newest / Most installed), responsive **card grid** (3-col) of engineer/team cards, pagination.
3. **Engineer detail** — header (tile, name, @author, version badge, install count, Report link), install command block, two-column body: left = description (markdown) + plugin structure preview; right sidebar = metadata (published date, last updated, size, tags, sha256) + **version history** list with pin-version rows. When the engineer includes hooks: an amber **warning banner** under the install block — "⚠ Includes N hooks that run automatically" — expandable to list each hook's trigger event and command. An optional third install step appears when a CLAUDE.md snippet exists: "add this line to your project's CLAUDE.md" with its own copy button.
4. **Team detail** — same layout; adds "Members" section: rows of member engineers each showing pinned version badge and link; note explaining teams are immutable snapshots; the hooks warning banner aggregates member hooks ("⚠ N hooks from 2 members"), each row attributed to its member.
5. **Creator profile** — avatar, @login, GitHub link, join date; tabs "Engineers / Teams"; card grid of their published items.
6. **How it works** — 3 numbered steps with icons (Browse → Copy command → Claude Code has your team), short plugin-spec section, "Every publish is scanned" trust section with the scan categories, FAQ accordion.
7. **My workspace** (authenticated) — page header with "New Engineer" / "New Team" primary buttons and two limits meters; **table/records layout** (not cards): name, type, status chip (Draft/Published/Rejected), version, installs, updated date, row actions (Edit, Publish, View).
8. **Engineer composer (upload-only)** — two-pane editor. Left pane = form (name, slug preview in mono, description, tag input). Right pane, three stacked stages: (a) a large **dropzone** ("Drop your .claude folder or .zip · max 20 MB" with a folder icon) shown until an upload exists; (b) after upload, the **import manifest** — three grouped sections with counts and status icons: *Imported* (green ✓ rows: "8 skills", "2 agents", "3 commands", "2 hooks ⚠ auto-running — listed with their trigger events", "MCP servers"), *Converted* (cyan ⓘ rows: "CLAUDE.md + rules/ → house-rules skill" with a "view snippet" expander), *Skipped* (muted rows with reasons: "permissions — no plugin equivalent", "settings.local.json — stripped"); every row expandable to show the file list; (c) live **plugin structure preview** (mono file tree) reflecting the manifest. A "Replace upload" ghost button resets to the dropzone. Sticky footer bar: "Save draft" (secondary) + "Publish" (primary) + last-saved timestamp.
9. **Team composer** — left: team form (name, description, tags); right: member picker (search published engineers, add with version pin dropdown), draggable ordered member list, structure preview; same sticky footer.
10. **Publish status states** (design as one artboard with three stacked states) — inline panel shown after Publish: (a) in-progress: stepper Queued → Building → Published with spinner; (b) success: green check, version badge, ready-to-copy install command; (c) rejected: the scan report panel with per-file findings and a "Fix and republish" button.

Also include: empty states (empty catalog search, empty workspace with friendly "compose your first engineer" illustration), the report-item modal (reason dropdown + details textarea), and a 404.

Mobile: show one representative mobile artboard (Catalog) — single-column cards, filters collapse into a sheet.

## Interactivity — the canvas must be one navigable prototype, not separate static pages

- **Full navigation**: every nav link, button, card, and text link routes to its correct page (logo → Home; cards → detail pages; @author → profile; Sign in → My workspace; New Engineer/Team → composers; Publish → publish-status states). No dead ends — if it looks clickable, it does something.
- **Hover states on everything interactive** with smooth ~150ms transitions: buttons shift fill/brightness, cards lift with a violet border glow, links shift color, table rows highlight.
- **Working micro-interactions**: copy buttons flip to "Copied ✓"; the Engineers/Teams segmented toggle switches visible cards; tag filters toggle; tabs switch content; FAQ accordion expands; "pin this version" rows expand to reveal the pinned command; modals open/close; the publish stepper animates Queued → Building → Published with the rejected state reachable.
- **App-like transitions** between pages (instant or subtle fade), and empty states reachable through real interactions (e.g. a search with no results).
