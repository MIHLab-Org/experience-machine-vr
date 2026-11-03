# Quest readiness checklist (contest build)

Task V2, written 2026-11-03 by a scheduled run. Replaces D3. Covers what a **release-channel build for the Meta VR Start Developer Competition 2026** needs, not a full Horizon Store submission.

Status: **met** · **unmet** · **unknown** (needs a check in the Editor or on a headset). Owner: **jim** (Editor, headset, accounts) · **auto** (a scheduled run can do it).

How the project values were read: from `ProjectSettings/ProjectSettings.asset` and `Packages/manifest.json` on `claude/auto` (read-only; runs never edit these). Re-check after V3, because Meta's Project Setup Tool will change many of them.

## A. Contest rules

Source: the contest summary in `CLAUDE_BACKLOG.md` (Jim, 2026-11-01) and https://start-developer-competition-26.devpost.com/rules. This run could not open the rules page, so re-read it once before V14, especially the form fields and the judging criteria.

| # | Requirement | Status | Owner | Notes |
|---|---|---|---|---|
| A1 | Whole experience usable with **hands only**, no controller, start to finish | unmet | auto + jim | V5–V8 write the hand paths, V9 audits them, V10 proves them on a Quest. Today dialogue needs **F** and talking needs **E** (keyboard). |
| A2 | Build delivered as an APK in a release channel named exactly **Competition**, with an invite URL | unmet | jim | V14. Needs a Meta developer org and an app in the Developer Dashboard first; create the app early (it can take a day to set up). |
| A3 | Video **under 3 minutes**, filmed in the headset or the Meta XR Simulator, public on YouTube or Vimeo | unmet | auto + jim | V13 plans it (target 2:50), Jim records in V14. |
| A4 | Devpost form: name, tagline, description, how it was built, what's next, division, track | unmet | auto + jim | V12 drafts, Jim rewrites. Confirm the exact field list on Devpost. |
| A5 | Division: likely **Adapted / Significantly Updated** (project predates Sep 24) — needs a "what's new since Sep 24" section | unknown | jim | Decide in V4. |
| A6 | Track: Entertainment or Gaming | unknown | jim | Decide in V4. |
| A7 | Deadline Tue **Nov 18, 12:00 pm PST**; aim to submit Mon Nov 17 | — | jim | Keep the build and invite URL live through judging. |

## B. Meta technical requirements for a Quest build

