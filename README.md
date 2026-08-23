# e3a — Engineer as an Agent

**Compose AI engineers. Build teams. Install them into Claude Code in one command.**

e3a is a free, open catalog where developers compose *AI engineers* — bundles of skills and a persona packaged as a Claude Code plugin — and assemble them into *teams* with pinned, reproducible versions. Anyone can browse and install without an account:

```
/plugin marketplace add https://<domain>/marketplace.json
/plugin install e3a-<creator>-<engineer>@e3a
```

## Why

AI dev environments are the most valuable, least portable asset an engineer has. e3a makes a working setup shareable: publish your engineer once, and your whole team — or the whole community — can run the identical environment.

## Architecture

| Piece | Tech |
|---|---|
| Web | React 18 + TypeScript + Vite on Azure Static Web Apps |
| API | .NET 10 isolated Azure Functions (consumption) |
| Content | Azure Blob Storage — immutable versioned plugin zips + `marketplace.json` |
| Data | Azure SQL + EF Core (catalog index only) |
| Async | Azure Storage Queue (publish pipeline: validate → security-scan → build → publish) |
| Edge | Cloudflare — CDN caching, purge-on-publish, rate limiting |
| Auth | GitHub OAuth (only needed to create/publish) |

Runs on ~$5/month. Every published version is security-scanned and content-addressed (sha256).

## Repo layout

```
web/     React SPA
api/     E3a.slnx — Functions host, Core (vertical slices), tests
infra/   Bicep (all Azure resources)
seed/    Flagship engineers published at launch
docs/    ALL project documents: constitution, implementation plan,
         architecture, plugin spec, security scan spec, design prompt
```

All engineering rules live in [docs/constitution.md](docs/constitution.md) — read it before contributing.

## Status

v0.1 in active development. Workflow builder planned for v0.2.
