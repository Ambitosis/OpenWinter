# How conversations end, and what reaches the chat window

Measured on the original client with the visual harness, which logs each dialog's end and
abort scripts, the store events and the chat window. The scenes are `convoend`, `convoend2`, `convoend3`,
`convoend4`, `loginconvo` and `cutsceneconvo`. `python tools/visual/check_convoend.py` compares the two engines case by case.
The rules are implemented in `src/OpenWinter.Game/Conversations/ConversationService.cs`.

## The last line

A line is the *last line* when it offers no answers (the window shows an end choice), or exactly one answer
that leads nowhere. Two answers that both end the conversation do not count. Almost every rule below turns on
this:

| How it stops | At the last line | Anywhere else |
|---|---|---|
| Player and talker 10 m apart (either one moving or jumping) | end script | abort script |
| The talker leaves the area, or a new conversation is started over this one | end script | abort script |
| The player presses Esc | the single answer is chosen (posted to chat, its action runs), or with no answers it finishes | abort script |
| A store opens while a line is shown | as Esc | as Esc |
| The talker is destroyed | neither script | neither script |

- The walk-away limit is 10 m. In `walk_away` the abort script ran at a distance of exactly 10.00. At 6.4 m
  the conversation goes on.
- `ClearAllActions(TRUE)` on the player and damage to the player don't stop a conversation. A ground click that
  walks the player under 10 m away doesn't stop it either.
- In `loginconvo`, jumping away runs the end script by this rule: its line's only answer ends the conversation.
- A fight started by either side stops it by the same rule: the talker or the player beginning an attack
  (ActionAttack, the stock AI's combat round) or casting a spell runs the end script at a last line and the abort
  script anywhere else (`convobusy2` open_attack_inreach, open_cast, last_attack, stock_attacked; `convobusy3`
  open_pc_attacks, open_pc_casts). Being attacked doesn't stop it (the talker or the player hit while talking:
  `convobusy` open_npc_attacked, open_pc_attacked), nor the talker walking 3 m, playing an animation, waiting or
  being cleared (`convobusy2`).
- While the end or abort script runs, IsInConversation still returns TRUE for both the player and the talker (every
  end and abort reading of `convoend` and `convobusy`).

## Cutscene mode

Measured with the visual scene `cutsceneconvo`: a conversation started by the talker (ActionStartConversation, or
BeginConversation run on it) while the player is in cutscene mode (SetCutsceneMode), with a module
OnPlayerCancelCutscene script that only logs.

- The conversation opens as usual and the player can answer it (number keys).
- Esc doesn't end it, at the first line or at the last: the window stays open, neither the end nor the abort script
  runs, and the single answer of a last line isn't chosen. Each press runs the module's OnPlayerCancelCutscene
  instead, exactly as Esc in cutscene mode with no conversation does (`cutsceneabort`), and cutscene mode stays on.
  With no OnPlayerCancelCutscene script, Esc does nothing at all. Left clicks enabled or not makes no difference.
- Once a script has turned cutscene mode off, Esc at a line runs the abort script as above.
- The other ways a conversation stops are unchanged: moving the player 33 m off ran the abort script at the first
  line and the end script at the last.

So the client sends the cutscene's cancel request for Esc in cutscene mode, not the conversation's escape (a server
does close the conversation and run its abort script if sent the escape, leaving a module that keeps cutscene mode on
with no way on).

## Busy: when a request to talk is refused

Measured with the parity suite `convo_busy` (two creatures on `nwserver`) and the visual scenes
`convobusy`, `convobusy2`, `convobusy3` and `convobusy4` (a player on `nwmain`). `python tools/visual/check_convobusy.py`
compares the two engines case by case. Implemented in `ConversationService.Refuses`.

