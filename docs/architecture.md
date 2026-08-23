# e3a Architecture

## Overview

```
Browser ──► Azure Static Web Apps (React SPA, free tier)
   │
   ├──► Cloudflare ──► api.<domain>   ──► Azure Functions (.NET 10 isolated, consumption)
   │                                        ├── HTTP triggers (auth, catalog, composer, publish)
   │                                        └── Queue trigger (publish pipeline)
   │                                              │
   └──► Cloudflare Worker ──► Blob Storage        ├── Blob: public/ (zips + marketplace.json)
        (/marketplace.json, /z/*)                 ├── Blob: drafts/, snapshots/ (private)
                                                  └── Azure SQL Basic (catalog index)
```

## Principles

- **Reads never hit the API.** `marketplace.json` and plugin zips are served from Blob via Cloudflare cache; the Functions app only handles auth, composition, and publishing. This makes consumption-plan cold starts irrelevant for consumers.
- **Versions are immutable.** A published zip at `/z/{name}/{semver}.zip` never changes; sha256 recorded in the DB and in the marketplace entry.
- **Public-only in v0.1.** No private items, no multi-tenancy. Login (GitHub OAuth) is required only to create/publish/vote.
- **Limits**: 50 engineers + 10 teams per creator; 50 versions per item; version created only on explicit Publish.

## Publish pipeline (queue worker)

dequeue → mark Building → assemble plugin tree (draft assets or member snapshots) →
validate structure → security scan (fail = Rejected + per-file report) →
deterministic zip + sha256 → upload to Blob → snapshot assets (engineers) →
mark Published → regenerate marketplace.json → purge Cloudflare cache.

Poison queue after 3 retries → version marked Failed.

## Backend style

Vertical slices without MediatR (cold-start budget): `E3a.Functions` hosts thin
triggers; `E3a.Core` holds `Features/<Area>/<UseCase>/{Command,Handler,Validator}`,
`Domain/`, and `Infrastructure/` (EF Core, Blob, Queue, GitHub client, scanner,
plugin builder, marketplace generator, Cloudflare purger).
