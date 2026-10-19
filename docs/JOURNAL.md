# Journal and quests

What the original does with the journal, measured black-box, and how OpenWinter does it
(`src/OpenWinter.Game/Players/JournalService.cs`, `client/UI/JournalPanel.cs`, `client/UI/Classic/Sheets/ClassicJournal.cs`).

## Native or scripted

- **Native**: AddJournalQuestEntry / RemoveJournalQuestEntry, the `NW_JOURNAL_ENTRY<tag>` local, a conversation
  line's Quest / QuestEntry, the journal line in the chat and the journal button's pulse.
- **Scripted**: quest **experience**. The engine never gives the journal's XP, not when a script reaches an end
  entry and not when a conversation does (both measured: experience unchanged). The stock includes do it
  (`nw_i0_plot` / `nw_i0_tool` RewardXP: GetJournalQuestExperience x percentage, then GiveXPToCreature), and modules
  call them; OpenWinter runs them unchanged.

## Testing

- **nwserver** suite `journal_funcs` (a generated module.jrl: `"journal"` in suite.json): GetJournalQuestExperience
  and the locals AddJournalQuestEntry / RemoveJournalQuestEntry leave on creatures, a placeable and the module.
- **nwmain** scene `journalpc`: the player's locals, experience and chat for each kind of update (new, next,
  repeated, lower, unknown entry, unknown quest, override, end, removal), two conversation lines with a quest, an
  update aimed at a henchman, screenshots of the party bar 0.3-4.5 s after updates, and ExportSingleCharacter.
- Visual scenes and parity suites can carry a journal (`"journal": [{ "tag", "name", "xp", "priority",
  "entries": [{ "id", "text", "end" }] }]`), and scene conversations a node's `"quest"` / `"questEntry"`.

## The functions

- **AddJournalQuestEntry(tag, state, oCreature, bAllPartyMembers, bAllPlayers, bAllowOverrideHigher)**
  - The engine keeps the state in the local int **"NW_JOURNAL_ENTRY" + tag** (the tag as written) of every creature
    it reaches. That includes **non-player creatures**: an NPC gets the local too. Placeables and the module don't.
  - The local is set **whether or not module.jrl has the quest or the entry** (state 9 of a three-entry quest, a
    quest that doesn't exist: both set).
  - Without bAllowOverrideHigher a state **not above the local** is ignored, and a local set by script counts (local
    7, then state 3: stays 7). With it, a lower state replaces the higher one.
  - **bAllPlayers** reaches every player and nobody else (an NPC named in the call doesn't get it).
  - **bAllPartyMembers** reaches the player's party. Aimed at a **henchman** it reaches only the henchman: its master's
    local and journal stayed as they were, and no line was printed.
- **RemoveJournalQuestEntry** clears the local (exact tag) and the journal entry.
- **GetJournalQuestExperience(tag)** reads module.jrl's XP; tags are **case-sensitive** ("Q_A" of "q_a" reads 0), as is
  the local's name.

## Conversations

A line with a Quest and QuestEntry, shown in the conversation window, updates the player's journal before the line
is printed (the chat shows the journal line first), with no experience even for an end entry. A first line with no
answers, spoken over the talker instead of opening the window, does **not** update the journal. Lines picked by the
player (replies) were not measured; OpenWinter treats them the same as shown lines.

## What the player sees

- **Chat**: "Your journal has been updated. <quest name>" (dialog.tlk 6395 and the quest's name) in **yellow**
  (255, 255, 1), for a new or next entry, an entry module.jrl lacks, an override to a lower entry, an end entry and a
  **removal** alike. Nothing for a repeated or lower state that was ignored, and nothing for a quest module.jrl lacks.
  There is no separate "quest completed" line, no floating text and no sound was looked for.
- **The journal button** (classic party bar) pulses: its "encouraged" state, a warm glow round the quill that comes and
  goes once a second (bright at 0.3, 1.3 and 4.5 s, faint at 0.8 and 2 s after an update). OpenWinter keeps it pulsing
  until the journal is opened; the modern interface lights its Journal button until then.

## Network

The server sends a `journal` notify to each player an update reaches (and only to them): the quest, its current
entry's text (empty for an entry module.jrl lacks), the earlier entries, and `update: true`. A removal sends
`removed: true` with the quest's name. When the journal window opens, the client asks for the whole journal and the
server resends it with `update: false` (no chat line, no pulse).

## Persistence

- **The character file**: ExportSingleCharacter wrote a .bic with no journal and no local variables at all (no
  `VarTable`, no NW_JOURNAL_ENTRY), so a server-vault character loses its quests when the server restarts, as
  persistent worlds know. OpenWinter's vault characters keep neither.
- **Single-player saves** keep the journal with the rest of the game (not measured on the original).

## Not matched yet / not measured

- The journal button's pulse in the original after more time (it was still pulsing 4.5 s after an update); whether it
  stops by itself or only when the journal is opened.
- Reply lines with a quest, and a conversation's quest flags (party, override).
- Several players: who sees which line when an update reaches a party.
- The journal window's own look for an entry module.jrl lacks.
