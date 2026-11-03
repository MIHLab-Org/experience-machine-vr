# Scheduled Run Instructions — Experience Machine

Runs happen in a cloud session against the GitHub repo, not on Jim's computer. Every scheduled run starts with no memory of earlier runs. This file and `CLAUDE_BACKLOG.md` are the whole briefing. Follow them in order and stop at the first guard that fails.

## 0. Ground rules

- Do exactly **one** backlog task per run. Never start a second, even if time remains.
- Only take tasks marked `todo`, `auto`, size `S`, with every dependency `done`. Never take `parked` tasks.
- Work on the git branch `claude/auto`. Never commit to `main`. Never force-push. Never delete files; if something should go, list it under "Notes from Jim" instead.
- Never edit `.unity` scene files, `.prefab` files, `ProjectSettings/`, `Packages/manifest.json`, or anything in `Library/`. Scene changes are done through Editor scripts Jim runs himself.
- Never run Unity builds, never open the Unity Editor, never touch store dashboards, Stripe, Supabase, or any account.
- Do not spawn subagents. Do at most 2 web searches per run, and only if the task needs a current fact.
- Do not edit Jim's own prose (dialogue he wrote, notes, docs he authored). New dialogue goes in new `.yarn` files.
- No copyrighted text from *Reality+* or any other book in dialogue.

## Repository changes

A scheduled task can only push to the repositories that were attached when it was created; naming a repo in its prompt doesn't grant access. So runs never create, fork, rename or transfer repositories, and never switch to a different repo. Whenever the project moves to another repository (a new fork, a rename, a new org), whoever makes the move must also recreate the scheduled task from a session that has the new repo selected, disable the old task, and update the repo named in the task prompt and in CLAUDE_BACKLOG.md. Runs check write access before doing any work (task prompt step 2).

## 1. Guards — check all of these before doing any work

Clone the repo, then `git fetch origin claude/auto` and check out `claude/auto` if it exists (otherwise stay on `main`). Read `.claude-runs/ledger.md` and `CLAUDE_BACKLOG.md` from that branch. The ledger on `claude/auto` is the authoritative one. Log skipped runs there too (commit + push only the ledger change).

| Guard | Rule | If it fails |
|---|---|---|
| G1 Monthly cap | Count rows for the current month whose result is `review` or `blocked` (skipped rows do not count). If the count ≥ **MONTHLY_RUN_CAP** (set below), stop. | Append a `skipped: monthly cap` row. End. |
| G2 Weekly cap | If ≥ **WEEKLY_RUN_CAP** completed runs in the last 7 days, stop. | Append `skipped: weekly cap`. End. |
| G3 Review queue | If **REVIEW_QUEUE_CAP** or more backlog tasks are in `review`, stop — Jim hasn't caught up, and more work would just pile up. | Append `skipped: review queue full`. End. |
| G4 Unmerged pile-up | If `claude/auto` is more than **UNMERGED_CAP** task commits ahead of `main` (`git rev-list --count origin/main..claude/auto`, counting only `auto:` commits), Jim hasn't merged recent work: stop. | Append `skipped: unmerged work piling up`. End. |
| G5 Nothing eligible | If no task meets the rules in §0, stop. | Append `skipped: no eligible task`. End. |
| G6 Pause switch | If a file named `.claude-runs/PAUSE` exists on `main` or `claude/auto`, stop. (Jim pauses runs by committing an empty `PAUSE` file on main.) | Append `skipped: paused`. End. |

**Compile check first:** after the guards pass, read the latest **Compile check** workflow run on `claude/auto` (GitHub MCP `actions_list`; the summary job lists the `error CS` lines). If it is red with compiler errors, this run's task is fixing them (log it as `CC <date> fix compile errors`, commit message `auto: CC fix compile errors`), not a backlog task. If it is red for another reason (licence, image, disk) or has never run, note that in the ledger row and carry on with the backlog. Setup: `Docs/compile-check.md`.

Settings (Jim edits these):

```
MONTHLY_RUN_CAP   = 20
WEEKLY_RUN_CAP    = 7
REVIEW_QUEUE_CAP  = 3
UNMERGED_CAP      = 6
```

Raised 2026-11-01 (was 9 / 2 / 2 / 4) for the Meta VR contest sprint, about one run a day until Nov 18. Jim: lower them again after the contest.

**Honest limit:** a run cannot see the actual plan-usage meter. These caps are a proxy: they bound how many runs happen and keep each run small. Check the real usage in Claude's settings after the first two weeks and lower the caps if runs are eating more than you want.

## 2. Do the task

1. `git checkout claude/auto` (create it from `main` if missing; if it exists, merge `main` into it first; if that merge conflicts, abort the merge, log `stopped: merge conflict`, and end).
2. Mark the task `doing` in `CLAUDE_BACKLOG.md`.
3. Do the task. Read only the files it needs.
4. Self-check, keeping it short:
   - C# — re-read the file once for undeclared types, missing `using` lines, and Unity API misuse.
   - Yarn — run `python Tools/validate_yarn.py` if it exists.
   - If a check fails and one fix attempt doesn't solve it, stop: set the task to `blocked`, write what went wrong in the Notes column, commit what you have.
5. Mark the task `review` (or `blocked`). If the task finishes a whole track step that needs Jim, say which `jim` task is now unblocked.
6. Commit on `claude/auto` with message `auto: <ID> <short title>`. Push the branch.
7. Update the task's GitHub issue, if it has one (the number is in the track heading, or in the task text for bugs). Track 0 (V tasks) has no issues: skip this step for them. If the task reached `review`, tick its checkbox in the issue's **Tasks** list (edit the issue body; change only that one `- [ ]` to `- [x]`). If it is `blocked`, leave the box unticked and add one short comment saying why. Never close, reopen, retitle or relabel issues; Jim closes them after merging.

## 3. Log and report

Append one row to `.claude-runs/ledger.md`:

```
| date | task ID | result (review / blocked / skipped: reason) | files changed | one-line summary |
```

Then end with a short summary for Jim: the task, the result, and what he needs to do next (if anything). Nothing more. To review: Jim looks at the `claude/auto` branch on GitHub (or opens a pull request from it into `main`), and merges when happy. Runs never open or merge pull requests themselves.

## Ledger header (for creating the file)

```
# Run ledger
| date | task | result | files | summary |
|---|---|---|---|---|
```
