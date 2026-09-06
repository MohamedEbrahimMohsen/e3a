# Plan — Create Team (workspace team composition, frontend wiring)

## What I found before planning — corrections to the brief

The orchestrator's pointers were checked against the working tree. Three are wrong and they change the shape of this slice.

| Claim in the brief | What is actually there |
|---|---|
| "Backend team CRUD/member management exists **in part**; wire up what is missing." | **The backend is complete.** `api/E3A.Application/Teams/` has all eight use cases — `CreateTeam`, `UpdateTeam`, `SetTeamMembers`, `PublishTeam`, `DeleteTeam`, `GetTeam`, `ListMyTeams`, `CheckTeamSlugAvailability` — each with command/query + validator + handler. `api/E3A.Api/Controllers/Teams/TeamsController.cs` exposes all eight. `api/E3A.Domain/Teams/` has `Team`, `TeamMember`, `TeamMemberPin`, `TeamStatus`, `ITeamRepository`. **No backend code is written in this slice.** |
| "Repo is on `main`, clean." | True *now*. The conversation-start git snapshot was stale: it showed `feature/public-profile` with uncommitted work. That branch is merged (`3e7a76d`), tree is clean, HEAD is `main`. |
| "Whether any team UI exists at all is for you to find." | `web/src/features/composer/TeamComposerPage.tsx` exists (110 lines) and is routed at `/workspace/new-team`, but it is a **static prototype**: hard-coded crew, `memberSearchPool` fixtures from `lib/catalog.ts`, `onPublish` toasts *"Team publishing is not wired up yet"*. It makes zero API calls. |

Other verified facts the plan rests on:

- All 27 `TEAM_*` error codes already exist in `ErrorCodes.cs` **and** in both `Messages.en.resx` and `Messages.ar.resx` (27 keys each, counts match). **No new error code is needed.**
- `postman/e3a.postman_collection.json` already mirrors all eight team endpoints (List My Teams, Check Team Slug Availability, Get Team, Create Team, Update Team, Set Team Members, Publish Team, Delete Team). **No Postman change is needed.**
- Team publish is fully wired: `PublishTeamHandler` → `ItemVersion.Create` raises `PublishRequestedDomainEvent` → `PublishRequestedEventHandler` enqueues → `ProcessPublishJobHandler` branches to `build.Team.MarkPublished(versionId)` (line 96). `GetPublishStatusQueryHandler` resolves team ownership via `ITeamRepository` and returns `ItemType = "Team"`.
- Backend Teams tests exist and pass: 20 files under `api/E3A.Tests/Teams/` plus `ProcessPublishJobHandlerTeamTests`, `TeamPublishBuilderTests`, `TeamTreeAssemblerTests`, `RegenerateMarketplaceTeamTests`, `PublishedTeamCollectorTests`, `GetPublishStatusQueryTeamTests`.
- Baselines measured on `main`: `dotnet test api/E3a.slnx` → **815 passed, 0 failed**. `cd web && npm run test` → **12 files, 79 tests passed**. `npx oxlint` → **8 warnings** (4 `react(only-export-components)`, 3 `react(set-state-in-effect)`, 1 `react-hooks(exhaustive-deps)`).

So the honest gap is entirely in `web/`. This plan is a **frontend-only slice**.

## Goal

A signed-in creator can open My Workspace, see their teams alongside their engineers, click **+ New Team**, name it, search the published catalog for member engineers, add them (each pinned to the exact `ItemVersion` that engineer had published at the moment of adding), reorder the roster, save, and hit Publish — landing on the publish status page which polls to `Published` and hands back the real `/plugin install e3a-team-{slug}@e3a` command. Today none of that is possible: the team composer is a hard-coded mock that cannot save, the workspace table never shows a team, and the publish status page assumes every version is an engineer.

## Scope

**In**
- `web/src/lib/workspaceApi.ts`: `getTeam`, `createTeam`, `updateTeam`, `setTeamMembers`, `publishTeam` + their types.
- `TeamComposerPage.tsx` rewritten against the real API (create/update metadata, catalog-backed member picker, ordered roster, save, publish).
- New route `/workspace/teams/:teamId` so an existing team can be reopened.
- `WorkspacePage.tsx` lists engineers **and** teams in one table, plus a **+ New Team** button.
- `PublishStatusPage.tsx` branches on `status.itemType` so a team publish shows the team's slug, the team install command, and a working "Fix and republish" link.
- Three pure sibling modules with vitest suites (`teamMembers.ts`, `workspaceRows.ts`, `publishTarget.ts`) — the only layer the runner can reach (`conventions/react-feature.md` §7).
- `lib/errorMessages.ts` extended with the `TEAM_*` surface the UI can now provoke.
- `docs/design-prompt.md` §33 reorder wording (docs-sync; see Decision 12).

**Out**
- Any change under `api/`. No entity, handler, validator, controller, error code, resx key, migration, or Postman request. Verified unnecessary above.
- Public team detail page (`/t/:name`) — still a `lib/catalog.ts` mock.
- Team listing in the public `/catalog` Engineers/Teams toggle.
- Team delete, team slug-availability probe, unlist/relist for teams.

**Deferred**

| # | Deferred | Why |
|---|---|---|
| D1 | Version-pin dropdown (pin a member to an *older* version) | No endpoint lists an engineer's versions. `GET /api/catalog/{slug}` returns only `latestVersionId`. Building the picker requires a new backend query — its own slice. `docs/design-prompt.md` §33 already describes the dropdown as the target; per `.claude/rules/docs-sync.md` an unbuilt sub-feature is **incompleteness, not divergence**, so the doc line stays. |
| D2 | "A newer version of member X is available — re-pin?" prompt | `docs/plugin-spec.md` explicitly defers this to the `team-compile-merge` slice. |
| D3 | Drag-and-drop member reordering | A `draggable` div is unreachable by keyboard (`react-feature.md` §6), and the ordering behaviour is delivered by ↑/↓ buttons. Drag is a polish pass. |
| D4 | Public team detail page + team catalog listing + team reporting | All three need a public team catalog endpoint that does not exist. `docs/implementation-plan.md` line 63 already records "team reporting deferred until a team catalog endpoint exists". Second slice. |
| D5 | Workspace "limits meters" (`docs/design-prompt.md` §31) | Needs `MaxTeamsPerCreator`/`MaxEngineersPerCreator` on the wire; unbuilt for engineers too. Incompleteness, not this slice. |
| D6 | Team delete from the workspace row | Engineer rows have no Delete either; adding it for teams only would be asymmetric. |

