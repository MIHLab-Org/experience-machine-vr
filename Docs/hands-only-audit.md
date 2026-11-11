# Hands-only audit of the contest slice

Task V9, written 2026-11-11 in an interactive session (Jim asked). It covers the `VR_Slice` scene as committed on `claude/auto` at `b1d5a1c`, plus the saved scene from the morning of 2026-11-11.

It walks through the slice from launch to quitting. For each step it lists how a player does it with hands only, and which script or setting covers it. Every step that still needs a key, a mouse or a controller, or that has no hand path at all, is a **gap** with a fix task. The audit was written from the scene, the scripts and the Yarn files, not on a headset, so anything marked **check on Quest** is for V10.

**How hands work in this build:**
- **Pointing at and pressing UI:** you point a hand ray and pinch your thumb and index finger to "click". This is Meta's Ray Canvas Interaction, on every Talk button and on the dialogue panel. Poke (pressing with a fingertip) isn't used: the Talk button floats about 2.15 m up and the panel sits about 1.2 m away, both out of finger reach.
- **Moving during walks:** you're carried alongside the philosopher (`WalkAlongMover`, V8), with a comfort vignette.
- **System gestures:** opening the Quest menu and recentering are handled by the Quest itself.
- **Editor-only keys don't count:** the keyboard walking and dialogue keys in `EditorKeyboardMove` (WASD, Space, 1–9) exist only in the Editor and are never in the build, so this audit ignores them.

## Walkthrough

| # | Step | What the player does with hands | Covered by | Status |
|---|---|---|---|---|
| 1 | Launch | Pinch the app tile in the Quest library | Quest OS | hand path |
| 2 | App opens in the slice | Nothing; `VR_Slice` is the first scene in the build list | `ProjectSettings/EditorBuildSettings.asset` | hand path |
| 3 | Hands are tracked | Put the controllers down; the hands appear | Meta Camera Rig + Interaction rig Building Blocks; Hand Tracking Support "Controllers and Hands", frequency High (`Assets/Oculus/OculusProjectConfig.asset`, Android manifest) | hand path · check on Quest |
| 4 | First view | Stand in the forest about 5 m from Socrates | Player rig on the terrain next to Socrates (fixed 2026-11-10) | hand path · check on Quest which way the player faces at the start |
| 5 | Reach Socrates | Get within 3 m: he starts on approach (`joinOnApproach`, trigger radius 3 m), or within 4 m and facing him for the Talk button | `PhilosopherWalker` on Socrates, `HandTalkPrompt` | **gap G2**: the player starts 4.9 m away, so they must step forward in the room. There's no hand way to move yet (see G3). |
| 6 | Socrates' opening | Listen and read | Socrates' join node | **gap G1**: his join node is `Start` (the Phaedrus walk to the plane tree in `technology.yarn`), which never reaches `Talk_Socrates` → `Map_Start` → `Contest_Question`. The contest route can't be reached. |
| 7 | Next line | Point at **Continue** and pinch | `WorldSpaceDialoguePanel` (V6) Continue button; Meta Ray Canvas Interaction on the Dialogue System canvas; Yarn `LineAdvancer` input set to Manual, so no key skips lines | hand path · check on Quest that the panel is readable and follows lazily |
| 8 | Answer the contest question | Point at an answer and pinch | Yarn `OptionItem` buttons on the same canvas | hand path |
| 9 | Walk with Socrates to a church father (92–134 m) | Nothing: you're carried at walking pace and the comfort vignette fades in | `<<handoff>>` (`YarnWalkCommands`), `WalkAlongMover` (V8) | hand path · **gap G4**: `WalkAlongMover.Stop()` / `Toggle()` exist but no hand gesture or button calls them, so the player can't stop a walk they find uncomfortable. |
| 10 | Talk with the father | Continue and answers, as in 7–8 | `Talk_GregoryOfNyssa` / `Talk_Augustine` / `Talk_Chrysostom` (V18–V20) | hand path |
| 11 | Handed on to the contemporary philosopher (3.5 m) or to the next father (about 100–180 m) | Carried as in 9 | `<<handoff>>`, `WalkAlongMover` | hand path (G4 applies) |
| 12 | Grove gate and quiz | Answer the quiz and get the passport stamp | `Gate_*` → `Quiz_*` nodes | hand path |
| 13 | Talk with Heim, Bostrom or Nozick | Continue and answers | `Talk_Heim` / `Talk_Bostrom` / `Talk_Nozick` | hand path |
| 14 | Ending | The conversation ends at `<<stop>>` (`Done_*` nodes) | — | **gap G5**: no closing moment. The player is left standing, with no word on what happened or what to do next. |
| 15 | Talk to other philosophers on the way | Only the four leads, Socrates and the three fathers have Talk buttons; the other philosophers start only with the **E** key | `PhilosopherNPC` (`requireKeyPress`) | **gap G6**: keyboard only. Outside the contest route, but judges may try them. |
| 16 | Move freely between groves | — | V15 (wrist Walk button) not written yet. Meta's hand teleport is in the rig, but its targets (the "Teleport NavMesh" Building Block) are turned off, so the arc lands nowhere. | **gap G3** |
| 17 | Turn | Turn your body; Meta's hand snap-turn is in the rig | Interaction rig locomotion | hand path · check on Quest that snap-turn doesn't fire by accident while pinching answers |
| 18 | Recenter | Look at your palm, pinch and hold | Quest OS | hand path · check on Quest |
| 19 | Open the Quest menu / pause | Look at your palm and pinch | Quest OS; the app should pause on loss of focus | check on Quest (readiness B16) |
| 20 | Hands lost and found again | Hands disappear; UI still works when they return | Meta rig | check on Quest (readiness B17) |
| 21 | Quit | From the Quest menu | Quest OS | hand path |

