# Writing brief: philosopher conversations for Peripatos (Yarn Spinner 3)

Upload this file to NotebookLM as a source. Then, in the chat, ask for one
philosopher at a time, for example: **"Following the writing brief, write
Kant.yarn."** One philosopher per message keeps the output from being cut off.

Other sources to add to the same notebook:

1. David Chalmers's webpage and the table of contents of *Reality+*. The text
   of the book is **not** in this notebook. Use the table of contents only to
   see which chapter covers which topic.
2. `RealityPlus_Map.txt`: a copy of the game's current dialogue. Each
   philosopher's existing node is in it (search for `title: Talk_Kant` and so on).
   These nodes already put several of Chalmers's ideas into the philosophers'
   mouths, and they are the best guide in this notebook to how the game uses
   the book.
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

Required structure. Node names use the file's key, here `Kant`; replace the
words in capitals with short words of your own (for example `DUTY`, `OUTCOMES`).
This is a fixed map: write **every** node on it.

```
Converse_Kant                         entry. Choice 1: 2 or 3 positions, A, B (, C)
  -> Converse_Kant_A                  the philosopher engages position A. Choice 2
       -> Converse_Kant_A_CONCEDE     a reply to the player conceding
       -> Converse_Kant_A_PUSHBACK    a reply to the player pushing back
       (-> Converse_Kant_A_ASK)       optional: a reply to a question
  -> Converse_Kant_B                  the same for position B
       -> Converse_Kant_B_CONCEDE
       -> Converse_Kant_B_PUSHBACK
  (-> Converse_Kant_C ...)            optional third position, same pattern
Converse_Kant_Turn                    every Choice 2 reply node jumps here.
                                      Choice 3: 2 or 3 verdicts
  -> Converse_Kant_Verdict_FIRST      a reply to that verdict
  -> Converse_Kant_Verdict_SECOND
Converse_Kant_Close                   every verdict node jumps here; ends with the handoff
```

What each kind of node must do:

1. **Entry (`Converse_Kant`).** The philosopher greets the player in character
   and raises the problem in their own terms, connected to virtual worlds.
   **Do not say "welcome to" the grove or the area, and do not name the
   area**: the node you hand off to already does that for some philosophers.
   Choice 1 offers two or three genuinely different positions.
2. **Position nodes (`Converse_Kant_A` and so on).** The philosopher states
   the player's position at its strongest, then presses on it with the best
   objection from their own view. Choice 2 offers the player's possible moves:
   concede, push back, or ask. **Every Choice 2 option jumps to its own reply
   node. None of them may jump straight to the Turn.**
3. **Reply nodes (`_CONCEDE`, `_PUSHBACK`, `_ASK`).** Two to four lines that
   answer *that* move with something new: a further consideration, a
   counterexample, or a concession by the philosopher. Then `<<jump
   Converse_Kant_Turn>>`.