**Is this one slice or two?** One. The three touched pages form a single unbroken path — composer → workspace → publish status. Dropping the workspace change strands every saved team (nothing links to `/workspace/teams/{id}`); dropping the publish-status change ends the flow on a page that says "Your engineer is live" with no install command and a "Fix and republish" link to a nonexistent engineer. The second slice is D4: the public team surface (`GET /api/catalog/teams/{slug}`, real `TeamDetailPage`, catalog Teams toggle, team reporting).

## Decisions

| # | Question | Decision | Why |
|---|---|---|---|
| 1 | Backend "exists in part" — what is missing? | Nothing. Write no C#. | All eight use cases, the controller, both resx files and the Postman folder verified present. Re-planning them would duplicate shipped code. |
| 2 | Which engineers can be team members? | Any **published** engineer, sourced from `GET /api/catalog?q=` — including other creators'. | `SetTeamMembersHandler` applies no owner filter, only `version.Status == Published`. `docs/plugin-spec.md` attributes members by slug, not owner. Restricting to own engineers would be a product change I have no mandate for. |
| 3 | How is a member's version pinned? | **Explicitly.** When the picker adds an engineer, the draft carries that engineer's `latestVersionId` from the catalog result, and `PUT /members` always sends a non-null `pinnedVersionId`. | The API accepts `null` and resolves server-side, but then what gets pinned depends on a race between the click and the save. Explicit pins mean the creator gets exactly the version the row showed. |
| 4 | Can the UI re-pin an existing member to a newer version? | No — and that is stated, not hidden. Removing a member, saving, re-adding and saving again re-pins to latest. | `TeamMemberPinResolver.ResolveVersionId` prefers the **persisted** pin over the engineer's latest, so a same-session remove+re-add still resolves the old pin. Two saves is the honest path until D1 lands. |
| 5 | Where does the team slug come from? | Derived from the display name via `toSlug(displayName)` on create; after that the server value is authoritative and read-only. `updateTeam` always sends the stored `serverSlug`. | Exactly what `EngineerComposerPage` does (`slug = serverSlug ?? toSlug(displayName)`), and `UpdateTeamHandler.ResolveSlugChangeAsync` short-circuits when the requested slug equals the current one, so a published team never trips `TEAM_SLUG_FROZEN`. |
| 6 | Separate "Save members" button? | No. **Save draft** persists metadata *then* members, in that order. **Publish** runs the same save first, then publishes. | Publish reads the persisted roster. A separate button lets a creator add three members, press Publish, and get `TEAM_EMPTY` from a screen showing three members. Save-then-publish makes that unreachable. |
| 7 | Member cap on the client? | Yes — `config.maxTeamMembers` from `VITE_MAX_TEAM_MEMBERS`, default `10`. | Mirrors the existing `maxUploadMegabytes` / `VITE_MAX_UPLOAD_MEGABYTES` precedent for a value that must agree with the server (`Teams:MaxMembersPerTeam` = 10). `react-feature.md` §2 forbids the bare literal. The server still enforces. |
| 8 | Reordering mechanism | ↑ / ↓ `<button type="button">` per row; array order becomes `SortOrder`. | `Team.ReplaceMembers` assigns `SortOrder` from list index, so array order *is* the roster order. Buttons are keyboard-reachable; a drag handle is not (D3). |
| 9 | Where does a workspace **Team** row's "View" link go? | To the team composer, never to `/t/{slug}`. | `TeamDetailPage` still renders from `lib/catalog.ts` fixtures and would 404 on a real slug — a dead end, which `react-feature.md` forbids. Revisit in D4. |
| 10 | Team install count column | `null` → renders `—`. | The `Team` entity has no `InstallCount` property (unlike `Engineer`), matching Decision 5 of `.process/public-profile/01-plan.md`. Rendering `0` would assert a fact the backend never measured. |
| 11 | Workspace row ordering | One merged list, `updatedAt` descending, ties broken by `itemId` ascending. | `docs/design-prompt.md` §31 specifies one records table with a Type column, not two tables. The `itemId` tiebreak keeps the test deterministic (`dotnet-testing.md` §8 spirit). |
| 12 | Does `/docs` need a change? | One line in `docs/design-prompt.md` §33 only. | §33 currently says "draggable ordered member list". After this slice the product answers "how do you reorder members?" with buttons — two different answers to the same question is **divergence** under `.claude/rules/docs-sync.md`. Everything else (version dropdown D1, limits meters D5, team detail D4) is code lagging the doc = **incompleteness**, which the rule says never to "fix". `implementation-plan.md` P5 scope is unchanged; `plugin-spec.md` merge rules are unchanged. |
| 13 | Delete the now-dead mock fixtures? | Yes — remove `memberSearchPool` from `lib/catalog.ts` and `CrewMember` from `lib/types.ts`. | Grep confirms `TeamComposerPage.tsx` is their only consumer. `noUnusedLocals` does not catch unused *exports*, so they would rot silently. |
| 14 | Any new frontend dependency? | None. No jsdom, no testing-library, no query library. | `react-feature.md` §7 makes the missing DOM runner a deliberate boundary. |

## Analyzer / EF-translation check (required by the brief)

**This slice adds no C#, therefore it introduces no EF expression tree, no string comparison inside one, and no culture-sensitive call.** The CA1304/CA1311/CA1862-vs-no-translation trap from the public-profile slice cannot recur here.

I nonetheless verified the existing server predicates this flow depends on, because the plan asserts they work:

