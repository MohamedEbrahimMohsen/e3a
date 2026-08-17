(function(){
if(window.E3A_DATA) return;
// E3A seed world (global build) — mock data module (imported by E3A.dc.html logic)
const PLATFORMS = {
  claude:{ id:'claude', label:'Claude', color:'#34E6E0', output:'Skill package + MCP config' },
  cursor:{ id:'cursor', label:'Cursor', color:'#9B7BFF', output:'Rules / .mdc files' },
  codex:{ id:'codex', label:'Codex', color:'#FFCA57', output:'AGENTS.md + skill package' },
  antigravity:{ id:'antigravity', label:'AntiGravity', color:'#F5A65B', output:'Experimental adapter' },
  mcp:{ id:'mcp', label:'Generic MCP', color:'#98A3B3', output:'MCP config + scoped key' },
};
const DOMAINS = {
  Architecture:'#34E6E0', Implementation:'#9B7BFF', Process:'#45D79D', Tooling:'#FFCA57', Team:'#F5A65B',
};
const TYPES = ['Principle','Preference','Decision','Anti-Pattern','Heuristic','Quality Threshold','Workflow','Pattern'];
const MODULES = ['Core','Architecture','Testing','Security','Review','Deploy','Avoid'];

const B = (id,eng,type,domain,statement,evidence,source,date,confidence,extra)=>Object.assign({
  id,eng,type,domain,statement,evidence,source,date,confidence,status:'active',
  history:[{t:date,label:'Extracted',c:Math.max(0.2,confidence-0.35)},{t:date,label:'Confirmed',c:confidence}],
  rel:[],conflictWith:null,scope:[],events:2
},extra||{});

function seedBeliefs(){ return [
// ——— .NET Architect (e-net) ———
B('B-001','e-net','Principle','Architecture',
 'Domain logic never references infrastructure; enforce dependency direction with architecture tests, not review comments.',
 'We keep catching EF Core types leaking into the domain during review, and it always slips through eventually. I added NetArchTest rules to the CI gate so the dependency direction is enforced by the build itself. Reviewers should argue about the model, not police using statements.',
 'ADR-012, Mar 2025','2025-03-14',0.94,{rel:['B-004','B-008']}),
B('B-002','e-net','Decision','Architecture',
 'Start every new product as a modular monolith; extract services only when a module has a proven independent scaling or deployment need.',
 'The billing split cost us four months of distributed-transaction pain for a service that deploys twice a quarter. Going forward we build modular monoliths with hard module boundaries and only extract when the operational evidence demands it — never for org-chart reasons.',
 'ADR-019, Jun 2025','2025-06-02',0.91,{rel:['B-003']}),
B('B-003','e-net','Heuristic','Architecture',
 'The first architectural question is always: what must be strongly consistent, and what can be eventually consistent?',
 'Before any diagram, I ask the team to list the invariants that must never be observed broken. In the payments kickoff that single question killed the event-sourced ledger idea in twenty minutes — the balance check had to be transactional, everything else could lag.',
 'Design review — payments-service, Feb 2025','2025-02-08',0.88,{rel:['B-002','B-008']}),
B('B-004','e-net','Anti-Pattern','Architecture',
 'Generic repository abstractions over EF Core hide the unit-of-work and produce N+1 queries; expose intent-named query methods instead.',
 'The IRepository<T> layer in the ordering module made every list endpoint an N+1 minefield and nobody could see the SQL. We deleted it, wrote intent-named methods like GetOpenOrdersWithLines, and the p95 dropped from 900ms to 210ms with no other change.',
 'Postmortem PM-07, Jan 2025','2025-01-21',0.9,{rel:['B-001']}),
B('B-005','e-net','Preference','Implementation',
 'DTOs and value objects are records; mutation is opt-in and must be justified in review.',
 'Every bug class we traced in the quote engine came from something mutating a shared DTO mid-pipeline. Since we switched contracts to records with init-only setters, that whole category of defect disappeared from the board.',
 'Code review — quote-engine #311, Apr 2025','2025-04-11',0.86),
B('B-006','e-net','Quality Threshold','Implementation',
 'Nullable reference types are enabled solution-wide and the build fails on warnings; suppressions require a linked issue.',
 'We turned on <Nullable>enable</Nullable> with TreatWarningsAsErrors in the platform template. The first week hurt; the next quarter we shipped zero NullReferenceExceptions to production for the first time since I have been here.',
 'Platform template PR #48, Nov 2024','2024-11-30',0.89),
B('B-007','e-net','Workflow','Process',
 'Any decision that outlives a sprint gets an ADR — one page, context, decision, consequences — before the code merges.',
 'Six months after the caching rewrite nobody could remember why we rejected Redis Cluster. Now the rule is simple: if the decision will still matter next quarter, it gets an ADR in the same PR. Archaeology is not a viable knowledge strategy.',
 'Team working agreement, Oct 2024','2024-10-15',0.92,{rel:['B-016']}),
B('B-008','e-net','Pattern','Architecture',
 'Cross-module events go through a transactional outbox; never publish to the bus inside a database transaction.',
 'The stock-sync incident happened because we published to RabbitMQ before the commit and the transaction rolled back. Outbox with a background dispatcher closed that gap permanently — the event and the state change now succeed or fail together.',
 'Postmortem PM-11, May 2025','2025-05-19',0.93,{rel:['B-001','B-003']}),
B('B-009','e-net','Heuristic','Implementation',
 'Optimize code for delete-ability: small classes, narrow interfaces, no speculative extension points.',
 'The easiest code to maintain is code you can delete in one commit. When I review, I ask "if this feature dies, how many files do we touch?" — if the answer is more than a folder, the design is wrong.',
 'Engineering guild talk, Sep 2024','2024-09-20',0.82),
B('B-010','e-net','Principle','Process',
 'Database migrations are reviewed like code and every migration ships with a tested rollback script.',
 'The column-rename outage taught us that a migration is production code with the blast radius of the whole database. Since we required rollback scripts and a second reviewer on migrations, we have had zero schema-related incidents in nine months.',
 'Postmortem PM-03, Aug 2024','2024-08-12',0.9),
B('B-011','e-net','Preference','Tooling',
 'NuGet versions are pinned centrally in one Directory.Packages.props; per-project version drift is banned.',
 'We had three versions of Newtonsoft in one solution and a serializer bug that only reproduced in one service. Central package management ended the drift — one file, one version, one upgrade PR.',
 'Code review — platform #229, Dec 2024','2024-12-05',0.84),
B('B-012','e-net','Anti-Pattern','Implementation',
 'AutoMapper does not belong in the domain layer; mapping at boundaries is written by hand where it can be read and debugged.',
 'The invoice bug hid for three weeks inside a convention-based mapping profile nobody could step through. Boundary mapping is boring code — and boring code is exactly what I want between my domain and the outside world.',
 'Code review — invoicing #187, Jul 2024','2024-07-25',0.85),
B('B-013','e-net','Quality Threshold','Process',
 'An endpoint does not ship without a p95 latency budget, and the budget is asserted in a load test, not a dashboard.',
 'Budgets that only live in Grafana are wishes. The checkout rewrite carried a 300ms p95 budget asserted in k6 as part of the pipeline; the one time it went red, we caught a quadratic serializer before customers did.',
 'Checkout rewrite retro, Mar 2025','2025-03-28',0.87),
B('B-014','e-net','Decision','Process',
 'Prefer a small number of high-value integration tests over unit-heavy suites; test behavior at the API boundary with a real database.',
 'The unit suite was green while the release was broken, because every seam was mocked. We rebuilt around Testcontainers-backed API tests: fewer tests, slower to run, and they have caught every regression since. I would rather wait ninety seconds than mock the truth away.',
 'Testing strategy ADR-021, Jun 2025','2025-06-20',0.83,{conflictWith:'B-015'}),
B('B-015','e-net','Principle','Process',
 'Unit tests are the primary safety net; isolate every dependency behind an interface and mock all IO for fast, deterministic suites.',
 'Our standard is one test project per assembly, every collaborator mocked with Moq, sub-second suites. Fast feedback beats realism — integration tests are a smoke layer at best, run nightly.',
 'Team wiki — testing standards, Feb 2023','2023-02-10',0.44,{conflictWith:'B-014'}),
B('B-016','e-net','Workflow','Team',
 'A cross-team API change starts as an RFC thread with a 48-hour comment window before any implementation begins.',
 'The notifications contract change broke two consumer teams because it went straight to a PR. The RFC-first rule is lightweight — one page, two days — and it has turned integration surprises into design conversations.',
 'Working agreement update, Apr 2025','2025-04-02',0.8,{rel:['B-007']}),
B('B-017','e-net','Heuristic','Team',
 'Review comments are prefixed blocking: or nit: — a review with unlabeled objections is a review that stalls the team.',
 'PRs were sitting for days because authors could not tell which comments mattered. The prefix convention sounds trivial, but median time-to-merge dropped from 41 hours to 9 once people knew what actually blocked them.',
 'Code review — guidelines #92, Jan 2025','2025-01-09',0.78),
B('B-018','e-net','Pattern','Implementation',
 'Expected failures return Result<T>; exceptions are reserved for the genuinely exceptional, never for control flow.',
 'Validation failures thrown as exceptions were costing us 40% of checkout CPU under load and made the logs unreadable. Result<T> at the application boundary made failure paths explicit, cheap, and impossible to forget in review.',
 'Perf investigation, Oct 2025','2025-10-03',0.87,{rel:['B-005']}),
B('B-019','e-net','Preference','Tooling',
 'Formatting and style live in analyzers and dotnet format in CI — never in editor settings or review comments.',
 'Style debates in review are a tax on everyone. The pipeline formats and the analyzers enforce; if a rule is not worth automating, it is not worth arguing about in a PR.',
 'Platform template PR #52, Dec 2024','2024-12-18',0.81,{rel:['B-006']}),
B('B-020','e-net','Decision','Tooling',
 'PostgreSQL is the default database; SQL Server only when a client contract requires it, and the choice is recorded in an ADR.',
 'We compared licensing, container story, and JSON support across three projects. Postgres won on every axis that we actually use. Defaults matter — every "we will decide per project" turns into a week of re-litigating.',
 'ADR-016, May 2025','2025-05-05',0.79),
B('B-021','e-net','Anti-Pattern','Process',
 'Feature branches older than three days are a merge debt; slice the work or ship it behind a flag.',
 'The reporting branch lived for three weeks and its merge broke main twice. Long-lived branches hide integration risk exactly where you cannot see it. Small slices behind flags keep main releasable and reviews humane.',
 'Retro — reporting incident, Sep 2025','2025-09-12',0.82),
B('B-022','e-net','Principle','Implementation',
 'Every async path accepts and propagates a CancellationToken; fire-and-forget tasks are banned outside the job runner.',
 'The export endpoint kept running for minutes after clients disconnected, holding connections and burning CPU on work nobody would receive. Token propagation end-to-end fixed it — cancellation is a correctness feature, not a nicety.',
 'Code review — payments-service #482, Aug 2025','2025-08-14',0.88,{rel:['B-018']}),
B('B-023','e-net','Quality Threshold','Team',
 'The onboarding doc is validated by the newest hire every quarter; if day-one setup takes more than half a day, that is a P2.',
 'Docs rot silently. Making the freshest pair of eyes the test harness keeps setup honest — last quarter the new hire found four dead steps and cut day-one setup from six hours to two.',
 'Team health review, Jun 2025','2025-06-30',0.75),
B('B-024','e-net','Workflow','Architecture',
 'Every service exposes /health and /ready with dependency checks; deploys gate on readiness, not on sleep timers.',
 'The rolling deploy that "worked on staging" dropped 4% of traffic because pods took connections before the cache warmed. Readiness probes with real dependency checks made deploys boring again.',
 'Deploy runbook v3, Jul 2025','2025-07-22',0.84),
// ——— Security Reviewer (e-sec) — 3 open conflict pairs ———
B('S-01','e-sec','Principle','Process',
 'All service credentials rotate automatically every 90 days; humans never see production secrets.',
 'The audit found four service accounts with two-year-old keys. Vault-managed rotation with 90-day TTLs closed the finding and removed secrets from every developer laptop.',
 'Security audit response, Apr 2025','2025-04-20',0.86,{conflictWith:'S-02'}),
B('S-02','e-sec','Preference','Process',
 'Long-lived service credentials are acceptable inside the private VPC; rotation overhead outweighs the risk for internal traffic.',
 'Internal services sit behind the VPC boundary with no ingress. Rotating internal credentials quarterly created more outages than attackers ever did.',
 'Infra review notes, Jan 2023','2023-01-15',0.4,{conflictWith:'S-01'}),
B('S-03','e-sec','Quality Threshold','Process',
 'A high-severity CVE in any direct dependency blocks the merge — no exceptions without a signed risk acceptance.',
 'The log4shell week proved that "we will patch later" means "we will patch during the incident". The gate is automated in the pipeline; the escape hatch requires a director signature.',
 'Pipeline policy PR #66, Feb 2025','2025-02-14',0.84,{conflictWith:'S-04'}),
B('S-04','e-sec','Heuristic','Process',
 'High CVEs can merge with a time-boxed exception ticket when the vulnerable path is provably unreachable.',
 'Blocking every high CVE froze three releases over a transitive dependency in a test-only package. Reachability analysis plus a 14-day exception ticket keeps velocity without pretending risk away.',
 'Triage retro, Nov 2024','2024-11-08',0.58,{conflictWith:'S-03'}),
B('S-05','e-sec','Workflow','Process',
 'External penetration tests run annually and before any new public surface ships.',
 'The external testers found the IDOR our internal reviews missed three times. Outside eyes on every new public surface is the cheapest breach we will ever pay for.',
 'Pentest report follow-up, Mar 2025','2025-03-10',0.8,{conflictWith:'S-06'}),
B('S-06','e-sec','Decision','Process',
 'Continuous internal scanning replaces external pentests; the tooling now covers what consultancies used to find.',
 'With DAST in the pipeline and weekly authenticated scans, the last two external engagements produced zero new findings. The budget is better spent on tooling.',
 'Budget planning doc, Dec 2023','2023-12-01',0.45,{conflictWith:'S-05'}),
B('S-07','e-sec','Principle','Architecture',
 'Every input at a trust boundary is validated against an allow-list schema; sanitization is a last resort, not a strategy.',
 'The template-injection finding traversed three services because each assumed the previous one had cleaned the input. Boundary schemas ended the ambiguity about who validates what.',
 'Incident SEC-19, Jun 2025','2025-06-11',0.88),
B('S-08','e-sec','Anti-Pattern','Implementation',
 'Rolling your own crypto, session handling, or password hashing — use the platform primitives and document the choice.',
 'The custom session token scheme from 2021 took two days to break in an internal exercise. ASP.NET Data Protection replaced it in one sprint.',
 'Internal red-team exercise, May 2025','2025-05-27',0.9),
B('S-09','e-sec','Heuristic','Review',
 'In security review, read the error paths first — vulnerabilities live where the happy path ends.',
 'Nine of the last eleven findings were in catch blocks, timeout handlers, and fallback code. The happy path gets all the testing; the sad path gets the CVEs.',
 'Review habits doc, Sep 2025','2025-09-05',0.77,{domain:'Process'}),
B('S-10','e-sec','Quality Threshold','Tooling',
 'Dependency updates are automated weekly; a dependency more than two majors behind is treated as tech debt with an owner.',
 'The frozen dependency set made the eventual framework upgrade a six-week project. Weekly automated bumps keep each individual change trivially reviewable.',
 'Platform maintenance policy, Aug 2025','2025-08-20',0.79),
// ——— UI Engineer (e-ui) ———
B('U-01','e-ui','Principle','Implementation','Component state stays local until two consumers need it; global stores are a last resort with a written justification.','The settings page rewrite removed 14 global atoms that had exactly one reader each. Local state made every component portable again.','Refactor notes, May 2025','2025-05-15',0.82),
B('U-02','e-ui','Preference','Tooling','Design tokens live in one source of truth and compile to CSS variables; hex values in component code fail the lint gate.','Three shades of the brand blue shipped in one quarter before the token gate. Now the palette drifts nowhere because it cannot.','Design system PR #31, Mar 2025','2025-03-22',0.78),
B('U-03','e-ui','Quality Threshold','Process','Interactive elements meet WCAG AA contrast and a 44px hit target — checked in CI with axe, not by eye.','The accessibility audit listed 61 violations, all preventable. Automated axe checks in the pipeline brought new violations to zero in two months.','A11y audit response, Apr 2025','2025-04-18',0.85),
B('U-04','e-ui','Anti-Pattern','Implementation','useEffect for data fetching in new code — server cache libraries own the request lifecycle.','Every stale-data bug last quarter traced to a hand-rolled effect with a missing dependency. Query libraries made the whole class of bug unrepresentable.','Frontend guild talk, Jun 2025','2025-06-25',0.8),
B('U-05','e-ui','Heuristic','Architecture','Ship the skeleton screen first: perceived performance is a design deliverable, measured as time-to-first-meaningful-paint.','The dashboard felt slow at 900ms and fast at 1.2s once the skeleton landed. Users measure feel, not milliseconds.','Perf review, Jul 2025','2025-07-08',0.74),
B('U-06','e-ui','Workflow','Team','Visual changes ship with a before/after screenshot in the PR description; reviewers should never have to run the branch to see the pixels.','Screenshot-first PRs cut review time in half and caught two regressions before merge this quarter.','Team convention, Aug 2025','2025-08-30',0.76),
// ——— E-Commerce team: Backend (e-be) ———
B('EB-01','e-be','Principle','Architecture','Order state transitions are an explicit state machine; illegal transitions are unrepresentable, not validated.','The double-refund bug existed because status was a string column. The state machine rewrite made the invalid path a compile error.','Postmortem OD-4, Feb 2025','2025-02-19',0.89),
B('EB-02','e-be','Pattern','Implementation','Idempotency keys on every mutating endpoint; retries are a client right, not a server surprise.','Payment retries created 212 duplicate orders in one afternoon. Idempotency keys made retries safe and the support queue quiet.','Incident review, Mar 2025','2025-03-05',0.87),
B('EB-03','e-be','Quality Threshold','Process','Checkout p99 under 800ms at 5x normal load, verified in the weekly load test.','Black Friday readiness is a rehearsal, not a hope. The weekly k6 run caught the connection-pool ceiling a month before it would have mattered.','Load test runbook, Sep 2025','2025-09-18',0.83),
B('EB-04','e-be','Decision','Architecture','Inventory reservations use short TTL holds, not hard locks; overselling is handled by compensation, not prevention.','Hard locks serialized the flash sale and cost more revenue than the seven oversells the TTL model produced all year.','ADR-E08, May 2025','2025-05-11',0.8),
B('EB-05','e-be','Anti-Pattern','Implementation','Catching Exception broadly in request handlers — let the middleware translate; local catches hide the real failure.','The silent catch in cart-service swallowed a serializer failure for six days. Broad catches are how incidents hide.','Code review — cart #77, Jan 2025','2025-01-28',0.81),
// ——— E-Commerce team: Frontend (e-fe) ———
B('EF-01','e-fe','Principle','Implementation','The cart is an optimistic UI: every mutation renders instantly and reconciles in the background with visible rollback.','Perceived latency on add-to-cart dropped from 700ms to 0. The rollback toast fires on under 0.4% of actions.','Cart rewrite retro, Apr 2025','2025-04-25',0.84),
B('EF-02','e-fe','Quality Threshold','Process','Bundle budget 180KB gz for the storefront entry; the CI gate fails the build, not a dashboard.','Every "one small library" adds up. The hard gate has been hit eight times and each time we found a lighter path within the day.','Perf budget PR #19, Feb 2025','2025-02-27',0.82),
B('EF-03','e-fe','Heuristic','Architecture','Render product pages statically with client islands only for price and stock; the page is a document, not an app.','Core Web Vitals went green the week we stopped hydrating the whole page for a price badge.','Storefront ADR-3, Jun 2025','2025-06-14',0.79),
B('EF-04','e-fe','Preference','Tooling','Visual regression tests on the five money pages every merge; a pixel diff is cheaper than a support ticket.','The checkout button turned white-on-white in a theme refactor and the pixel diff caught it before any human saw it.','Testing notes, Jul 2025','2025-07-30',0.77),
B('EF-05','e-fe','Workflow','Team','Feature flags default off in production and every flag has a removal date in the ticket.','Forty-one stale flags made the codebase archaeology. The removal-date rule keeps the flag count under a dozen.','Flag hygiene review, Aug 2025','2025-08-08',0.75),
// ——— E-Commerce team: DevOps (e-do) ———
B('ED-01','e-do','Principle','Process','Every deploy is a rollback rehearsal: if the rollback path is untested, the deploy is not ready.','The migration that could not roll back turned a five-minute deploy into a four-hour incident. Rollback-first planning ended that class of night.','Postmortem INF-9, Mar 2025','2025-03-17',0.88),
B('ED-02','e-do','Pattern','Tooling','Infrastructure is Terraform with drift detection in CI; console changes are incidents, not conveniences.','The mystery security-group change took a day to diagnose because it existed only in the console. Drift detection now pages within an hour.','Infra policy, May 2025','2025-05-23',0.85),
B('ED-03','e-do','Quality Threshold','Process','Alert rules must page on symptoms users feel, with fewer than two false pages a week — noisy alerts get deleted, not snoozed.','We deleted 60% of the alert rules and mean time-to-acknowledge improved. An alert nobody trusts is worse than no alert.','On-call retro, Jun 2025','2025-06-09',0.83),
B('ED-04','e-do','Heuristic','Architecture','Capacity plans are written for 10x, provisioned for 2x, and auto-scaled between; paying for peak all year is a design failure.','The flash-sale scaling event cost $340 in burst capacity against $19k of always-on headroom the old plan required.','Capacity review, Sep 2025','2025-09-25',0.78),
B('ED-05','e-do','Anti-Pattern','Process','Snowflake environments — staging that differs from production is a rehearsal for the wrong play.','The TLS bug reproduced nowhere because staging terminated TLS differently. Environment parity is boring and it works.','Incident INF-12, Jul 2025','2025-07-19',0.8),
// ——— Graduation team: AI Engineer (e-ai) ———
B('GA-01','e-ai','Principle','Architecture','Every model output that reaches a user passes through a deterministic validation layer; the model proposes, code disposes.','The grading assistant hallucinated a rubric item in week one. Schema-validated outputs with hard fallbacks ended user-visible hallucinations.','Eval report GR-2, Apr 2025','2025-04-09',0.86),
B('GA-02','e-ai','Quality Threshold','Process','No prompt change ships without an eval run on the golden set; a regression over 2% blocks the merge.','Prompt edits felt free until one dropped extraction accuracy 11% silently. The golden-set gate made prompt engineering an engineering discipline.','Eval pipeline PR #14, May 2025','2025-05-29',0.84),
B('GA-03','e-ai','Heuristic','Implementation','Retrieval quality beats model size: fix the context before reaching for a bigger model.','Upgrading the model improved answers 3%; fixing chunking improved them 22% at a tenth of the cost.','Retrieval experiment log, Jun 2025','2025-06-17',0.81),
B('GA-04','e-ai','Decision','Tooling','All LLM calls go through one gateway with logging, cost caps, and per-feature budgets.','The runaway agent loop cost $600 in a weekend. The gateway with per-feature budgets caps the blast radius of every future mistake.','ADR-G05, Jul 2025','2025-07-12',0.83),
B('GA-05','e-ai','Anti-Pattern','Process','Demo-driven development: a feature that only works on the happy-path demo script is not a feature.','Three "done" features failed on the first real student transcript. Every AI feature now ships with an adversarial test set.','Sprint retro, Aug 2025','2025-08-21',0.79),
// ——— Graduation team: Backend (e-gb) ———
B('GB-01','e-gb','Principle','Architecture','Student records are append-only with full audit trails; corrections are new events, never edits.','The grade-change dispute was resolved in minutes because every mutation had an author and a timestamp. Append-only is the compliance story.','Compliance review, May 2025','2025-05-07',0.85),
B('GB-02','e-gb','Quality Threshold','Implementation','Every API endpoint has an OpenAPI contract reviewed before implementation; the contract is the PR.','Contract-first killed the "frontend blocked on backend" standups. Both sides build against the same YAML from day one.','Working agreement, Jun 2025','2025-06-23',0.8),
B('GB-03','e-gb','Workflow','Process','Background jobs are idempotent and checkpointed; any job must survive being killed at any line.','The enrollment sync died mid-run and re-ran cleanly because of checkpoints. Jobs that cannot be killed safely cannot be operated.','Job runner design doc, Jul 2025','2025-07-28',0.78),
B('GB-04','e-gb','Preference','Tooling','SQLite for local dev, Postgres in CI and beyond — the dev loop stays under five seconds.','Local Docker Postgres added 40 seconds to every test run. SQLite locally with a nightly Postgres matrix keeps the loop tight and the truth honest.','Dev-loop notes, Aug 2025','2025-08-16',0.72),
B('GB-05','e-gb','Heuristic','Team','Estimate in ranges, commit to the pessimistic end, and celebrate the optimistic one.','Two semesters of data: our optimistic estimates were right 22% of the time. Range estimates ended the death marches.','Planning retro, Sep 2025','2025-09-09',0.7),
];}

function seedCandidates(){ return [
 { id:'C-01', eng:'e-net', type:'Heuristic', domain:'Implementation',
   statement:'Every async method accepts a CancellationToken and passes it downstream; fire-and-forget is banned.',
   evidence:'Same as the export incident — any Task we cannot cancel is a resource leak waiting for a slow client. I reject PRs that add async methods without a token parameter, even internal ones.',
   source:'Code review — payments-service #495, Jul 2026', confidence:0.41, batch:'review', nearDup:'B-022' },
 { id:'C-02', eng:'e-net', type:'Preference', domain:'Tooling',
   statement:'Rider over Visual Studio for daily work; the debugger and refactoring tools pay for themselves weekly.',
   evidence:'I moved the team to Rider licenses after the solution-wide rename in VS corrupted three project files. Structural search alone has found four latent bugs this quarter.',
   source:'Tooling survey response, Jun 2026', confidence:0.38, batch:'review' },
 { id:'C-03', eng:'e-net', type:'Decision', domain:'Architecture',
   statement:'All services should share a single database schema — separate databases per service create needless integration work.',
   evidence:'Honestly the shared database never caused us problems back then, and joining across service data was trivial. Splitting databases just meant building APIs for queries we used to write in SQL.',
   source:'Old team transcript, 2019 import', confidence:0.22, batch:'review' },
 { id:'C-04', eng:'e-ui', type:'Preference', domain:'Implementation',
   statement:'Animation durations live in tokens (fast 150ms / base 250ms / slow 400ms); ad-hoc durations fail review.',
   evidence:'The product felt janky because forty components animated at forty speeds. Three tokens later everything feels like one product.',
   source:'Design system PR #44, Jul 2026', confidence:0.36, batch:'review' },
 { id:'C-05', eng:'e-ui', type:'Heuristic', domain:'Process',
   statement:'Prototype in production-grade code from day one; throwaway prototypes always ship.',
   evidence:'Every "quick prototype" I have written in this company is still running. Writing it properly the first time is cheaper than the rewrite that never comes.',
   source:'Frontend guild notes, Jul 2026', confidence:0.33, batch:'review' },
 { id:'C-06', eng:'e-fe', type:'Quality Threshold', domain:'Process',
   statement:'Lighthouse performance score stays above 90 on the money pages; the check runs on every merge to main.',
   evidence:'Scores drifted from 96 to 71 in one quarter of unwatched merges. The gate makes the drift visible one PR at a time instead of one quarter at a time.',
   source:'Perf review, Jul 2026', confidence:0.35, batch:'review' },
];}

function seedEngineers(){ return [
 { id:'e-net', name:'.NET Architect', role:'Solution Architect', seniority:'Principal', team:null,
   stack:['.NET 9','C#','PostgreSQL','Azure','RabbitMQ'], platforms:['claude'], status:'Compiled', version:'1.4.0',
   compiled:{ claude:{version:'1.4.0', date:'2026-07-18', stale:false} },
   promptsDone:['proj','style','arch','test','review','decide'], createdAt:'2024-08-01' },
 { id:'e-sec', name:'Security Reviewer', role:'Security Engineer', seniority:'Senior', team:null,
   stack:['AppSec','SAST/DAST','Vault','Threat modeling'], platforms:['codex'], status:'Conflict Review', version:'0.9.2',
   compiled:{ codex:{version:'0.9.2', date:'2026-06-30', stale:true} },
   promptsDone:['proj','review'], createdAt:'2025-02-01' },
 { id:'e-ui', name:'UI Engineer', role:'Frontend Engineer', seniority:'Senior', team:null,
   stack:['React','TypeScript','Design systems'], platforms:['antigravity'], status:'Collecting', version:'0.4.1',
   compiled:{}, importSession:{stage:4, total:8, label:'Project selection'},
   promptsDone:['proj','style','folder'], createdAt:'2026-05-01' },
 { id:'e-be', name:'Backend Engineer', role:'Backend Engineer', seniority:'Senior', team:'t-ecom',
   stack:['.NET','PostgreSQL','Redis'], platforms:['claude'], status:'Compiled', version:'2.1.0',
   compiled:{ claude:{version:'2.1.0', date:'2026-07-10', stale:false} }, promptsDone:['proj','arch','test'], createdAt:'2025-01-10' },
 { id:'e-fe', name:'Frontend Engineer', role:'Frontend Engineer', seniority:'Mid', team:'t-ecom',
   stack:['React','Next.js'], platforms:['cursor'], status:'Update Available', version:'1.2.0',
   compiled:{ cursor:{version:'1.2.0', date:'2026-06-12', stale:true} }, promptsDone:['proj','style'], createdAt:'2025-01-10' },
 { id:'e-do', name:'DevOps Engineer', role:'DevOps Engineer', seniority:'Senior', team:'t-ecom',
   stack:['Terraform','Kubernetes','AWS'], platforms:['codex'], status:'Installed', version:'1.0.3',
   compiled:{ codex:{version:'1.0.3', date:'2026-07-01', stale:false, installed:true} }, promptsDone:['proj','arch'], createdAt:'2025-03-15' },
 { id:'e-ai', name:'AI Engineer', role:'AI Engineer', seniority:'Senior', team:'t-grad',
   stack:['Python','LLM evals','RAG'], platforms:['claude'], status:'Compiled', version:'1.1.0',
   compiled:{ claude:{version:'1.1.0', date:'2026-07-15', stale:false} }, promptsDone:['proj','decide'], createdAt:'2025-09-01' },
 { id:'e-gb', name:'Backend Engineer', role:'Backend Engineer', seniority:'Mid', team:'t-grad',
   stack:['.NET','SQLite','Postgres'], platforms:['codex'], status:'Ready to Compile', version:'0.3.0',
   compiled:{}, promptsDone:['proj'], createdAt:'2025-09-01' },
];}

function seedTeams(){ return [
 { id:'t-ecom', name:'E-Commerce Team', status:'Package Update Required', version:'3.2.0', stale:true,
   staleReason:'Frontend Engineer brain advanced past v3.2.0', createdAt:'2025-01-10',
   packageDate:'2026-06-12' },
 { id:'t-grad', name:'Graduation Team', status:'Active', version:'1.0.0', stale:false, createdAt:'2025-09-01', packageDate:'2026-07-15' },
];}

function seedSubmissions(){ return [
 { id:'sub-1', eng:'e-net', source:'claude-export-2026-07.json', kind:'Claude conversation export', date:'2026-07-12', candidates:11, accepted:9, rejected:2 },
 { id:'sub-2', eng:'e-net', source:'Extraction prompt — Testing philosophy', kind:'Prompt response', date:'2026-06-20', candidates:6, accepted:5, rejected:1 },
 { id:'sub-3', eng:'e-net', source:'adr-archive.zip', kind:'ZIP (14 markdown files)', date:'2026-05-02', candidates:18, accepted:15, rejected:3 },
 { id:'sub-4', eng:'e-sec', source:'audit-notes-2025.md', kind:'Markdown', date:'2026-04-11', candidates:9, accepted:8, rejected:1 },
 { id:'sub-5', eng:'e-ui', source:'cursor-sessions-may.zip', kind:'Cursor sessions', date:'2026-05-20', candidates:7, accepted:6, rejected:1 },
];}

function seedLog(){ return [
 { id:'L-01', ts:'2026-07-22 16:40', eng:'e-fe', type:'extract', text:'2 candidate beliefs extracted from cursor-session-jul.md', subject:{kind:'tab', eng:'e-fe', tab:'validate'} },
 { id:'L-02', ts:'2026-07-22 14:05', eng:'t-ecom', type:'team', text:'Frontend Engineer brain advanced — E-Commerce team package marked stale', subject:{kind:'team', id:'t-ecom'} },
 { id:'L-03', ts:'2026-07-21 11:32', eng:'e-net', type:'confirm', text:'Belief B-018 confirmed — confidence 0.71 → 0.87', subject:{kind:'belief', id:'B-018'} },
 { id:'L-04', ts:'2026-07-20 09:15', eng:'e-sec', type:'conflict', text:'3 conflicts detected across credential, CVE and pentest policies', subject:{kind:'tab', eng:'e-sec', tab:'brain'} },
 { id:'L-05', ts:'2026-07-18 17:50', eng:'e-net', type:'compile', text:'Compiled v1.4.0 for Claude — 4.2k token core profile', subject:{kind:'tab', eng:'e-net', tab:'compile'} },
 { id:'L-06', ts:'2026-07-15 10:22', eng:'e-ai', type:'compile', text:'Compiled v1.1.0 for Claude', subject:{kind:'tab', eng:'e-ai', tab:'compile'} },
 { id:'L-07', ts:'2026-07-12 15:03', eng:'e-net', type:'extract', text:'11 candidates extracted from claude-export-2026-07.json · 2 duplicates merged', subject:{kind:'tab', eng:'e-net', tab:'evolve'} },
 { id:'L-08', ts:'2026-07-10 13:44', eng:'e-be', type:'compile', text:'Compiled v2.1.0 for Claude', subject:{kind:'tab', eng:'e-be', tab:'compile'} },
 { id:'L-09', ts:'2026-07-02 09:40', eng:'e-ui', type:'import', text:'Import session paused at stage 4 of 8 — Project selection', subject:{kind:'tab', eng:'e-ui', tab:'evolve'} },
];}

const INTERVIEW = [
 { q:'Describe a recent technical decision you are proud of. Why was it right?',
   sample:'Last quarter I replaced our per-service databases split with a modular monolith for the new logistics product. The team wanted microservices from day one, but we had no independent scaling need and a two-person backend team. Keeping one deployable with hard module boundaries let us ship in six weeks instead of four months. The module boundaries are enforced by architecture tests, so the future extraction path stays open without paying the distributed-systems tax today.',
   cands:[ {type:'Decision', domain:'Architecture', statement:'Default to a modular monolith until an independent scaling or deployment need is proven.', ev:0},
           {type:'Pattern', domain:'Architecture', statement:'Module boundaries are enforced by architecture tests so future service extraction stays cheap.', ev:3} ] },
 { q:'When starting a project, what is the first architectural question you ask?',
   sample:'The first thing I ask is what data must be strongly consistent and what can lag. Everything else — storage, messaging, service boundaries — falls out of that answer. On the last project this one question eliminated an event-sourcing design in a single meeting, because the core invariant was transactional. I also ask who owns each piece of data, because shared ownership is where systems rot first.',
   cands:[ {type:'Heuristic', domain:'Architecture', statement:'The first architectural question: which data must be strongly consistent, and which can be eventual?', ev:0},
           {type:'Heuristic', domain:'Architecture', statement:'Every piece of data needs exactly one owner; shared ownership is where systems rot first.', ev:3} ] },
 { q:'What do most engineers do by default that you actively avoid?',
   sample:'Most engineers reach for abstraction before they have three concrete cases. I actively avoid speculative interfaces, generic repositories, and plugin systems for requirements nobody has asked for. Duplication is cheaper than the wrong abstraction, and deleting duplicated code is trivial while unwinding a bad abstraction takes a quarter. I also avoid adding libraries for problems under fifty lines of code.',
   cands:[ {type:'Anti-Pattern', domain:'Implementation', statement:'Abstracting before three concrete cases exist — duplication is cheaper than the wrong abstraction.', ev:0},
           {type:'Preference', domain:'Tooling', statement:'No new library for a problem solvable in under fifty lines of owned code.', ev:3},
           {type:'Heuristic', domain:'Implementation', statement:'Deleting duplication is trivial; unwinding a bad abstraction costs a quarter.', ev:2} ] },
 { q:'What does "good enough" mean before shipping?',
   sample:'Good enough means the failure modes are known and boring. The endpoint has a latency budget asserted in a load test, errors return typed results the client can act on, and the rollback path has actually been executed once. I do not need every edge case handled — I need to know which edge cases are unhandled and have that written down where the on-call engineer will find it.',
   cands:[ {type:'Quality Threshold', domain:'Process', statement:'Ship when failure modes are known and boring: asserted latency budget, typed errors, rehearsed rollback.', ev:0},
           {type:'Principle', domain:'Process', statement:'Unhandled edge cases are acceptable only when documented where on-call will find them.', ev:3} ] },
 { q:'What technical principle do you defend even when others disagree?',
   sample:'History is never deleted. Audit trails, migrations, ADRs, even failed experiments — everything is append-only and searchable. People argue it is clutter, but every serious incident I have debugged was solved by history someone had wanted to delete. Storage is cheap; archaeology without records is impossible. I will trade almost any convenience for a complete record of why the system is the way it is.',
   cands:[ {type:'Principle', domain:'Process', statement:'History is never deleted: audit trails, ADRs and failed experiments are append-only and searchable.', ev:0},
           {type:'Heuristic', domain:'Process', statement:'Storage is cheap; archaeology without records is impossible — keep the complete record of why.', ev:3} ] },
];

const PROMPT_LIB = [
 { id:'proj', cat:'Project experience', desc:'Surface decisions, trade-offs and lessons from your most significant projects.' },
 { id:'style', cat:'Coding style & naming', desc:'Extract your conventions for naming, structure, and readability.' },
 { id:'folder', cat:'Folder organization', desc:'How you lay out solutions, projects and modules — and why.' },
 { id:'arch', cat:'Architecture preferences', desc:'Your defaults for boundaries, data ownership, and system shape.' },
 { id:'test', cat:'Testing philosophy', desc:'What you test, at which layer, and what you refuse to mock.' },
 { id:'error', cat:'Error handling', desc:'How failures are represented, propagated, logged and retried.' },
 { id:'docs', cat:'Documentation style', desc:'What gets written down, where it lives, and when it is required.' },
 { id:'review', cat:'Code review habits', desc:'What you block on, what you let slide, and how you phrase it.' },
 { id:'decide', cat:'Decision making', desc:'How you weigh options, when you commit, and how you record it.' },
 { id:'comm', cat:'Communication style', desc:'How you explain, escalate, disagree, and write to be understood.' },
];
function promptText(cat){
 return `You have full context on my engineering history — our conversations, my code, my reviews, my decisions.\n\nActing as a knowledge archaeologist, extract my real position on: ${cat.toUpperCase()}.\n\nFor each position you find, output:\n- STATEMENT: one sentence, ≤280 chars, in my voice\n- TYPE: Principle | Preference | Decision | Anti-Pattern | Heuristic | Quality Threshold | Workflow | Pattern\n- DOMAIN: Architecture | Implementation | Process | Tooling | Team\n- EVIDENCE: a verbatim 40–100 word quote of mine that proves it, with source and date\n\nOnly include positions with direct evidence. Do not infer, flatter, or generalize. If evidence conflicts, output both sides and flag the conflict.`;
}
const SAMPLE_PASTE = `STATEMENT: Log messages are written for the 3am reader: every error log carries the entity id, the operation, and the next action to take.
TYPE: Principle · DOMAIN: Process
EVIDENCE: "I rewrote the payment worker logs after the March incident — the old ones said 'operation failed' 400 times. Now every line has the payment id, what we were doing, and what to check. On-call time to diagnosis went from 40 minutes to 4." (Incident review, Mar 2026)

STATEMENT: Retries always use exponential backoff with jitter and a hard cap; naked retry loops are rejected in review.
TYPE: Pattern · DOMAIN: Implementation
EVIDENCE: "The retry storm in the notification service DDoSed our own API. Polly policies with jitter and a circuit breaker are now in the platform template — nobody writes a retry loop by hand." (Code review — notifications #218, Feb 2026)

STATEMENT: Every exception that crosses a service boundary is translated to a typed error contract; stack traces never leave the process.
TYPE: Principle · DOMAIN: Architecture
EVIDENCE: "We leaked an internal stack trace into a client-facing error in April. Boundary middleware now maps everything to the error catalogue — codes, not traces." (Security fix #91, Apr 2026)`;

function pasteCandidates(engId, catId){
 const base=[
  { type:'Principle', domain:'Process', statement:'Log messages are written for the 3am reader: every error log carries the entity id, the operation, and the next action.', evidence:'I rewrote the payment worker logs after the March incident — the old ones said "operation failed" 400 times. Now every line has the payment id, what we were doing, and what to check. On-call time to diagnosis went from 40 minutes to 4.', source:'Incident review, Mar 2026' },
  { type:'Pattern', domain:'Implementation', statement:'Retries always use exponential backoff with jitter and a hard cap; naked retry loops are rejected in review.', evidence:'The retry storm in the notification service DDoSed our own API. Polly policies with jitter and a circuit breaker are now in the platform template — nobody writes a retry loop by hand.', source:'Code review — notifications #218, Feb 2026' },
  { type:'Principle', domain:'Architecture', statement:'Every exception that crosses a service boundary is translated to a typed error contract; stack traces never leave the process.', evidence:'We leaked an internal stack trace into a client-facing error in April. Boundary middleware now maps everything to the error catalogue — codes, not traces.', source:'Security fix #91, Apr 2026' },
 ];
 return base.map((b,i)=>Object.assign({ id:`C-P${Date.now()%100000}-${i}`, eng:engId, confidence:0.34+i*0.03, batch:'prompt-'+catId }, b));
}
function uploadCandidates(engId, batchId){
 const base=[
  { type:'Workflow', domain:'Process', statement:'Incidents get a blameless postmortem within 72 hours; action items have owners and dates or they do not exist.', evidence:'The postmortems that changed anything were the ones written while the logs were still warm. After 72 hours it is fiction. And an action item without an owner is a wish.', source:'Postmortem template v2, Jan 2026' },
  { type:'Heuristic', domain:'Architecture', statement:'Design the data model for the queries you will run at 3am, not the entities in the domain diagram.', evidence:'Every incident query joins five tables the ORM designed for purity. I now start schema design from the on-call runbook backwards.', source:'Schema review notes, Feb 2026' },
  { type:'Preference', domain:'Tooling', statement:'One command bootstraps the whole dev environment; if setup needs a wiki page, setup is broken.', evidence:'make dev went from a nice-to-have to the thing I check first in any repo. New joiners commit on day one now instead of day three.', source:'Onboarding retro, Mar 2026' },
  { type:'Quality Threshold', domain:'Process', statement:'Error budgets are agreed with product per feature; when the budget is spent, reliability work preempts the roadmap.', evidence:'The SLO conversation with product changed everything — instead of arguing about "quality time", we spend a budget we both agreed to. When it is gone, the roadmap waits.', source:'Q1 planning notes, Jan 2026' },
  { type:'Anti-Pattern', domain:'Team', statement:'Hero on-call: if one person can fix what nobody else can, that is a bus-factor incident already in progress.', evidence:'When Karim left, three systems became unfixable overnight. Rotation with mandatory shadowing is now non-negotiable, whatever it costs in short-term speed.', source:'Team health review, Apr 2026' },
  { type:'Decision', domain:'Tooling', statement:'Structured logging (Serilog + message templates) everywhere; string interpolation in log calls fails the analyzer.', evidence:'You cannot query what you did not structure. The analyzer rule annoyed everyone for a week and then the first incident query paid for it in one afternoon.', source:'Platform PR #61, Feb 2026' },
 ];
 return base.map((b,i)=>Object.assign({ id:`C-U${Date.now()%100000}-${i}`, eng:engId, confidence:0.3+i*0.04, batch:batchId }, b));
}

const PKG_TREES = {
 claude:[
  { path:'skills/core-identity.md', size:'3.1 KB', content:'# Core Identity — .NET Architect\n\nPrincipal solution architect. Evidence-first, boring-by-design.\n\n- Domain never references infrastructure (B-001)\n- Modular monolith by default (B-002)\n- History is never deleted\n\n## Voice\nDirect, specific, cites incidents and ADRs. Flags confidence on every recommendation.' },
  { path:'skills/architecture.md', size:'4.8 KB', content:'# Architecture Module\n\n## Defaults\n- First question: strong vs eventual consistency (B-003)\n- Outbox for cross-module events (B-008)\n- Readiness-gated deploys (B-024)\n\n## Avoid\n- Generic repositories over EF Core (B-004)\n- Premature service extraction (B-002)' },
  { path:'skills/testing.md', size:'2.9 KB', content:'# Testing Module\n\n- Integration tests at the API boundary with a real database (B-014)\n- p95 budgets asserted in k6, not dashboards (B-013)\n- Migrations tested with rollback scripts (B-010)' },
  { path:'skills/review.md', size:'2.2 KB', content:'# Review Module\n\n- blocking: / nit: prefixes required (B-017)\n- Reject async without CancellationToken (B-022)\n- No AutoMapper in domain (B-012)' },
  { path:'mcp.config.json', size:'0.8 KB', content:'{\n  "mcpServers": {\n    "e3a-brain": {\n      "command": "npx",\n      "args": ["@e3a/brain-server", "--profile", "net-architect"],\n      "env": { "E3A_KEY": "e3a_sk_••••••••" }\n    }\n  }\n}' },
  { path:'INSTALL.md', size:'1.4 KB', content:'# Install — Claude\n\n1. Unzip into your Claude skills directory\n2. Add mcp.config.json entries to your MCP settings\n3. Restart Claude — the engineer announces itself on first message\n\nPackage: {{PKG}} · signed sha256:9f31…c2ae' },
 ],
 cursor:[
  { path:'.cursor/rules/identity.mdc', size:'2.6 KB', content:'---\ndescription: Engineer identity — always applied\nalwaysApply: true\n---\nYou operate as this engineer. Evidence-first. Cite belief ids when applying judgment.' },
  { path:'.cursor/rules/architecture.mdc', size:'3.4 KB', content:'---\nglobs: ["**/*.cs"]\n---\n- Enforce dependency direction (B-001)\n- Prefer Result<T> over exceptions for expected failures (B-018)' },
  { path:'.cursor/rules/testing.mdc', size:'2.1 KB', content:'---\nglobs: ["**/*Tests*/**"]\n---\n- API-boundary integration tests with Testcontainers (B-014)\n- No mock-everything suites (B-015 retired)' },
  { path:'INSTALL.md', size:'1.1 KB', content:'# Install — Cursor\n\n1. Copy .cursor/ into the repository root\n2. Reload the window\n3. Rules apply automatically by glob' },
 ],
 codex:[
  { path:'AGENTS.md', size:'4.4 KB', content:'# AGENTS.md — Engineer profile\n\n## Identity\nEvidence-first engineer. Every recommendation carries confidence and provenance.\n\n## Hard rules\n- Rollback rehearsed before deploy\n- Idempotent, checkpointed jobs' },
  { path:'skills/profile.skill.json', size:'2.0 KB', content:'{\n  "name": "engineer-brain",\n  "version": "1.0.0",\n  "entry": "AGENTS.md",\n  "modules": ["core","architecture","testing","review","deploy","avoid"]\n}' },
  { path:'INSTALL.md', size:'1.0 KB', content:'# Install — Codex\n\n1. Place AGENTS.md at repo root\n2. Register skill package: codex skills add ./skills\n3. Verify with: codex profile show' },
 ],
 antigravity:[
  { path:'adapter/e3a.adapter.yaml', size:'1.9 KB', content:'adapter: e3a-brain\nstatus: experimental\ninject:\n  system: profile/core.md\n  context: profile/modules/*.md\nrefresh: on-session-start' },
  { path:'profile/core.md', size:'3.0 KB', content:'# Core profile\nCompiled from active beliefs. Do not edit — recompile from E3A.' },
  { path:'INSTALL.md', size:'0.9 KB', content:'# Install — AntiGravity (experimental)\n\n1. Drop adapter/ into ~/.antigravity/adapters\n2. Enable in Settings → Experimental → External identity\n3. Restart the workspace' },
 ],
 mcp:[
  { path:'mcp.config.json', size:'0.9 KB', content:'{\n  "mcpServers": {\n    "e3a-brain": {\n      "url": "https://mcp.e3a.dev/brain",\n      "headers": { "Authorization": "Bearer e3a_sk_••••••••" }\n    }\n  }\n}' },
  { path:'SCOPES.md', size:'0.7 KB', content:'# Scoped key\n\nKey grants read-only access to:\n- beliefs.read (active only)\n- profile.read\nNo write scopes. Rotate in Settings → API & MCP.' },
  { path:'INSTALL.md', size:'0.8 KB', content:'# Install — Generic MCP\n\n1. Add the server block to your client MCP config\n2. Paste your scoped key\n3. Call tool brain.identity to verify' },
 ],
};
const INSTALL_GUIDES = {
 claude:[ {t:'Download and unzip the skill package', c:'unzip {{PKG}} -d ~/claude/skills/'}, {t:'Merge the MCP config into your Claude settings', c:'cat mcp.config.json >> ~/.claude/mcp.json'}, {t:'Restart Claude and verify the engineer announces itself', c:null}, {t:'Ask it: "what do you believe about testing?" — it should cite B-014 with evidence', c:null} ],
 cursor:[ {t:'Copy the rules folder into your repository root', c:'cp -r .cursor/ /path/to/repo/'}, {t:'Reload the Cursor window', c:null}, {t:'Rules apply automatically by glob — check Settings → Rules', c:null} ],
 codex:[ {t:'Place AGENTS.md at the repository root', c:'cp AGENTS.md /path/to/repo/'}, {t:'Register the skill package', c:'codex skills add ./skills'}, {t:'Verify the profile is active', c:'codex profile show'} ],
 antigravity:[ {t:'Drop the adapter into your adapters directory', c:'cp -r adapter/ ~/.antigravity/adapters/e3a'}, {t:'Enable Settings → Experimental → External identity', c:null}, {t:'Restart the workspace — experimental: expect rough edges', c:null} ],
 mcp:[ {t:'Add the server block to your client MCP config', c:null}, {t:'Paste your scoped key (read-only scopes)', c:'export E3A_KEY=e3a_sk_...'}, {t:'Verify with the identity tool', c:'mcp call brain.identity'} ],
};

window.E3A_DATA = { PLATFORMS, DOMAINS, TYPES, MODULES, seedBeliefs, seedCandidates, seedEngineers, seedTeams, seedSubmissions, seedLog, INTERVIEW, PROMPT_LIB, promptText, SAMPLE_PASTE, pasteCandidates, uploadCandidates, PKG_TREES, INSTALL_GUIDES };

})();