4. **Turn (`Converse_Kant_Turn`).** The philosopher states the strongest
   objection to *their own* view **and answers it in this same node**, honestly,
   including what the answer does not settle. Then Choice 3 offers two or three
   verdicts the player might reach (for example "Your answer holds", "Your
   critics win this one", "It depends on something we have not settled").
   **Do not offer "How do you answer that?" as an option**: the answer has
   already been given.
5. **Verdict nodes (`Converse_Kant_Verdict_...`).** Two or three lines replying
   to that verdict specifically, then `<<jump Converse_Kant_Close>>`.
6. **Close (`Converse_Kant_Close`).** Two or three lines that name what is
   still open and leave the player with a question to carry. The last line is
   the handoff jump from the roster, for example `<<jump Talk_Kant>>`.

### Branching rule (most important)

- **No two options in the same choice may jump to the same node,** and no
  option may skip its reply node.
- A different option must get a different *response*, not the same response
  with one line changed. If two options would get the same reply, merge them
  or rewrite one.
- Paths rejoin only at `Converse_<Key>_Turn` and `Converse_<Key>_Close`.

### Size

At least **45 spoken lines** in total (count only lines that start with the
speaker's name; headers, options and jumps do not count), across 11 to 18
nodes. Any single path through the conversation should be about 15 to 20
spoken lines, three to five minutes in the headset.

### Avoid stock lines

Do not reuse filler transitions. In particular, none of these, or close
variants: "Let us test the limits of your view", "Let me turn this on my own
position", "Precisely", "Exactly", "Indeed", "Step forward", "Step closer",
"Take me to...". Every line should say something about the argument.

### Before you output

Write the file, then check it and fix it before showing it:

1. For every choice, list each option's target. No target repeats within a
   choice, and no Choice 2 option targets the Turn.
2. Every node on the map exists, and every node is reached.
3. Count the spoken lines: at least 45.
4. The Turn node contains the philosopher's answer to the objection.
5. No line from the "Avoid stock lines" list.

Output only the final file, not the checklist.

## Voice and content

- **Discursive, not a lecture.** The philosopher asks more than they tell. No
  speech should run longer than three lines before the player gets to choose
  something.
- **Accurate positions.** State only what the philosopher actually held.
  This applies to critics too: name another philosopher as a critic only for
  an objection that philosopher actually made, or that clearly follows from
  their stated view (Bentham on pleasure, for example). An objection named
  after someone, such as the "Boltzmann brain" problem, was not necessarily
  made by that person. If in doubt, say "my critics". A
  historical figure reasons about virtual worlds *from their own commitments*
  ("If your senses could be fed by a machine, then by my method...") and never
  claims to know modern facts they could not have known. The game has always
  let them talk across time about one another, as in "Long before Hobbes, I...".
- **Tie it to virtual worlds through the chapter's topic.** The roster gives
  the *Reality+* chapters for each philosopher. Use the table of contents to
  see what those chapters are about, and use the philosopher's existing node
  in `RealityPlus_Map.txt` to see how the game already frames the question.
  You do not have the book's text, so **do not attribute specific claims,
  arguments or examples to Chalmers** beyond what the existing nodes or his
  webpage state. When in doubt, let the philosopher argue from their own
  position and leave Chalmers out of that line.
- **Check positions against the SEP.** For each philosopher, use the SEP
  entry or entries listed under **Reference sources** as the authority on
  what they held. Where the list says there is no SEP entry, rely on the
  philosopher's existing node and on widely known facts about their work, and
  keep claims about the person modest. Paraphrase the SEP: do not copy its
  sentences.
- **No long quotations.** At most one short phrase (under 15 words) quoted from
  Chalmers's webpage, the SEP or any other modern source. Paraphrase everything else. Short lines
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
    <<jump Converse_Example_First_Concede>>
-> I still disagree, and here is why.
    <<jump Converse_Example_First_PushBack>>
===

title: Converse_Example_First_Concede
tags: converse
area: Normative
speaker: Example
---
Example: A response written for someone who conceded.
Example: A further consideration that the concession opens up.
<<jump Converse_Example_Turn>>
===

title: Converse_Example_First_PushBack
tags: converse
area: Normative
speaker: Example
---
Example: A response written for someone who pushed back.
Example: A counterexample or concession aimed at that reply.
<<jump Converse_Example_Turn>>
===
```

Note how the two options above lead to two different nodes, and the paths
rejoin only at `Converse_Example_Turn`.

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
| Heim | No entry. Rely on his existing node (`Talk_Heim`, `Gate_Metaphysics`) and keep claims modest. |
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
| Narada | No entry. Rely on his existing node (`Talk_Narada`) and the traditional story of Narada and Vishnu's maya; keep claims modest. |
| Turing | https://plato.stanford.edu/entries/turing/ |
| Searle | https://plato.stanford.edu/entries/chinese-room/ |
| Wheeler | https://plato.stanford.edu/entries/information-semantic/ (discusses his "it from bit") |
| Russell | https://plato.stanford.edu/entries/russell/ and https://plato.stanford.edu/entries/russellian-monism/ |
| Sellars | https://plato.stanford.edu/entries/sellars/ |
| Galileo | https://plato.stanford.edu/entries/galileo/ |
| Bostrom | No entry on him or the simulation argument. https://plato.stanford.edu/entries/brain-vat/ for the sceptical background; rely on his existing node (`Talk_Bostrom`) and the widely known form of his simulation argument. |
| Boltzmann | https://plato.stanford.edu/entries/statphys-boltzmann/ |
| Nozick | No entry on him. https://plato.stanford.edu/entries/well-being/ (section 4.1, the experience machine) and https://plato.stanford.edu/entries/hedonism/ |
| Bentham | https://plato.stanford.edu/entries/bentham/ |
| Kant | https://plato.stanford.edu/entries/kant-moral/ |
| Aristotle | https://plato.stanford.edu/entries/aristotle-ethics/ |
| Singer | No entry on him. https://plato.stanford.edu/entries/moral-animal/ |
| Slater | No entry. https://plato.stanford.edu/entries/experimental-moral/ (VR versions of moral dilemmas); rely on his existing node (`Talk_Slater`) for his experiments and do not add details of studies beyond it. |
| Mozi | https://plato.stanford.edu/entries/mohism/ |
| Hobbes | https://plato.stanford.edu/entries/hobbes-moral/ |
| Rawls | https://plato.stanford.edu/entries/rawls/ |