Sources: Meta's [Unity "prepare for publish"](https://developers.meta.com/horizon/documentation/unity/unity-prepare-for-publish/) page (IL2CPP, ARM64 only, min API 29+, target API automatic, unique package name, version code) and the [Android 12L blog post](https://developers.meta.com/horizon/blog/meta-quest-apps-android-12l-june-30/) (new apps must target API 32). Rows marked *VRC* are from Meta's [VRC guidelines](https://developers.meta.com/horizon/resources/publish-quest-req), which this run could not load in full; treat them as "verify in the Project Setup Tool". Release channels are less strict than the public store, but a build that meets these will not get rejected at upload.

| # | Requirement | Project today | Status | Owner | Fix |
|---|---|---|---|---|---|
| B1 | Build target **Android** | Project is set up for WebGL (web edition); Android settings exist but the platform has not been switched | unmet | jim | V3: File > Build Profiles > Android > Switch Platform. |
| B2 | Scripting backend **IL2CPP** | `scriptingBackend: Android: 1` (IL2CPP) | met | — | — |
| B3 | Target architecture **ARM64 only** (store accepts only 64-bit) | `AndroidTargetArchitectures: 2` (ARM64) | met | — | — |
| B4 | Minimum API level **29 or higher** (Meta's Unity page); the Meta XR SDK may ask for higher — accept whatever the Project Setup Tool sets | `AndroidMinSdkVersion: 26` | unmet | jim | V3: Player > Other Settings > Minimum API Level, or let the Project Setup Tool fix it. |
| B5 | Target API level **32 (Android 12L)** or "Automatic (highest installed)" | `AndroidTargetSdkVersion: 0` (Automatic) | met | — | Make sure the installed Android SDK includes API 32+ (Unity Hub's Android module does). |
| B6 | Unique package name `com.Company.App` | `com.UnityTechnologies.com.unity.template.urpblank` (template default) | unmet | jim | V3: set e.g. `org.mihlab.experiencemachine`. Must match the app in the Developer Dashboard and never change after the first upload. |
| B7 | Version code goes up with every upload | `AndroidBundleVersionCode: 1`, version `0.1.0` | met (first upload) | jim | Raise it by 1 before every later upload to the Competition channel. |
| B8 | **Meta XR SDK v81+** (All-in-One, or Core + Interaction) and XR Plug-in Management with OpenXR + Meta Quest feature group (or Oculus plug-in) | No `com.meta.*`, `com.unity.xr.*` or OpenXR packages in `Packages/manifest.json` | unmet | jim | V3. |
| B9 | **Hand tracking** declared in the Android manifest (`com.oculus.permission.HAND_TRACKING` permission and the `oculus.software.handtracking` feature) | No custom manifest in `Assets/Plugins/Android/` | unmet | jim | V3: enable Hand Tracking Support ("Controllers and Hands", or "Hands Only") in the Meta XR / OVRManager settings; the SDK generates the manifest entries. Check the built manifest in the Project Setup Tool. |
| B10 | Graphics API: Vulkan first (Meta's recommended default) | Android list: Vulkan, then OpenGLES3 | met | — | Leave as is unless the Project Setup Tool says otherwise. |
| B11 | Texture compression **ASTC** for Android | Android default format ASTC | met | — | — |
| B12 | Stereo rendering: Single Pass Instanced / Multiview | Not set (no XR loader yet) | unknown | jim | V3: set in the OpenXR / Oculus loader settings; the Project Setup Tool flags it. |
| B13 | Frame rate: hold the headset's refresh rate (72 Hz minimum on Quest) with no sustained drops *VRC* | Untested; the scene uses ~1 GB on WebGL and has a large terrain | unknown | jim | V10: watch the OVR Metrics Tool overlay during the slice. If it drops, reduce terrain detail/draw distance and trees in `VR_Slice` first. |
| B14 | No crash during a full playthrough of the slice; app launches and quits cleanly *VRC* | Untested | unknown | jim | V10. |
| B15 | Comfort: no forced camera rotation, comfort option for any artificial movement *VRC* | Walk-along movement not written yet | unmet | auto | V8: auto-follow must not rotate the view, with a vignette toggle and a hand stop; or teleport. |
| B16 | Recentering / Meta (system) menu works; app pauses when the headset menu opens *VRC* | Untested | unknown | jim | V10; the camera rig from Building Blocks handles recentering by default. |
| B17 | Hand-tracking loss handled (hands disappear cleanly, UI still reachable when they return) | Untested | unknown | jim | V10. |
| B18 | Privacy policy URL in the Developer Dashboard | None published (D4 parked) | unknown | jim | Release channels may not ask for one; if the dashboard requires it to create the app, unpark D4 (a scheduled run can draft it) and publish on mihlab.org. |
| B19 | APK signed with a release keystore (not the debug key) | No keystore set | unmet | jim | V14: Player > Publishing Settings > create a keystore; back it up — every later upload must use the same key. |
| B20 | App icon, name and store/dashboard art | Product name "The Experience Machine", company "MIHLab.org"; no icons | unknown | jim | Release-channel builds need only the dashboard basics; check what the dashboard asks for when creating the app. |

## C. Summary

- **Met (6):** IL2CPP, ARM64, target API automatic, first version code, Vulkan, ASTC.
- **Unmet, Jim (V3/V14):** Android platform, min API, package name, Meta XR SDK + OpenXR, hand-tracking manifest, release keystore, Competition channel, video, form.
- **Unmet, runs:** hands-only interaction (V5–V9), comfort movement (V8), submission and video drafts (V12, V13).
- **Unknown until a headset test (V10):** frame rate, crash-free run, recentering, hand loss, stereo mode, privacy policy URL.

Biggest risk to the deadline: frame rate (B13). The forest scene was built for desktop WebGL; check it on the Quest as soon as V3's hello-world build works, not at V10.
