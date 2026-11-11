# Devpost submission draft: Meta VR Start Developer Competition 2026

Task V12, drafted 2026-11-11 in an interactive session (Jim asked). **Draft only: Jim rewrites it in his own voice before submitting.** Items marked **[Jim]** need a decision or a fact only Jim has. Re-check the exact form fields on Devpost before pasting (readiness A4).

## Form fields

| Field | Draft |
|---|---|
| Project name | Peripatos: The Experience Machine |
| Tagline (≤ 140 characters) | Walk with Socrates through a forest of philosophers and decide, with your own hands, whether a virtual world can be real. *(121 characters)* |
| Track | Entertainment (Jim, V4) |
| Division | Adapted / Significantly Updated (Jim, V4; the project predates Sep 24) |
| Target launch date | **[Jim]** |
| Team | Montaque "Jim" Reynolds, MIHLab (Moral Imagination and Hope Laboratory). **[Jim]** add anyone else, and decide whether to mention the AI tools used to build it |
| Build | Invite URL to the **Competition** release channel **[Jim, V14]** |
| Video | YouTube/Vimeo link, under 3 minutes **[Jim, V14; plan in `video-plan.md`]** |

Other tagline options, if the first one doesn't sound like you:
- Walk and talk with Socrates and the church fathers in a VR forest, and decide with your own hands whether a virtual tree is real. *(129)*
- A hands-only VR walk with Socrates: say whether a virtual tree is real, and he leads you to the philosophers who can test your answer. *(134)*

## Description (under 500 words)

**Inspiration**

I teach philosophy, and the question my students care about most is the one VR makes concrete: if a world is virtual, is it any less real? Robert Nozick asked whether you would plug into a machine that gave you any experience you wanted. Today the machine is on your face. *Peripatos* (after the walking school of Aristotle's Lyceum) turns that question into a walk. Socrates meets you in a forest and points at a tree: if someone built this tree in a computer, would it be real? Your answer decides where you go next.

**What it is**

The forest is a map of philosophy. Groves for Metaphysics, Epistemology and Value Theory hold more than thirty philosophers. If you say the virtual tree **isn't real**, Socrates walks you to Gregory of Nyssa, who argues that a body is just its qualities meeting in one place. If you say **"I don't know"**, he takes you to Augustine, who shows that even a deceived mind knows it exists. If you say **"Who cares?"**, he takes you to John Chrysostom, who asks whether a pleasant illusion is a good life. Each father then hands you on to a contemporary philosopher (Michael Heim, Nick Bostrom or Robert Nozick), or sends you to another grove if your answer belongs there. Every conversation is a short Socratic exchange: the philosophers ask you why, and your answers carry you through the map. Grove quizzes check that you can tell a metaphysical question from an epistemic or an ethical one.

**How it's played with hands**

There's no controller anywhere. A Talk button appears above a philosopher when you're close and facing them; you point and pinch to start. Lines and answers sit on a panel that follows you lazily; you point and pinch to continue or to choose. When a philosopher walks you somewhere, you're carried alongside at walking pace with a comfort vignette, and your view never turns by itself.

**How we built it**

Unity 6 with the Meta XR SDK (Interaction SDK Building Blocks for the camera rig, hand tracking and ray-canvas interaction). Dialogue is written in Yarn Spinner 3. A shared walking system lets any philosopher walk with the player and hand them to another philosopher by name. The NavMesh, the bodies and the character looks are applied with small Editor tools, so the whole roster can be updated in a click.

**What's next**

Every philosopher on the map as a walking guide, a carried map and notebook so students can use the game as a study guide, and versions for classrooms. **[Jim]** add launch plans.

## What's new since Sep 24 (Adapted division)

Before Sep 24 *Experience Machine* was a desktop and web game: Socrates walked with you, and stationary philosophers answered questions in a forest. **[Jim: check this matches what existed then]**

New since Sep 24:
- **Quest edition, hands-first.** Ported to Meta Quest with the Meta XR SDK: a hand-tracked rig, Talk buttons and a world-space dialogue panel you press with a hand ray, and keyboard prompts hidden in VR.
- **Walk-and-talk system.** Any philosopher can now walk with the player and hand them on to another by name. The player is carried at walking pace with a comfort vignette.
- **A new opening and a new route.** Socrates' contest question about the virtual tree, and three new characters, Gregory of Nyssa, Augustine and John Chrysostom, each with a branching dialogue that leads to Heim, Bostrom or Nozick.
- **Bodies and looks** for the philosophers, who used to be capsules, and a NavMesh so they walk the forest.

## How hand interaction works (optional field)

- **Start a conversation:** walk up to a philosopher and face them; a Talk button appears above their head. Point at it and pinch.
- **Read and answer:** the dialogue panel floats about a metre in front of you and re-centres when you turn. Pinch **Continue** for the next line, or pinch an answer.
- **Walks:** when a philosopher sets off, you're carried beside them at walking pace. A vignette narrows your view while you move, and your view never rotates by itself.
- **Everything else** (recentering, the Quest menu, quitting) uses the standard Quest hand gestures.
