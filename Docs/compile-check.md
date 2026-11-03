# Compile check (CI)

`.github/workflows/compile-check.yml` tells you, after every push, whether the project's scripts still compile for the Quest (Android). Scheduled runs can't open Unity, so this is how they (and you) find out that a script they wrote doesn't compile.

## What it does

- **When:** every push to `claude/auto` or `main`, and by hand (Actions tab > **Compile check** > Run workflow). A newer push to the same branch cancels the older run.
- **compile job:** checks out the repo (with LFS), restores the cached `Library` folder, and opens the project in Unity batch mode (GameCI `unity-builder`, Unity version from `ProjectSettings/ProjectVersion.txt`) with the **Android** build target. It calls `CompileCheck.Run` (`Assets/Editor/CompileCheck.cs`), which does nothing but exit. Unity has to compile every script before it can call that method, and aborts with an error if any script fails, so the job goes red on any compile error. No build, no APK, no deploy.
- **summary job:** reads the compile job's log and writes every `error CS` line (file, line, message) into the run's summary page, so you don't have to dig through the Unity log.

## Setup in `MIHLab-Org/experience-machine-vr` (Jim)

A fork does not copy secrets. Add the same three secrets the Deploy WebGL workflow uses, in the new repo: **Settings > Secrets and variables > Actions > New repository secret**:

| Secret | Value |
|---|---|
| `UNITY_LICENSE` | contents of your `.ulf` licence file (same as the old repo) |
| `UNITY_EMAIL` | your Unity account email |
| `UNITY_PASSWORD` | your Unity account password |

Also check **Settings > Actions > General** allows workflows to run in the fork (GitHub disables Actions on forks until you enable them: Actions tab > "I understand my workflows, go ahead and enable them").

Then run it once by hand and check it goes green. `SITE_DEPLOY_TOKEN` is **not** needed here.

## Caveats

- The first run has no `Library` cache and switching to Android re-imports every asset, so it can take 30-60 minutes. Later runs reuse the cache (keyed on `Packages/packages-lock.json` and the Unity version).
- It needs GameCI's **Android** Docker image for Unity 6000.5.7f1. If the job fails with "image not found" (the version is very new), the summary says "failed for another reason"; tell Claude and the workflow can pin a nearby version instead.
- A red run with no `error CS` lines means something other than code: licence activation, disk space, or the image. Open the compile job's log.
- It only checks compilation. It does not catch runtime errors, missing references in scenes, or Meta SDK setup problems.
- Until the Meta XR SDK is installed (V3), scripts that use Meta types would fail here; V5-V8 are written to compile without it.