| Predicate | Where | Compiles? | Translates? |
|---|---|---|---|
| `x => x.OwnerUserId == ownerUserId` | `CreateTeamHandler.CountAsync`, `ListMyTeamsQueryHandler.FindAsync` | Yes — `Guid` equality, no analyzer surface | Yes — scalar equality |
| `x => engineerIds.Contains(x.Id)` | `SetTeamMembersHandler` | Yes | Yes — translates to `IN` |
| `x => versionIds.Contains(x.Id)` | `SetTeamMembersHandler` | Yes | Yes |
| `x => x.ItemType == ItemType.Team && x.ItemId == team.Id && (x.Status == …)` | `PublishTeamHandler` | Yes — enum/`Guid` equality | Yes; enums are `HasConversion<string>()` |
| `x => x.Status == EngineerStatus.Published` | `GetCatalogQueryHandler` (the member picker's source) | Yes | Yes |
| `DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase)` | `GetCatalogQueryHandler` | Yes — this is the form CA1862 *asks for* | **Not applicable** — it runs on the materialised `List<Engineer>` after `FindAsync`, never reaching SQL. This is why the picker's search is safe. |

Frontend gates instead: `npm run build` (`tsc -b`), `npm run test`, `npx oxlint` against the measured 8-warning baseline.

## Existing code touched

| File | Change |
|---|---|
| `web/src/lib/workspaceApi.ts` | Add `TeamInput`, `TeamMember`, `TeamDetail`, `TeamMemberSelectionInput`; add `getTeam`, `createTeam`, `updateTeam`, `setTeamMembers`, `publishTeam`. Leave `Team` and `listMyTeams` untouched. |
| `web/src/lib/config.ts` | Add `maxTeamMembers` to the `config` object + `DEFAULT_MAX_TEAM_MEMBERS = 10`. |
| `web/.env.example` | Add `VITE_MAX_TEAM_MEMBERS=10`. |
| `web/src/lib/config.test.ts` | Append one `it` for the `maxTeamMembers` fallback. |
| `web/src/lib/errorMessages.ts` | Add the 17 `TEAM_*` / team-publish-failure entries listed under **Error codes**. |
| `web/src/lib/errorMessages.test.ts` | Append a `teamErrorCodes` array + one `it.each` block. |
| `web/src/lib/catalog.ts` | Delete the `memberSearchPool` export (line 89). |
| `web/src/lib/types.ts` | Delete the `CrewMember` interface (lines 27–31). |
| `web/src/App.tsx` | Inside the existing `ComposerLayout` → `RequireAuth` block, add `<Route path="/workspace/teams/:teamId" element={<TeamComposerPage />} />` directly after the `/workspace/new-team` route. No new import. |
| `web/src/features/composer/TeamComposerPage.tsx` | Full rewrite (see contract). |
| `web/src/features/workspace/WorkspacePage.tsx` | Load teams too; render `WorkspaceRow[]`; add **+ New Team**; move `formatUpdated` out to `workspaceRows.ts`. |
| `web/src/features/publish/PublishStatusPage.tsx` | Branch on `status.itemType` via `publishTargetFor`. |
| `docs/design-prompt.md` | §33: `draggable ordered member list` → `ordered member list with keyboard-accessible move up/down controls (drag-and-drop deferred)`. Change nothing else. |

## Files to create

| # | Path | Type | Summary |
|---|---|---|---|
| 1 | `web/src/features/composer/teamMembers.ts` | pure module | Roster state machine: add/remove/move/serialise, version label, structure preview. |
| 2 | `web/src/features/composer/teamMembers.test.ts` | vitest | 18 cases. |
| 3 | `web/src/features/workspace/workspaceRows.ts` | pure module | Merge engineers + teams into one sorted row model with routes and formatting. |
| 4 | `web/src/features/workspace/workspaceRows.test.ts` | vitest | 10 cases. |
| 5 | `web/src/features/publish/publishTarget.ts` | pure module | Map `itemType` → install kind, composer path, noun. |
| 6 | `web/src/features/publish/publishTarget.test.ts` | vitest | 3 cases. |

### Contracts

**`web/src/lib/workspaceApi.ts`** — append after the existing `Team` interface / at the end of the function list:

```ts
export interface TeamInput {
  slug: string;
  displayName: string;
  description: string | null;
  tags: string[];
}

export interface TeamMember {
  engineerId: string;
  engineerSlug: string;
  pinnedVersionId: string;
  pinnedSemanticVersion: string;
  sortOrder: number;
}

export interface TeamDetail {
  id: string;
  slug: string;
  displayName: string;
  description: string | null;
  tags: string[];
  status: string;
  latestVersionId: string | null;
  members: TeamMember[];
  createdAt: string;
  updatedAt: string;
}

export interface TeamMemberSelectionInput {
  engineerId: string;
  pinnedVersionId: string | null;
}

export function getTeam(teamId: string): Promise<TeamDetail> {
  return requestJson<TeamDetail>(`/teams/${encodeURIComponent(teamId)}`);
}

export function createTeam(input: TeamInput): Promise<Team> {
  return requestJson<Team>('/teams', { method: 'POST', body: input });
}

export function updateTeam(teamId: string, input: TeamInput): Promise<Team> {
  return requestJson<Team>(`/teams/${encodeURIComponent(teamId)}`, { method: 'PUT', body: input });
}

export function setTeamMembers(teamId: string, members: TeamMemberSelectionInput[]): Promise<TeamDetail> {
  return requestJson<TeamDetail>(`/teams/${encodeURIComponent(teamId)}/members`, { method: 'PUT', body: { members } });
}

export function publishTeam(teamId: string, increment: VersionIncrement): Promise<PublishStatus> {
  return requestJson<PublishStatus>(`/teams/${encodeURIComponent(teamId)}/publish`, { method: 'POST', body: { increment } });
}
```

Wire-shape verification: `CreateTeamRequest(Slug, DisplayName, Description, Tags?)` and `UpdateTeamRequest(Slug?, DisplayName, Description, Tags?)` match `TeamInput` field-for-field; `SetTeamMembersRequest(Members?)` of `TeamMemberRequest(EngineerId, PinnedVersionId?)` matches `{ members }`; `PublishTeamRequest([property: JsonRequired] VersionIncrement Increment)` matches `{ increment }` and is bound by MVC's `JsonStringEnumConverter`. `POST /teams` returns **201**, `POST /teams/{id}/publish` returns **202** — `requestJson` treats both as success (only 204 short-circuits).

---

**`web/src/lib/config.ts`** — add alongside `DEFAULT_MAX_UPLOAD_MEGABYTES`:

```ts
const DEFAULT_MAX_TEAM_MEMBERS = 10;
// inside config:
maxTeamMembers: Number(import.meta.env.VITE_MAX_TEAM_MEMBERS ?? DEFAULT_MAX_TEAM_MEMBERS),
```

---

**`web/src/features/composer/teamMembers.ts`** — no React import; only `import type`.

```ts
import type { CatalogEngineer } from '../../lib/api';
import type { TeamMember, TeamMemberSelectionInput } from '../../lib/workspaceApi';

export interface TeamMemberDraft {
  engineerId: string;
  engineerSlug: string;
  pinnedVersionId: string;
  pinnedSemanticVersion: string | null;
}

export interface AddMemberOutcome {
  drafts: TeamMemberDraft[];
  problem: string | null;
}

export function toMemberDrafts(members: TeamMember[]): TeamMemberDraft[];
export function isMember(drafts: TeamMemberDraft[], engineerId: string): boolean;
export function addMember(drafts: TeamMemberDraft[], engineer: CatalogEngineer, maxMembers: number): AddMemberOutcome;
export function removeMemberDraft(drafts: TeamMemberDraft[], engineerId: string): TeamMemberDraft[];
export function moveMemberDraft(drafts: TeamMemberDraft[], fromIndex: number, toIndex: number): TeamMemberDraft[];
export function toMemberSelections(drafts: TeamMemberDraft[]): TeamMemberSelectionInput[];
export function memberVersionLabel(draft: TeamMemberDraft): string;
export function teamStructurePaths(drafts: TeamMemberDraft[]): string[];
```

Behaviour, exactly:

- `toMemberDrafts` — copy, sort by `sortOrder` ascending then `engineerId` ascending, map to `{ engineerId, engineerSlug, pinnedVersionId, pinnedSemanticVersion }`. Never mutate the argument.
- `isMember` — `drafts.some(draft => draft.engineerId === engineerId)`.
- `addMember` — evaluated in this order, each returning `{ drafts, problem }` with **the original array reference on any problem**:
  1. `engineer.latestVersionId === null` → problem `` `${engineer.slug} has no published version to pin yet.` ``
  2. `isMember(drafts, engineer.id)` → problem `` `${engineer.slug} is already on the roster.` ``
  3. `drafts.length >= maxMembers` → problem `` `A team can hold at most ${maxMembers} members.` ``
  4. otherwise → `{ drafts: [...drafts, { engineerId: engineer.id, engineerSlug: engineer.slug, pinnedVersionId: engineer.latestVersionId, pinnedSemanticVersion: null }], problem: null }`
- `removeMemberDraft` — `drafts.filter(draft => draft.engineerId !== engineerId)`.
- `moveMemberDraft` — return `drafts` unchanged when `fromIndex` or `toIndex` is `< 0` or `>= drafts.length`, or when they are equal. Otherwise copy, `splice(fromIndex, 1)`, `splice(toIndex, 0, moved)` — a **move**, not a swap.
- `toMemberSelections` — `drafts.map(draft => ({ engineerId: draft.engineerId, pinnedVersionId: draft.pinnedVersionId }))`, preserving order.
- `memberVersionLabel` — `` draft.pinnedSemanticVersion === null ? 'latest' : `v${draft.pinnedSemanticVersion}` ``.
- `teamStructurePaths` — `[]` when `drafts.length === 0`; otherwise `['.claude-plugin/plugin.json', 'agents/', 'commands/', ...drafts.map(draft => `skills/${draft.engineerSlug}--*/`)]`. The `--` is the double-hyphen namespacing mandated by `docs/plugin-spec.md`; the wildcard is deliberate — the client does not know each member's skill names.

---

**`web/src/features/workspace/workspaceRows.ts`**

```ts
import type { Engineer, Team } from '../../lib/workspaceApi';

export type WorkspaceRowType = 'Engineer' | 'Team';

export interface WorkspaceRow {
  itemId: string;
  slug: string;
  displayName: string;
  type: WorkspaceRowType;
  status: string;
  installCount: number | null;
  updatedAt: string;
  editPath: string;
  actionPath: string;
  actionLabel: 'Publish' | 'View status';
  viewPath: string;
}

export function toWorkspaceRows(engineers: Engineer[], teams: Team[]): WorkspaceRow[];
export function formatInstallCount(installCount: number | null): string;
export function formatUpdated(updatedAt: string): string;
```

- Engineer row: `editPath = ` `` `/workspace/engineers/${engineer.id}` ``; `installCount = engineer.installCount`; `viewPath = engineer.status === 'Published' ? `/e/${engineer.slug}` : editPath`.
- Team row: `editPath = ` `` `/workspace/teams/${team.id}` ``; `installCount = null`; `viewPath = editPath` (Decision 9).
- Both: `actionPath = latestVersionId ? `/workspace/publish?versionId=${latestVersionId}` : editPath`; `actionLabel = latestVersionId ? 'View status' : 'Publish'`.
- Sort: `updatedAt` descending (`Date.parse`), ties by `itemId` ascending.
- `formatInstallCount(null)` → `'—'` (U+2014); number → `installCount.toLocaleString('en-US')`.
- `formatUpdated` — the body currently inline in `WorkspacePage.tsx`: `new Date(updatedAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })`.

---

**`web/src/features/publish/publishTarget.ts`**

```ts
import type { PluginItemType } from '../../lib/config';

export interface PublishTarget {
  itemType: PluginItemType;
  composerPath: string;
  noun: string;
}

export function publishTargetFor(itemType: string, itemId: string): PublishTarget;
```

- `itemType === 'Team'` → `{ itemType: 'Team', composerPath: `/workspace/teams/${itemId}`, noun: 'team' }`
- anything else (including an unknown string) → `{ itemType: 'Engineer', composerPath: `/workspace/engineers/${itemId}`, noun: 'engineer' }`

---

**`web/src/features/composer/TeamComposerPage.tsx`** — full rewrite. Only export is `TeamComposerPage` (an extra export trips `react(only-export-components)` against the 8-warning baseline).

Module constants: `MEMBER_SEARCH_DEBOUNCE_MS = 250`, `MEMBER_SEARCH_PAGE_SIZE = 6`, `increments: VersionIncrement[] = ['Patch', 'Minor', 'Major']`, `labelStyle`.

State: `routeTeamId = useParams().teamId ?? null`; `teamId`, `displayName`, `description`, `tags`, `tagDraft`, `serverSlug`, `members: TeamMemberDraft[]`, `memberQuery`, `candidates: CatalogEngineer[]`, `increment`, `saving`, `publishing`, `errorMessage`, `lastSaved` (`'never'`), `loadStatus` (`routeTeamId ? 'loading' : 'ready'`). Derived: `slug = serverSlug ?? toSlug(displayName)`.

Effect A — load, `[routeTeamId]`, mirrors `EngineerComposerPage`: return early when `routeTeamId` is null; `let cancelled = false`; `getTeam(routeTeamId).then(team => { if (cancelled) return; setTeamId, setDisplayName, setDescription(team.description ?? ''), setTags, setServerSlug, setMembers(toMemberDrafts(team.members)), setLoadStatus('ready'); }).catch(error => { if (!cancelled) { setLoadStatus('failed'); setErrorMessage(messageForApiError(error)); } })`; cleanup sets `cancelled = true`. No synchronous `setState` at the top of the effect body — that is what makes `EngineerDetailPage:23` warn.

Effect B — member search, `[memberQuery]`: `let cancelled = false`; `const timer = window.setTimeout(() => { getCatalog({ searchText: memberQuery.trim() || undefined, pageNumber: 1, pageSize: MEMBER_SEARCH_PAGE_SIZE }).then(result => { if (!cancelled) setCandidates(result.items); }).catch(() => { if (!cancelled) setCandidates([]); }); }, memberQuery ? MEMBER_SEARCH_DEBOUNCE_MS : 0);` cleanup clears the timer and sets `cancelled`.

`persist(): Promise<TeamDetail>` — the single write path:
1. `const input: TeamInput = { slug, displayName, description: description || null, tags };`
2. `const saved = await (teamId ? updateTeam(teamId, input) : createTeam(input));`
3. `setServerSlug(saved.slug);` and, when `teamId === null`: `setTeamId(saved.id); navigate(`/workspace/teams/${saved.id}`, { replace: true });`
4. `const detail = await setTeamMembers(saved.id, toMemberSelections(members));`
5. `setMembers(toMemberDrafts(detail.members)); setLastSaved('just now');`
6. `return detail;`

`handleSaveDraft` — `if (saving || publishing) return;` `setSaving(true); setErrorMessage(null); persist().then(() => showToast('Draft saved')).catch(error => setErrorMessage(messageForApiError(error))).finally(() => setSaving(false));`

`handlePublish` — `if (saving || publishing) return;` `setPublishing(true); setErrorMessage(null); persist().then(detail => publishTeam(detail.id, increment)).then(result => navigate(`/workspace/publish?versionId=${result.versionId}`)).catch(error => { setErrorMessage(messageForApiError(error)); setPublishing(false); });`

`handleAddMember(engineer: CatalogEngineer)` — `const outcome = addMember(members, engineer, config.maxTeamMembers); if (outcome.problem !== null) { setErrorMessage(outcome.problem); return; } setErrorMessage(null); setMembers(outcome.drafts); showToast(`Added ${engineer.slug}`);`

`addTag()` — identical to `EngineerComposerPage.addTag`.

Render (keeps the existing two-column visual language and every class/`var(--token)` already in the file):
- `loadStatus === 'loading'` → the same `Loading…` block as `EngineerComposerPage`.
- `<ComposerShell title={teamId ? displayName || 'Team' : 'New team'} lastSaved={lastSaved} onSaveDraft={handleSaveDraft} onPublish={handlePublish} saveDisabled={saving || publishing} publishDisabled={publishing || saving || members.length === 0 || displayName.trim().length === 0} publishLabel={publishing ? 'Publishing…' : 'Publish'} statusLabel={saving ? 'Saving…' : 'Draft'}>`
- Left column: dismissible error banner (copy the `EngineerComposerPage` markup verbatim); Team name `<input>` + `slug: {slug || '—'}`; Description `<textarea>`; Tags chip input; the existing cyan snapshot-immutability note; the Version increment `<select id="version-increment">` (moved here from the right column so the right column is purely the roster).
- Right column, block 1 — **Add members**: search `<input value={memberQuery}>`; then `candidates.map(candidate => …)` rendering emoji (`emojiFor(candidate.slug)`), `candidate.slug`, `candidate.displayName`, and either `Added ✓` when `isMember(members, candidate.id)` or `<button type="button" onClick={() => handleAddMember(candidate)} className="btn-primary">Add</button>`. When `candidates.length === 0`, one muted row `No published engineers match that search.`
- Right column, block 2 — **Members** (`{members.length} / {config.maxTeamMembers}`): each row shows `String(index + 1).padStart(2, '0')`, emoji, `engineerSlug`, `memberVersionLabel(draft)` in a `version-badge`, then three `<button type="button">`: `↑` (`aria-label={`Move ${draft.engineerSlug} up`}`, `disabled={index === 0}`, `onClick={() => setMembers(moveMemberDraft(members, index, index - 1))}`), `↓` (`disabled={index === members.length - 1}`, `→ index + 1`), `×` (`aria-label={`Remove ${draft.engineerSlug}`}`, `onClick={() => setMembers(removeMemberDraft(members, draft.engineerId))}`). Empty roster → muted `Add at least one published engineer before publishing.`
- Right column, block 3 — **Structure preview**: `<StructureTree fontSize={12} entries={teamStructurePaths(members).map(path => ({ label: path, indent: path.startsWith('skills/') }))} />`.

Every interactive control is a real `<button type="button">` or `<Link>`; no `<div onClick>` (`react-feature.md` §6). Note the prototype's `<span onClick>` handles — they do **not** survive the rewrite.

---

**`web/src/features/workspace/WorkspacePage.tsx`**

- Load both: one effect, `[reloadToken]`, `Promise.all([listMyEngineers(), listMyTeams()])` → `if (!cancelled) { setRows(toWorkspaceRows(engineers, teams)); setStatus('ready'); }`, `.catch` → `setStatus('failed')`. One failure fails the page, matching the existing single-source behaviour.
- Header: `+ New Engineer` **and** `+ New Team` (`className="btn-primary"` / `className="btn-secondary"`), both `<button type="button">` navigating to `/workspace/new-engineer` and `/workspace/new-team`.
- Empty state: keep the heading/subline, offer both buttons.
- Table: unchanged `gridColumns`, unchanged header labels; each row now renders `row.slug`, `row.displayName`, `row.type`, `statusChipStyle(row.status)`, `formatInstallCount(row.installCount)`, `formatUpdated(row.updatedAt)`, and three `<Link>`s to `row.editPath` (Edit), `row.actionPath` (`row.actionLabel`), `row.viewPath` (View). `key={row.itemId}`.
- Delete the local `formatUpdated`; keep `statusChipStyle` local (it returns `CSSProperties` and moving it would add an export to a `.tsx`, tripping `only-export-components`).

---

**`web/src/features/publish/PublishStatusPage.tsx`**

- Replace the `engineer: Engineer | null` state with `publishedSlug: string | null`.
- `const target = status === null ? null : publishTargetFor(status.itemType, status.itemId);`
- Inside the poll's terminal branch, when `result.status === 'Published'`: `const load = result.itemType === 'Team' ? getTeam(result.itemId).then(team => team.slug) : getEngineer(result.itemId).then(engineer => engineer.slug); load.then(loadedSlug => { if (!cancelled) setPublishedSlug(loadedSlug); }).catch(() => undefined);`
- Success subline: `` publishedSlug !== null ? `${publishedSlug} is live in the catalog` : `Your ${target.noun} is live in the catalog` ``.
- Install block: `<InstallBlock single line2={installCommand(publishedSlug, target.itemType)} />`, rendered only when `publishedSlug !== null`.
- "View in catalog" button: render **only** when `target.itemType === 'Engineer'` (no team catalog page yet — Decision 9 / D4).
- Failure block: `<Link to={target.composerPath}>` replaces the hard-coded `/workspace/engineers/${status.itemId}`.
- Everything else — stepper, chained `setTimeout` poll, the `let attempts` local, `POLL_*` constants, `failureText` — unchanged.

## Error codes

**No new error code, no resx change.** Verified: every `TEAM_*` constant in `api/E3A.Application/Exceptions/ErrorCodes.cs` (27 of them, lines 44–69) already has a key in both `api/E3A.Api/Resources/Messages.en.resx` and `Messages.ar.resx` — `grep -c "TEAM_"` returns 27 for each file. The implementer must not add, rename or reword a single one.

What *does* change is the client-side fallback map. `messageForApiError` prefers the server's localized `message`, so these only fire for an empty body and — critically — for `failureText`, which maps raw `failureReason` codes (`react-feature.md` §4). Add to `web/src/lib/errorMessages.ts`:

| Code | English prose |
|---|---|
| `TEAM_NOT_FOUND` | We couldn't find that team. |
| `TEAM_NOT_OWNED` | That team belongs to someone else. |
| `TEAM_LIMIT_REACHED` | You have reached the number of teams you can create. |
| `TEAM_EMPTY` | Add at least one member before publishing this team. |
| `TEAM_SLUG_FROZEN` | A team's name cannot change after its first publish. |
| `TEAM_SLUG_RESERVED` | That team name is reserved. Please choose another. |
| `TEAM_SLUG_INVALID` | That team name cannot be turned into a valid plugin name. |
| `TEAM_SLUG_TOO_SHORT` | That team name is too short. |
| `TEAM_SLUG_TOO_LONG` | That team name is too long. |
| `TEAM_DISPLAY_NAME_REQUIRED` | Please give the team a name. |
| `TEAM_DISPLAY_NAME_TOO_LONG` | That team name is longer than we allow. |
| `TEAM_DISPLAY_NAME_INVALID` | A team name needs at least one English letter or digit. |
| `TEAM_DESCRIPTION_TOO_LONG` | That description is too long. Please shorten it. |
| `TEAM_TOO_MANY_TAGS` | That is more tags than a team can carry. |
| `TEAM_TAG_TOO_LONG` | One of those tags is too long. |
| `TEAM_MEMBER_LIMIT_REACHED` | That is more members than a team can hold. |
| `TEAM_MEMBER_DUPLICATE` | That engineer is already on the roster. |
| `TEAM_MEMBER_NOT_PUBLISHED` | That engineer has no published version to pin. |
| `TEAM_MEMBER_VERSION_NOT_PUBLISHED` | The version pinned for one member is not published. |
| `TEAM_MEMBER_SNAPSHOT_EMPTY` | One member's published files could not be read. |
| `TEAM_MEMBER_MANIFEST_INVALID` | One member's published manifest could not be read. |
| `TEAM_ROSTER_INVALID` | This team's saved roster could not be read. |
| `PLUGIN_SECURITY_SCAN_BLOCKED` | The security scan blocked this publish. Review the report and try again. |
| `MARKETPLACE_TEAM_LIMIT_EXCEEDED` | The marketplace is at its team limit. Please try again later. |

No Arabic strings are needed for these — `errorMessages.ts` is the English-only client fallback; the bilingual pair already exists server-side.

## Domain behaviour

**None authored in this slice.** The domain methods this flow drives already exist and are already tested; they are listed here so the reviewer can confirm nothing was re-implemented:

- `Team.Create(ownerUserId, slug, displayName, description, tags)` — `Status = Draft`, `LatestVersionId = null`, stamps `CreationDate`/`UpdationDate`.
- `Team.UpdateMetadata(displayName, description, tags)` — stamps `UpdationDate`.
- `Team.ChangeSlug(slug)` — stamps `UpdationDate`; guarded in the handler by `Team.IsSlugMutable => LatestVersionId == null`, which throws `BusinessRuleViolationCoreException(TeamSlugFrozen)`.
- `Team.ReplaceMembers(pins, updatedBy)` — clears `Members`, re-creates one `TeamMember` per pin with `SortOrder = index`, stamps `UpdationDate`. **This is why client array order is the roster order.**
- `Team.MarkPublished(latestVersionId)` — called only by `ProcessPublishJobHandler` after a successful build.
- `Team.Delete()` — `Status = Deleted` + `SoftDelete()` + stamp. Not reachable from this slice's UI (D6).

The one domain guard this UI must never provoke: `PublishTeamHandler` throws `BadRequestCoreException(TeamEmpty)` when `team.Members.Count == 0`. Decision 6 (save-then-publish) plus the `members.length === 0` publish-disable make that unreachable from the composer; manual check **V4** confirms it.

## API surface

No endpoint is added, removed or changed. The eight endpoints this slice *consumes*, all pre-existing:

| Method | Route | Auth | Request | Response |
|---|---|---|---|---|
| GET | `/api/teams/mine` | `[Authorize]` | — | `TeamResult[]` |
| GET | `/api/teams/{teamId:guid}` | `[AllowAnonymous]`, owner-checked in handler for non-published | — | `TeamDetailResult` |
| POST | `/api/teams` | `[Authorize]` | `CreateTeamRequest` | 201 `TeamResult` |
| PUT | `/api/teams/{teamId:guid}` | `[Authorize]` | `UpdateTeamRequest` | 200 `TeamResult` |
| PUT | `/api/teams/{teamId:guid}/members` | `[Authorize]` | `SetTeamMembersRequest` | 200 `TeamDetailResult` |
| POST | `/api/teams/{teamId:guid}/publish` | `[Authorize]` | `PublishTeamRequest` | 202 `PublishStatusResult` |
| GET | `/api/publish/{versionId:guid}/status` | `[Authorize]` | — | `PublishStatusResult` |
| GET | `/api/catalog?q=&page=&pageSize=` | `[AllowAnonymous]` | — | `PageData<CatalogEngineerResult>` |

`postman/e3a.postman_collection.json` already contains a request for each; **it must not be edited**. If the implementer finds themselves editing it, they have gone out of scope.

## Test plan

Baseline to beat: `npm run test` = 12 files / 79 tests, all green. Every file below is `.ts` (a `.tsx` test is invisible to `include: ['src/**/*.test.ts']`). Style: `describe('<exportName>')` + `it('should <outcome> when <condition>')`. Shared local fixture builders `engineerWith(overrides)` / `teamWith(overrides)` / `draft(slug, versionId, semanticVersion)`, following `profileItems.test.ts`.

| # | Test file | `describe` | `it` | Asserts |
|---|---|---|---|---|
| 1 | `teamMembers.test.ts` | `addMember` | should add the engineer pinned to its latest version when the roster has room | `drafts` length +1; last draft's `pinnedVersionId` equals the engineer's `latestVersionId`; `pinnedSemanticVersion` is `null`; `problem` is `null` |
| 2 | `teamMembers.test.ts` | `addMember` | should refuse an engineer with no published version | `problem` names the slug; returned `drafts` is the same reference as the input |
| 3 | `teamMembers.test.ts` | `addMember` | should refuse an engineer already on the roster | `problem` is non-null; `drafts` length unchanged |
| 4 | `teamMembers.test.ts` | `addMember` | should refuse a new member when the roster is at the cap | with `maxMembers = 2` and 2 drafts: `drafts` length still 2; `problem` contains `'2'` |
| 5 | `teamMembers.test.ts` | `isMember` | should report true for an engineer on the roster | `true` |
| 6 | `teamMembers.test.ts` | `isMember` | should report false for an engineer not on the roster | `false` |
| 7 | `teamMembers.test.ts` | `removeMemberDraft` | should drop only the named member | length -1; the other slugs survive in order |
| 8 | `teamMembers.test.ts` | `removeMemberDraft` | should leave the roster unchanged when the engineer is not on it | same length, same slugs |
| 9 | `teamMembers.test.ts` | `moveMemberDraft` | should move a member to an earlier index and shift the others down | from `[a,b,c]`, move 2→0 gives `[c,a,b]` (a swap would give `[c,b,a]`) |
| 10 | `teamMembers.test.ts` | `moveMemberDraft` | should move a member to a later index | from `[a,b,c]`, move 0→2 gives `[b,c,a]` |
| 11 | `teamMembers.test.ts` | `moveMemberDraft` | should leave the roster unchanged when an index is out of range | `-1` and `length` both return the original order |
| 12 | `teamMembers.test.ts` | `toMemberDrafts` | should order members by sortOrder rather than array order | input array ordered `[sortOrder 2, 0, 1]` → output slugs in `0,1,2` order |
| 13 | `teamMembers.test.ts` | `toMemberDrafts` | should carry the pinned version and semantic version onto each draft | `pinnedVersionId` and `pinnedSemanticVersion` match the source member |
| 14 | `teamMembers.test.ts` | `toMemberSelections` | should send each member's explicit pinned version id in roster order | every `pinnedVersionId` is non-null and equals its draft's; array order preserved |
| 15 | `teamMembers.test.ts` | `memberVersionLabel` | should prefix a saved semantic version with v | `'v1.2.0'` |
| 16 | `teamMembers.test.ts` | `memberVersionLabel` | should read latest when the member has not been saved yet | `'latest'` |
| 17 | `teamMembers.test.ts` | `teamStructurePaths` | should namespace each member's skills folder with a double hyphen | contains `'skills/payments-engineer--*/'`; one skills row per member |
| 18 | `teamMembers.test.ts` | `teamStructurePaths` | should return no paths for an empty roster | `[]` |
| 19 | `workspaceRows.test.ts` | `toWorkspaceRows` | should type an engineer row Engineer and a team row Team | one row of each `type` |
| 20 | `workspaceRows.test.ts` | `toWorkspaceRows` | should order rows by updatedAt descending across both item types | interleaved fixture (team newest, engineer oldest) → team first |
| 21 | `workspaceRows.test.ts` | `toWorkspaceRows` | should leave a team install count null so no number is rendered | team row `installCount` is `null`; engineer row carries its number |
| 22 | `workspaceRows.test.ts` | `toWorkspaceRows` | should point an engineer edit link at the engineer composer and a team edit link at the team composer | `/workspace/engineers/{id}` and `/workspace/teams/{id}` |
| 23 | `workspaceRows.test.ts` | `toWorkspaceRows` | should label the action View status when a latest version exists and Publish when it does not | both labels + the `?versionId=` query on the former |
| 24 | `workspaceRows.test.ts` | `toWorkspaceRows` | should point a published engineer view link at its catalog page and a draft one at its composer | `/e/{slug}` vs `/workspace/engineers/{id}` |
| 25 | `workspaceRows.test.ts` | `toWorkspaceRows` | should point a team view link at the composer because no public team page exists | team `viewPath` equals its `editPath` and never starts `/t/` |
| 26 | `workspaceRows.test.ts` | `toWorkspaceRows` | should return no rows when the creator has neither engineers nor teams | `[]` |
| 27 | `workspaceRows.test.ts` | `formatInstallCount` | should group thousands with separators | `'12,345'` |
| 28 | `workspaceRows.test.ts` | `formatInstallCount` | should render an em dash when there is no install count | `'—'` |
| 29 | `workspaceRows.test.ts` | `formatUpdated` | should format the date as short month, day and year | contains `'2026'` and `'Mar'` |
| 30 | `publishTarget.test.ts` | `publishTargetFor` | should route a team publish back to the team composer | `itemType` `'Team'`, `composerPath` `/workspace/teams/{id}`, `noun` `'team'` |
| 31 | `publishTarget.test.ts` | `publishTargetFor` | should route an engineer publish back to the engineer composer | `'Engineer'`, `/workspace/engineers/{id}`, `'engineer'` |
| 32 | `publishTarget.test.ts` | `publishTargetFor` | should treat an unrecognised item type as an engineer | `'Engineer'` |
| 33 | `config.test.ts` (append to the existing `describe('config')`) | `config` | should fall back to ten team members when VITE_MAX_TEAM_MEMBERS is unset | `config.maxTeamMembers` is `10` |
| 34 | `errorMessages.test.ts` (append) | `messageForErrorCode` | should map every team error code to readable text | `it.each(teamErrorCodes)` over all 24 codes in the table above: message non-empty, `!== code`, contains no `_` |

Expected result: `npm run test` reports **15 files** and **79 + 33 + 24 = 136 tests** (34 rows, but row 34 is an `it.each` of 24 cases and rows 1–33 are 33 single cases → 33 + 24 = 57 new). If the reported count differs, a test was silently dropped or duplicated — investigate before reporting green.

### What no test here can prove (state this in the report)

- **Nothing above touches a component.** `environment: 'node'` with `include: ['src/**/*.test.ts']` cannot mount React, so no test proves that `TeamComposerPage` actually calls `addMember`, that Publish is disabled on an empty roster, that the ↑ button is wired to `index - 1`, or that `WorkspacePage` renders `WorkspaceRow[]`. This is `dotnet-testing.md` §9's "wrong level" trap in its frontend form: the pure functions are correct over already-correct inputs, and the defect channel is the *caller*. Do not imply coverage. The manual checks below are the evidence.
- There is no backend test in this slice because there is no backend change. The 815 existing tests must still pass unchanged — a diff in that number is itself a finding.

### Mandatory mutation checks (`conventions/dotnet-testing.md` §9)

Before running each: copy the production file to the scratch directory. After restoring, verify with `cmp` (or `md5sum` before/after) — **never re-edit from memory**. Record both observed outcomes (the failing test name, and green after restore) in `02-implementation.md`.

| # | Mutate | In | Must break — and ideally only this |
|---|---|---|---|
| M1 | `drafts.length >= maxMembers` → `drafts.length > maxMembers` | `teamMembers.ts` | #4 |
| M2 | Delete the `engineer.latestVersionId === null` branch | `teamMembers.ts` | #2 |
| M3 | Delete the `isMember` duplicate branch | `teamMembers.ts` | #3 |
| M4 | Replace the splice move with a two-element swap | `teamMembers.ts` | #9 and #10 |
| M5 | Drop the out-of-range guard so `toIndex === drafts.length` proceeds | `teamMembers.ts` | #11 |
| M6 | Remove the `sortOrder` sort from `toMemberDrafts` | `teamMembers.ts` | #12 |
| M7 | `pinnedVersionId: draft.pinnedVersionId` → `pinnedVersionId: null` | `teamMembers.ts` | #14 |
| M8 | `--` → `-` in the skills path | `teamMembers.ts` | #17 |
| M9 | Team `installCount: null` → `installCount: 0` | `workspaceRows.ts` | #21 and #28 must still pass (they test different functions) — #21 only |
| M10 | Remove the descending `updatedAt` sort | `workspaceRows.ts` | #20 |
| M11 | Team `viewPath` → `` `/t/${team.slug}` `` | `workspaceRows.ts` | #25 |
| M12 | `latestVersionId ? 'View status' : 'Publish'` → always `'Publish'` | `workspaceRows.ts` | #23 |
| M13 | Let the `'Team'` case fall through to the engineer branch | `publishTarget.ts` | #30 |
| M14 | `'—'` → `'0'` | `workspaceRows.ts` | #28 |

If a mutation breaks **nothing**, the corresponding test is vacuous and must be rewritten before the slice ships.

### Manual verification (the component layer)

| # | Check | Evidence to record |
|---|---|---|
| V1 | `npm run build` | Zero TS errors. `tsc` is the proof that `setTeamMembers(id, toMemberSelections(members))` type-checks as `TeamMemberSelectionInput[]` and that `installCommand(slug, target.itemType)` accepts `PluginItemType`. |
| V2 | Full happy path against a locally running API: sign in → **+ New Team** → name it → search → add two published engineers → **Save draft** → **Publish** | The four response bodies: `201` from `POST /api/teams`, `200` from `PUT /api/teams/{id}/members` with a two-element `members` array carrying real `pinnedSemanticVersion` values, `202` from `POST /api/teams/{id}/publish`, and the status poll reaching `"status":"Published"`. Then the rendered install line reading exactly `/plugin install e3a-team-{slug}@e3a`. |
| V3 | Keyboard-only pass on the composer | Tab reaches, in order: name, description, tag input, increment select, member search, each **Add**, each ↑ / ↓ / ×, **Save draft**, **Publish**; Enter/Space activates each. No control reachable only by mouse (`react-feature.md` §6). |
| V4 | Empty-roster publish | Publish is disabled with zero members. Temporarily remove the `members.length === 0` term, confirm the `400 TEAM_EMPTY` body renders as the prose from the error map (not the raw code), then restore and re-verify with `cmp`. |
| V5 | Workspace round-trip | The saved team appears as a row: type `Team`, installs `—`, Edit → `/workspace/teams/{id}` which reloads the roster in the composer with the pinned versions shown as `v…`, not `latest`. |
| V6 | Reopen after publish | `/workspace/teams/{id}` on a published team shows the slug as static text (no slug input), and Save draft succeeds without a `TEAM_SLUG_FROZEN` error (Decision 5). |
| V7 | `npx oxlint` | Still **8** warnings, same rules and files as the baseline. A 9th means an export leaked into a `.tsx`. |

## Definition of done

- [ ] Not one file under `api/` is modified — `git diff --stat main -- api/` is empty.
- [ ] `postman/e3a.postman_collection.json` is unmodified.
- [ ] `ErrorCodes.cs`, `Messages.en.resx`, `Messages.ar.resx` are unmodified; `grep -c "TEAM_"` still returns 27 for both resx files.
- [ ] `dotnet test api/E3a.slnx` → 815 passed, 0 failed (unchanged).
- [ ] The six new files exist at exactly the paths in **Files to create**; no other new file exists.
- [ ] `workspaceApi.ts` exports `getTeam`, `createTeam`, `updateTeam`, `setTeamMembers`, `publishTeam` with the signatures above; every interpolated id passes through `encodeURIComponent`.
- [ ] `TeamComposerPage.tsx` imports nothing from `lib/catalog.ts`; `memberSearchPool` and `CrewMember` are deleted and grep finds no remaining reference.
- [ ] `App.tsx` routes `/workspace/teams/:teamId` inside `ComposerLayout` → `RequireAuth`.
- [ ] `WorkspacePage.tsx` renders team rows and both `+ New Engineer` and `+ New Team`.
- [ ] `PublishStatusPage.tsx` contains no hard-coded `/workspace/engineers/` path and no unconditional `getEngineer` call.
- [ ] Every `.tsx` touched exports exactly one symbol (its component).
- [ ] Every interactive control added is a `<button type="button">`, `<a>` or `<Link>`; zero `<div onClick>` / `<span onClick>` remain in `TeamComposerPage.tsx`.
- [ ] `config.maxTeamMembers` is read from `VITE_MAX_TEAM_MEMBERS`; `web/.env.example` lists it; no bare `10` appears in `TeamComposerPage.tsx` or `teamMembers.ts`.
- [ ] The 24 error codes in the **Error codes** table are present in `lib/errorMessages.ts`.
- [ ] `npm run test` green; file count 15; test count 136.
- [ ] All 14 mutation checks (M1–M14) were performed, each observed to break its named test, each file restored and verified byte-identical with `cmp`. Both outcomes recorded in `02-implementation.md`.
- [ ] V1–V7 performed, with the recorded evidence (response bodies, install line, oxlint count) in `02-implementation.md`.
- [ ] `npm run build` zero errors; `npx oxlint` still 8 warnings; zero `oxlint-disable` / `@ts-ignore` anywhere.
- [ ] `docs/design-prompt.md` §33 updated to the wording in Decision 12 — and **no other doc touched**, because nothing else in this change diverges from `/docs` (`.claude/rules/docs-sync.md`).
- [ ] `02-implementation.md` states plainly which behaviours remain unproven by automated test (all component wiring) and which manual check stands in for each.
