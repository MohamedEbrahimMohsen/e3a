# Core Libraries

Sixteen shared libraries. Nothing project-specific ever goes in here — if it is
not useful to every project, it belongs in that project.

`Core.Cache` and `Core.Queues` are stubs. Do not build against them; use the
cloud storage client or the platform's own queue bindings instead.

## Consumed two ways

**Project references (today).** A generated solution points `CoreRepoPath` at
this folder and references the projects directly. No feed, no packing, F12
steps into the source.

**Packages (once published).** `pack.ps1` packs every library to a folder feed:

```powershell
./pack.ps1 -Output D:\Personal\Packages
```

Consumers add that folder to `nuget.config` and switch to `-p:CoreMode=package`.
When a hosted feed exists, only the `nuget.config` URL changes.

## The one thing that will waste your afternoon

A rebuilt package with an **unchanged version** is served from the global
package cache, not from the folder feed. Your changes appear not to apply and
nothing looks wrong.

`Directory.Build.props` handles this: local packs get a timestamp version
suffix, so every pack is a new version and the cache can never serve a stale
one. CI (with `CI=true`) packs clean versions.

## Debugging

`DebugType=embedded` with `EmbedAllSources`, so symbols and sources travel
inside the package. Stepping into Core works in package mode exactly as it does
in project mode — the usual reason people avoid packages does not apply here.