## Gaps and fix tasks

| Gap | Problem | Fix | Task | Who |
|---|---|---|---|---|
| G1 | Socrates starts the Phaedrus walk (`Start`), not the contest question | In `VR_Slice`, set Socrates' `PhilosopherWalker` Join Node to `Talk_Socrates` (which jumps to `Map_Start` and then `Contest_Question` the first time). The Phaedrus walk stays in the file for the web edition. | V22 | local |
| G2 | The player starts 4.9 m from Socrates and can't move with hands | Move the player start to about 2.5 m from Socrates, facing him, so he starts talking by himself | V23 | local |
| G3 | No free movement between groves | V15 (wrist Walk button), already in the backlog. Until then, either turn on Meta's hand teleport (activate the "Teleport NavMesh" Building Block and bake its surface) or turn off the teleport interactors, so the arc doesn't promise something that doesn't work. | V15 + V24 | auto + local |
| G4 | No hand way to stop a walk | A small "Stop" button on the comfort vignette canvas or on the dialogue panel that calls `WalkAlongMover.Toggle()`. Plain Unity UI, pressable with the same ray-pinch. | V25 | auto |
| G5 | No ending | A short `Slice_End` node in `ChurchFathers.yarn` that the three contemporary talks reach instead of stopping silently. Socrates (or the father) sums up which question the player chose and points at the other two groves. Jim adds the jump in his `Done_*` nodes, or a run adds it to new nodes only. | V26 | auto + jim |
| G6 | Philosophers outside the route need the E key | Select them and run **MIHLab → Make scene hand-ready**, then Meta's **Add Ray Interaction to Canvas** on each new Talk canvas | V27 | local |

**Priority for the contest:**
- G1 blocks the whole route.
- G2 decides what the judges' first five minutes look like.
- G4 is a comfort requirement (readiness B15).
- G5 matters for "Polish & Presentation".
- G3 and G6 can wait if time runs out, as long as the teleport arc is switched off.
