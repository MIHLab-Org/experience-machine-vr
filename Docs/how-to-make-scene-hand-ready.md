# How to make a scene hand-ready (V7)

The menu **MIHLab → Make scene hand-ready** prepares the open scene for hands-only play on the Quest. It never deletes anything. Every change goes through Undo, so Ctrl+Z reverts the whole run.

Run it in **VR_Slice**, not SampleScene. SampleScene stays the flat-screen version.

## The clicks

1. Open `VR_Slice`.
2. In the Hierarchy, select the philosophers players should talk to. For the contest slice, that's Socrates, the three church fathers, Heim, Bostrom and Nozick. Selecting a parent object selects every philosopher under it.
3. Run **MIHLab → Make scene hand-ready**. The Console lists every change, each line starting with `[MIHLab hand-ready]`.
4. The menu then selects every canvas that still needs Meta's hand setup. For each one:
   - Right-click it in the Hierarchy and choose **Interaction SDK → Add Poke Interaction to Canvas**, so a fingertip can press the buttons.
   - Right-click it again and choose **Interaction SDK → Add Ray Interaction to Canvas**, so a pinch can press them from a distance.
   - If the wizard shows a **Fix** button (for example, "EventSystem needs a PointableCanvasModule"), click it.
5. Save the scene.

You can run the menu again at any time. It skips what's already done and only reports which canvases still need step 4.

## What it changes

| What | Change |
|---|---|
| Dialogue canvas (the Canvas above Yarn's LinePresenter) | Adds `WorldSpaceDialoguePanel` (V6). Sets it to **World Space** now, 0.9 m wide, about 1.2 m in front of the camera, so Meta's quick actions can be applied in the Editor. At run time the panel follows the player's head. The Canvas Scaler is disabled, since it does nothing in World Space. |
| Each selected philosopher | Adds `HandTalkPrompt` (V5) and builds its **Talk** button in the scene (child object `TalkPrompt Canvas`), so the button exists before Play and can get Meta's poke and ray setup. At run time it shows only when you are within 4 m and looking at the philosopher. |
| Meta Interaction SDK | Checked by type name. The menu doesn't add Meta components itself: a PointableCanvas on its own doesn't let hands press anything. Meta's two quick actions add it together with the poke and ray parts and their surfaces. |

## Checking it

- **In the Editor, no headset:** press Play and walk up to Socrates. The Talk button appears above him, and clicking it with the mouse starts the conversation. The dialogue panel floats in front of you with a Continue button.
- **On the Quest:** the Talk button and the dialogue buttons press with a fingertip (poke), or by pointing and pinching (ray).

## If something looks wrong

- **The Talk button sits at the philosopher's feet in the Scene view:** that's only its resting spot. It moves above the head at run time.
- **The dialogue canvas looks huge or tiny in the Scene view:** check the Console line for its size. WorldSpaceDialoguePanel resizes and moves it at run time, so judge it in Play mode.
- **"No Yarn dialogue canvas found":** the Dialogue System prefab isn't in the open scene, or it has no LinePresenter.
