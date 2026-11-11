# Writing brief: philosopher conversations for Peripatos (Yarn Spinner 3)

Upload this file to NotebookLM as a source. Then, in the chat, ask for one
philosopher at a time, for example: **"Following the writing brief, write
Kant.yarn."** One philosopher per message keeps the output from being cut off.

Other sources to add to the same notebook:

1. David Chalmers, *Reality+* (the chapters listed in the roster below).
2. `RealityPlus_Map.txt`: a copy of the game's current dialogue. Each
   philosopher's existing node is in it (search for `title: Talk_Kant` and so on).
3. The Stanford Encyclopedia of Philosophy (SEP) entries listed under
   **Reference sources** at the end of this brief. Add each one with
   NotebookLM's "Website" source option, pasting the URL. Adding only the
   entries for the philosophers you are writing that day keeps the notebook
   focused.

---

## What the game is

*Peripatos: The Experience Machine* is a hands-only VR game for Meta Quest. The
player walks through a forest where philosophers stand in groves, one grove per
area of *Reality+*. Talking to a philosopher opens a world-space dialogue panel.
The player reads each line, presses Continue, and answers by poking one of
the option buttons. There is no voice acting and no typing.

Each philosopher already has a short node that poses one puzzle and ends in a
quiz-like choice. Your job is to write the **conversation that comes before
it**: a discursive, walking-pace exchange in which the philosopher and the
player work through the question together. When your conversation ends, it
hands over to the existing node. That node closes the visit, so **do not
repeat its example or its question**. Set it up instead.

## The shape: a debate in which every perspective is a node

Think of an Audio Overview in Debate format, with the player as one of the two
debaters. The philosopher argues their real position. The player picks the
position they want to try out, and the philosopher engages *that* position
seriously. It is never a quiz, and no answer is marked wrong.