A *request* is a click on a creature or ActionStartConversation, checked when the asker gets there (asked from 9 m
away, the message came as the player arrived). It is refused while the creature asked is in combat (GetIsInCombat:
fighting, or attacked within the last few seconds) or dead. Then its OnConversation doesn't run and no dialog opens,
whether a dialog was named or not.

| Who asks whom | The one asked is in combat | dead |
|---|---|---|
| The player asks a creature (or starts her own private conversation) | "The object is busy and can not talk to you right now." (strref 6625, yellow, chat only) | nothing |
| A creature asks the player | the player gets "You are too excited to talk right now." (strref 63229) | not measured |
| A creature asks a creature | nothing | nothing |

- The asker's own combat doesn't matter: a player under attack can ask a creature, and a creature fighting can ask the
  player. Neither does the one asked casting, walking, being hostile, already being in a conversation (the
  original lets several talk to one) or being uncommandable (the stock `nw_c2_default4` then declines unless charmed).
  Placeables are never busy.
- BeginConversation is never refused, from a creature fighting or toward a player in combat.
- For example, Aribeth and the others are fighting during the Prelude's graduation fight, so clicking one of them
  gives the busy message.
- Between two creatures, ActionStartConversation only runs the partner's OnConversation (GetLastSpeaker is the asker,
  GetPCSpeaker invalid), whatever dialog was named. If that script calls BeginConversation, the original plays a
  conversation between the two creatures on its own (lines spoken in turn, the first answer taken, IsInConversation
  TRUE for both, the dialog's scripts run on the creature BeginConversation names as the other party). OpenWinter
  doesn't have conversations between creatures yet: BeginConversation there returns FALSE.

## Stores opened from a conversation

- **From a reply's action** (the usual merchant "Let me see your wares"): the store opens once the reply is
  handled. If the reply ends the conversation, the end script runs first (`store_reply_end`). If it leads to
  another line, the conversation goes on beside the store window (`store_fromreply`).
- **From a line's own action**: the line is posted to chat but no window opens, and the conversation closes by
  the last-line rule (end at a last line, abort otherwise) without choosing an answer. Whether that line is the
  first or a later one makes no difference to OpenWinter. On the original, a store opened by the *first* line's
  action gave abort in 3 of 4 runs even at a last line (`store_reply`, `rep_first_end`, `rep_first_end2`). It
  races with the window opening, so OpenWinter always takes the race-free result.
- **Half a second after the line shows** (`late_*`): exactly like Esc. At a single ending answer that answer is
  chosen and posted to chat.

## One-liners

A conversation whose first line offers no answers opens no window. The line is spoken over the talker (and
reaches chat as speech), its action script runs with `GetPCSpeaker()` invalid, and the end script runs
(`store_noreply`, `store_oneliner`).

## The chat window

Every line shown and every answer chosen is posted to the player's chat window, as "Bandit: First line." and
"Aluvian Darkstar: Keep talking." (convoend's chat log, 19–20 lines per scene, identical in both engines). The
server sends them on a `Dialog` chat channel, which has no speech bubble. The classic chat window's menu can
leave them out ("Exclude Dialogue").

Damage from `EffectDamage` (spells, traps, scripts) prints like a weapon hit: "Someone damages Aluvian
Darkstar: 1 (1 Magical)". "Someone" is used when no creature caused it, and the amount is 0 against a plot
target.

For the classic window's look (portraits, colours, the right-click menu and the split), see [CLASSIC_UI.md](CLASSIC_UI.md).

## Not measured

- The text of the end choice on a line with no answers.
- Conversations between two creatures beyond the request above (`convo_busy` saw them play on their own), and
  `BeginConversation` from a placeable and private conversations. The ending rules come from an NPC talking to the
  player.
- The busy rule from the real mouse: the clicks were driven by the player's scripted ActionStartConversation, the same
  dialog action a click queues. A dead player asked by a creature. Whether a potion or other item use ends an open
  conversation (OpenWinter only stops it for spells cast).
- What other players nearby see of a non-private conversation.
