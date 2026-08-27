---
description: Scaffold a new .NET solution from the ddd-sln template, then verify it restores, builds, tests, and has no vulnerable packages
argument-hint: <SolutionName> [output-dir]
allowed-tools: Bash, Read, Write, Edit, Grep, Glob, AskUserQuestion
---

Scaffold a new solution named **$ARGUMENTS**.

`dotnet new` does the generation. You do the deciding, the verifying, and the fixing. Never hand-copy or hand-rename template files — if the template is broken, say so and stop.

## 1. Gather inputs

Required: solution name, output directory. Take them from `$ARGUMENTS` when present.

Ask (one `AskUserQuestion`, only for what is still unknown, only where the template exposes a matching symbol): first module name · auth mode · Docker yes/no.

Validate before generating:

- Name matches `^[A-Za-z_][A-Za-z0-9_.]*$` — it becomes a namespace root.
- Name is not a reserved word or an existing package on the default feed that you would shadow.
- Output directory does not exist, or is empty. **Never pass `--force`.** A non-empty target is a stop, not a prompt.

## 2. Ensure the template is installed

```bash
dotnet new list ddd-sln
```

Missing → install from the template root, then re-check. If it still does not appear, stop and report — do not fall back to copying files.

## 3. Generate

```bash
dotnet new ddd-sln -n <Name> -o <dir> [--auth <mode>] [--docker <bool>] [--ModuleName <Module>]
```

Pass only symbols the template actually declares. Read its `template.json` to confirm names rather than guessing flags.

Then confirm the shape is real: solution file present, project count > 0, no occurrence of the template's `sourceName` left anywhere in the output. A leftover `sourceName` means the template's rename is incomplete — report it as a **template bug**, with file and line. Do not patch the generated output to hide it.

## 4. Verify

In order. Each gate must pass before the next runs.

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build          # skip if the template ships no test project
dotnet list package --vulnerable --include-transitive
dotnet list package --deprecated
```

Capture the verbatim output of each. Report what you observed — never a green build you did not see.

## 5. Fixing — bounded, and different per failure class

### Build and test failures

Fix and re-run. **Maximum 3 rounds.** Still failing after 3 → stop, report the shortest decisive error line, the diagnosis, and what you tried. Do not start a 4th.

Fix the generated solution only. Never edit the installed template to make one generation pass — that silently breaks every future one. If the root cause is in the template, say so explicitly and fix it there deliberately, as a separate step the user approves.

### Vulnerable packages

**Always report before changing anything.** For each: package · current version · severity · first fixed version · direct or transitive.

Then:

- **Direct dependency, patch or minor bump** → apply it, re-run restore/build/test, keep it only if all three still pass. Revert and report if not.
- **Direct dependency, major bump** → stop and ask. Major bumps carry breaking changes; that is the user's call, not yours.
- **Transitive** → do not bump the parent blindly. Report the dependency path (`dotnet nuget why <project> <package>`) and propose either a central version pin or a direct reference at the fixed version. Apply only after the user picks.
- **No fixed version exists** → report it. Do not remove the package, do not downgrade around it, do not suppress the warning with `NoWarn`.

Never silence an audit warning to make a build go green. Suppressing `NU1901`–`NU1904` is prohibited.

### Deprecated packages

Report only. Not a build gate.

## 6. Finish

```bash
git init
git add -A
git commit -m "chore: scaffold <Name> from ddd-sln template"
```

Then report:

- Command line used, and the resolved symbol values
- Project count and the solution path
- Verbatim outcome of restore, build, test — including counts
- Vulnerabilities found, what you changed, what you left for the user
- Anything you could not resolve, with the decisive error line

Keep it short. The user can open the solution.

## Rules

- The template is the single source of truth for structure. If generation is wrong, the fix belongs in `template.json`, not in the output.
- Never claim a build or test run you did not execute.
- Never exceed 3 fix rounds without handing control back.
- Never change target framework, remove a package, or suppress a warning to reach green.