Required structure (node names use the file's key, here `Kant`):

1. **`Converse_Kant`**: the entry node. The philosopher greets the player in
   character, raises the problem in their own terms and connects it to virtual
   worlds as *Reality+* does. It ends with **Choice 1**: two or three options,
   each a different perspective on the problem.
2. **One node per perspective**, for example `Converse_Kant_Outcomes`,
   `Converse_Kant_Duty` and `Converse_Kant_Doubt`. In each one the philosopher
   takes the player's view seriously, states it at its strongest, and then
   presses on it with the best objection from their own position. Each ends
   with **Choice 2**: concede, push back, or ask a question. All of these lead
   on to step 3, either directly or through one short follow-up node.
3. **`Converse_Kant_Turn`**: the philosopher turns the argument on
   themselves. Present the strongest objection to *their own* view, the one
   their best critics make, and let them answer it honestly. Name the critic if
   that critic is another philosopher in the game (see the roster). Otherwise
   say "my critics". End with **Choice 3**.
4. **`Converse_Kant_Close`**: a short wrap-up that names what is still open
   and leaves the player with a question to carry. The last line of the node
   must be the handoff jump from the roster, for example `<<jump Talk_Kant>>`.

Size: 40 to 80 lines in total across 5 to 9 nodes.

## Voice and content

- **Discursive, not a lecture.** The philosopher asks more than they tell. No
  speech should run longer than three lines before the player gets to choose
  something.
- **Accurate positions.** State only what the philosopher actually held. A
  historical figure reasons about virtual worlds *from their own commitments*
  ("If your senses could be fed by a machine, then by my method...") and never
  claims to know modern facts they could not have known. The game has always
  let them talk across time about one another, as in "Long before Hobbes, I...".
- **Tie it to *Reality+*.** Use the chapter or chapters in the roster. Explain
  Chalmers's view in your own words.
- **Check positions against the SEP.** For each philosopher, use the SEP
  entry or entries listed under **Reference sources** as the authority on
  what they held, alongside *Reality+*. Where the two differ, follow the SEP
  on the philosopher's own view and *Reality+* on Chalmers's use of it. Where
  the list says there is no SEP entry, rely on *Reality+* and keep claims about
  the person modest. Paraphrase the SEP as well: do not copy its sentences.
- **No long quotations.** At most one short phrase (under 15 words) quoted from
  *Reality+* or any other modern book. Paraphrase everything else. Short lines
  from public-domain historical texts are fine.
- **Respect the player.** Every option is a position a thoughtful person might
  hold. None of them is a joke option, and nobody is mocked.
- **Plain English** for a general adult audience, with technical terms
  explained the first time they appear.

## Readability in VR

- Each spoken line is at most **30 words**, and one or two sentences is best.
- Each option is at most **12 words**.
- At most **3 options** per choice.

## Yarn rules (the file must compile; follow exactly)

- Output **one file in a single code block**, with the filename on the line
  above the block: `Kant.yarn`.
- Every node has this header, then `---`, then the body, then `===`:

  ```
  title: Converse_Kant
  tags: converse
  area: Normative
  speaker: Kant
  ---
  ```

- Spoken lines start with the speaker's short name from the roster and a colon:
  `Kant: Then let us test it.` **Only the philosopher speaks.** The player
  speaks only through options.
- Options start with `-> ` at the left margin. Lines that belong to an option
  are indented **4 spaces** beneath it. Every option ends with a `<<jump ...>>`
  to a node in the same file.
- **No colon anywhere** except right after the speaker's name. This includes
  options: Yarn reads `-> Yes: because` as a character called "Yes".
- **Do not use these characters** in spoken lines or options: `{ } [ ] < > #`
  and `//`. Write words instead ("less than", "number").
- **The only command allowed is `<<jump NodeName>>`.** No `<<set>>`, `<<if>>`,
  variables, `<<wait>>` or anything else.
- Every node name starts with `Converse_<Key>`, is unique, and is reached from
  somewhere. No dead ends: every path ends at the Close node's handoff jump.
- No comments and no text outside the nodes.

### Syntax example (placeholder content, shown only for the format)

```
title: Converse_Example
tags: converse
area: Normative
speaker: Example
---
Example: A short opening line. Then the problem, in one sentence.
Example: A question for you.
-> First perspective, in a few words.
    <<jump Converse_Example_First>>
-> Second perspective, in a few words.
    <<jump Converse_Example_Second>>
===

title: Converse_Example_First
tags: converse
area: Normative
speaker: Example
---
Example: The strongest version of your view. Then my objection.
-> I concede the point.
    <<jump Converse_Example_Turn>>
-> I still disagree, and here is why.
    Example: A one-line reply.
    <<jump Converse_Example_Turn>>
===
```

---

## Roster

| File key | Speaker name | Area | *Reality+* chapters | Hand off with | Current node opens with |
|---|---|---|---|---|---|
| Heim | Heim | Metaphysics | 6, 10, 12 | `<<jump Gate_Metaphysics>>` | virtual realism, whether virtual objects are real |
| Berkeley | Berkeley | Metaphysics | 4, 7 | `<<jump Talk_Berkeley>>` | the tree in the quad when no one looks |
| Plato | Plato | Metaphysics | 1, 23 | `<<jump Talk_Plato>>` | the cave and its shadows |
| Descartes | Descartes | Epistemology | 3 | `<<jump Gate_Epistemology>>` | the stick that looks bent in water |
| Zhuangzi | Zhuangzi | Epistemology | 1, 24 | `<<jump Talk_Zhuangzi>>` | the butterfly dream |
| Moore | Moore | Epistemology | 4 | `<<jump Talk_Moore>>` | "here is one hand" |
| Elisabeth | Elisabeth | Mind | 14 | `<<jump Gate_Mind>>` | her letter to Descartes on mind and body |
| Nagel | Nagel | Mind | 15 | `<<jump Talk_Nagel>>` | what it is like to be a bat |
| Clark | Clark | Mind | 16, 11 | `<<jump Talk_Clark>>` | Otto and Inga, the extended mind |
| Putnam | Putnam | Language | 20 | `<<jump Gate_Language>>` | Twin Earth and XYZ |
| Carnap | Carnap | Language | 4 | `<<jump Talk_Carnap>>` | whether an unverifiable hypothesis is meaningful |
| Anselm | Anselm | Religion | 7 | `<<jump Gate_Religion>>` | what it would take to be a god |
| Leibniz | Leibniz | Religion | 7, 8, 18 | `<<jump Talk_Leibniz>>` | why a good creator allows suffering |
| Narada | Narada | Religion | 1 | `<<jump Talk_Narada>>` | Vishnu's maya and the errand for water |
| Turing | Turing | Computing | 21, 15 | `<<jump Gate_Computing>>` | what a computer is |
| Searle | Searle | Computing | 21, 15 | `<<jump Talk_Searle>>` | the wall that "runs" a word processor |
| Wheeler | Wheeler | Computing | 8, 9 | `<<jump Talk_Wheeler>>` | it from bit |
| Russell | Russell | PhysicalScience | 22 | `<<jump Gate_PhysicalScience>>` | physics says what things do, not what they are |
| Sellars | Sellars | PhysicalScience | 23 | `<<jump Talk_Sellars>>` | the manifest and scientific images |
| Galileo | Galileo | PhysicalScience | 23 | `<<jump Talk_Galileo>>` | colours and tastes exist only in the perceiver |
| Bostrom | Bostrom | Probability | 5 | `<<jump Gate_Probability>>` | the simulation argument |
| Boltzmann | Boltzmann | Probability | 24 | `<<jump Talk_Boltzmann>>` | Boltzmann brains |
| Nozick | Nozick | WellBeing | 17 | `<<jump Gate_WellBeing>>` | the experience machine |
| Bentham | Bentham | WellBeing | 17 | `<<jump Talk_Bentham>>` | pleasure and pain as the only goods |
| Kant | Kant | Normative | 18 | `<<jump Gate_Normative>>` | treating persons never merely as means |
| Aristotle | Aristotle | Normative | 18, 17 | `<<jump Talk_Aristotle>>` | character and what you are becoming |
| Singer | Singer | Applied | 18, 19 | `<<jump Gate_Applied>>` | what gives a being moral status |
| Slater | Slater | Applied | 19, 11 | `<<jump Talk_Slater>>` | distress in a virtual shock experiment |
| Mozi | Mozi | Political | 19 | `<<jump Talk_Mozi>>` | the time before laws and rulers |
| Hobbes | Hobbes | Political | 19 | `<<jump Gate_Political>>` | the war of all against all |
| Rawls | Rawls | Political | 19 | `<<jump Talk_Rawls>>` | the veil of ignorance for a virtual world |

Philosophers in the same area can be named as one another's critics in the
Turn node: for example Bentham against Kant, Hobbes against Rawls, or Searle
against Turing.

Not included: Socrates and the three church fathers (Gregory of Nyssa,
Augustine and John Chrysostom). Their contest-route dialogue is already written.

---

## Reference sources (Stanford Encyclopedia of Philosophy)

Where the SEP has no entry on the person, the closest topic entry is given;
use the named section where one is listed.

| File key | SEP entries |
|---|---|
| Heim | No entry. Rely on *Reality+* ch. 6 and 10. |
| Berkeley | https://plato.stanford.edu/entries/berkeley/ |
| Plato | https://plato.stanford.edu/entries/plato/ |
| Descartes | https://plato.stanford.edu/entries/descartes-epistemology/ |
| Zhuangzi | https://plato.stanford.edu/entries/zhuangzi/ and https://plato.stanford.edu/entries/dreams-dreaming/ |
| Moore | https://plato.stanford.edu/entries/moore/ |
| Elisabeth | https://plato.stanford.edu/entries/elisabeth-bohemia/ |
| Nagel | No entry on him. https://plato.stanford.edu/entries/consciousness/ |
| Clark | No entry on him. https://plato.stanford.edu/entries/embodied-cognition/ (section 2.3, Extended Cognition) and https://plato.stanford.edu/entries/content-externalism/ |
| Putnam | No entry on him. https://plato.stanford.edu/entries/content-externalism/ (Twin Earth) and https://plato.stanford.edu/entries/brain-vat/ |
| Carnap | https://plato.stanford.edu/entries/carnap/ |
| Anselm | https://plato.stanford.edu/entries/anselm/ and https://plato.stanford.edu/entries/ontological-arguments/ |
| Leibniz | https://plato.stanford.edu/entries/leibniz-evil/ |
| Narada | No entry. Rely on *Reality+* ch. 1. |
| Turing | https://plato.stanford.edu/entries/turing/ |
| Searle | https://plato.stanford.edu/entries/chinese-room/ |
| Wheeler | https://plato.stanford.edu/entries/information-semantic/ (discusses his "it from bit") |
| Russell | https://plato.stanford.edu/entries/russell/ and https://plato.stanford.edu/entries/russellian-monism/ |
| Sellars | https://plato.stanford.edu/entries/sellars/ |
| Galileo | https://plato.stanford.edu/entries/galileo/ |
| Bostrom | No entry on him or the simulation argument. https://plato.stanford.edu/entries/brain-vat/ for the sceptical background; rely on *Reality+* ch. 5 for the argument itself. |
| Boltzmann | https://plato.stanford.edu/entries/statphys-boltzmann/ |
| Nozick | No entry on him. https://plato.stanford.edu/entries/well-being/ (section 4.1, the experience machine) and https://plato.stanford.edu/entries/hedonism/ |
| Bentham | https://plato.stanford.edu/entries/bentham/ |
| Kant | https://plato.stanford.edu/entries/kant-moral/ |
| Aristotle | https://plato.stanford.edu/entries/aristotle-ethics/ |
| Singer | No entry on him. https://plato.stanford.edu/entries/moral-animal/ |
| Slater | No entry. https://plato.stanford.edu/entries/experimental-moral/ (VR versions of moral dilemmas); rely on *Reality+* ch. 19 and 11 for his experiments. |
| Mozi | https://plato.stanford.edu/entries/mohism/ |
| Hobbes | https://plato.stanford.edu/entries/hobbes-moral/ |
| Rawls | https://plato.stanford.edu/entries/rawls/ |
