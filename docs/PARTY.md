# Parties

How players form, manage and leave parties, and the party pieces of the rules: invitations, joining, leaving,
kicking, the leadership, the player list, the radial's Party ring, the chat lines and who reads them, One Party Only,
the area's PvP setting, the party's kill experience and SetCommandingPlayer. Also the spell-casting lines and the
loading-screen hints in the chat window. Code: `src/OpenWinter.Game/Society/PartyService.cs` (the rules),
`Requests/PlayerListRequests.cs` (the client requests), `client/UI/PlayerListPanel.cs` (the modern list and
invitation window), `client/UI/Classic/Hud/ClassicPlayerList.cs` (the classic ones), `client/UI/Hud/RadialMenu.Party.cs`
(the radial). Tests: `tests/OpenWinter.Tests/PartyTests.cs`.

## Engine or script

All of party management is the engine's. No stock script takes part: the player list, the radial's party entries,
invitations, the chat lines, One Party Only and the leader passing on are native, and EE has **no party module
event** (its module events are the ones in the toolset's module properties; `nwscript.nss` has no OnPlayerJoinParty).
What scripts see:

- **Functions:** AddToParty, RemoveFromParty, GetFactionLeader, GetFirstFactionMember / GetNextFactionMember,
  GetFactionEqual, SetPCLike / SetPCDislike, SetCommandingPlayer, AdjustAlignment's bAllPartyMembers,
  AddJournalQuestEntry's bAllPartyMembers. All of them run on the same party state as the players' own actions.
- **GUI events** (OnPlayerGuiEvent): GUIEVENT_PLAYERLIST_PLAYER_CLICK (12) when a name in the player list is clicked,
  GUIEVENT_PLAYERLIST_PLAYER_TELL (28) for its Tell button (the selected player in GetLastGuiEventObject), and
  GUIEVENT_PARTYBAR_PORTRAIT_CLICK (13). OpenWinter's lists send 12 and 28.
- **NWNX_Events** (for modules written against NWNX:EE): OpenWinter raises `NWNX_ON_PARTY_INVITE`,
  `_ACCEPT_INVITATION`, `_REJECT_INVITATION`, `_IGNORE_INVITATION`, `_LEAVE`, `_KICK` and `_TRANSFER_LEADERSHIP`,
  each `_BEFORE` (SkipEvent cancels) and `_AFTER`, with OBJECT_SELF the player acting and the event data `INVITED`,
  `INVITED_BY`, `PARTY_LEADER`, `LEAVING`, `KICKED`, `NEW_LEADER` (hex object ids). `_KICK_HENCHMAN` is not raised.
- **Device property** `ui.party.invite-popup.enabled`: the client's "Never show this again." choice. OpenWinter keeps it
  in the client's settings (Gameplay → PartyInvitePopup) and does not yet report it to GetPlayerDeviceProperty.

## The rules

Two original clients can't join one server on one licence ([MULTIPLAYER_HARNESS.md](MULTIPLAYER_HARNESS.md)), so
nothing that needs a second player can be measured on the original. The rules below follow the talk table's lines,
which name each case:

| Action | Rule | Lines (dialog.tlk) |
|---|---|---|
| Invite Into Party (1067) | Only a party's leader, or a player alone, invites; only a player who is alone can be invited | inviter: 6462 "You have sent a Party Invitation to X."; invited: 6460 "You have received a Party Invitation from X." and the invitation window (66776 "You have been invited by X to join a party.") |
| ... by someone not leading | refused | 66858 "Only the party leader may perform this action." |
| ... of someone in a party | refused | 66891 "Invitation failed: X is already in a party." |
| ... of someone with an open invitation | refused | 6456 "X is already considering an invitation." |
| ... while either has invited someone else | refused | 6457 "X is already involved in party negotiations." |
| Accept (66774, 6386 on the radial) | joins the inviter's party | everyone in it, the newcomer too: 6454 "X has joined the party." |
| Reject (66775, 6385) | | invited: 6465 "You have rejected the Party Invitation from X."; inviter: 6469 "X has rejected your invitation to join the party." |
| Ignore Invitations from (6384) | that player's invitations are refused for a while (`IgnoreTime`, 300 s, an estimate) | invited: 6464 "You are ignoring Party Invitations from X for a short time."; inviter: 6458 "X is ignoring your Party Invitation." (also for each refused invitation) |
| An invitation not answered | expires after `InvitationLifetime` (30 s; not measured) | accepting it late: 6468 "The Party Invitation from X has already expired." |
| Leave Party (1071) | | the leaver: 10427 "You have left the party."; the rest: 6453 "X has left the party." |
| Remove From Party (1068) | the leader only (66858) | kicked: 6466 "You have been kicked from the party."; the rest: 6467 "X has been kicked from the party." |
| Transfer Leadership (6383) | the leader only (66858) | everyone: 6455 "X is now the Party Leader." |
| The leader leaves (or logs out) | the member who joined first leads | the rest: 6453, then 6455 |
| AddToParty / RemoveFromParty | as accepting an invitation / leaving | the same lines |
| One Party Only (server.one-party-only) | every player is put into the one party when she enters; nobody may leave or be removed | 66735 "You cannot leave the party. This server has been set to "One Party Only."", 66736 for Remove |
| Dislike (8000) | both players dislike each other (as SetPCDislike: nwscript.nss "Sets oPlayer and oTarget to dislike each other") | the other: 7992 "X now dislikes you!" |

**One Party Only is nwserver's default** (`one-party-only = true` in the settings.tml it writes, schema default true,
strref 66769 "Only One Party"). OpenWinter's dedicated server mirrors that default (`"OneParty"` in server.json,
`--oneparty 0|1`); single player and tests have it off. The multiplayer scenes say `"oneParty": false` so both
harnesses (OpenWinter's server.json and nwserver's settings.tml) run the same way.

## The player list

Measured with one original client on nwserver (scene `mp_partyui`):
- **Classic** (`ingame_pvp.gui` on `pnl_ig_pvp`) opens at the screen's **top left**: "Player List" title, X button,
  the toolbar, "PvP Zone: Party Protected" (PvPSettingLabel: 8017 and 8018 Party Protected / 8019 Full PvP / 8020 No
  PvP), and the list under the Player / Attitude / Reaction headings with a scroll bar at its right. The list is
  **empty with nobody else on the server**: a player is not on her own list.
- **Toolbar** (`ctl_ig_btn_icon`, their placeholder `gui_replace01` drawn with radial icons): Tell Player (66779,
  `ir_chat`), Leave Party (1071, `ir_leave`), Invite Into Party (1067, `ir_invite`), Transfer Leadership (6383,
  `ir_transleader`). The Boot and Ban slots are empty frames for a player (Dungeon Masters': 7998 / 7997, not
  measured). Leave, Invite, Transfer and Tell pressed alone, with nothing selected, do nothing and print nothing.
  There is no Remove From Party button: removing a member is the radial's.
- **The rows** (not measured: needs a second player): OpenWinter draws the name (the leader of a party of several
  marked "(Party Leader)", 66852), the Attitude (Like / Dislike, 7999 / 8000, click to switch) and the Reaction.
  Clicking a name selects it and raises GUIEVENT_PLAYERLIST_PLAYER_CLICK.
- **Modern** (`PlayerListPanel`): the same state and buttons in OpenWinter's kit, with Remove From Party in place of
  Boot / Ban.
- **The invitation window** (`party_invitation.gui`): the message, OK (captioned Accept, 66774), Reject (66775),
  Cancel (closes; the invitation stays open until it expires) and "Never show this again." (66777).
- Both lists keep themselves current: the server sends `player-list` to every player when a party, an invitation,
  a like or the players change.

## The radial

- **The player's own ring has no party entry** in multiplayer (`mp_partyui`, on her body and on her portrait): the
  same seven wedges as in single player, S empty.
- **Another player's ring** (not measured: needs a second player) gains **Party** (6382, `ir_party`) at NW: Invite
  Into Party N, Accept Invitation from NE, Reject Invitation from E, Ignore Invitations from SE, Leave Party S, Remove
  From Party SW, Transfer Leadership W, each enabled only when it applies (the facts come with the target, `party` in
  the target facts). The entries are the talk table's (1067, 6386, 6385, 6384, 1071, 1068, 6383) with the game's
  matching icons; the places are inferred.

## PvP by area

`World.PvpAllows` uses the area's setting (`World.AreaPvp`: the area's PlayerVsPlayer, never more than the
server's), the same one the area-entry line names. A No PvP area protects players even on a Full PvP server; Party
areas protect a player's party. The target's area decides (the attacker's when the target has none).

## Kill experience between players

The rule in [KILL_XP.md](KILL_XP.md), with several players: every counted player takes the row of the **players'
average level** (truncated), divided by (players + associates / 4). A level 15 fighter killing a goblin (challenge
rating 0.5, column C0) beside a level 1 wizard: row 8, C0 = 7, halved: 3 each
(`PartyTests.KillExperienceUsesThePartysAverageRowSharedByThePlayers`). The original's split between players is
**not measured** (a second original client is needed).

## Faction functions on a party

Measured (scene `loadtips`, a player with two henchmen): GetFactionLeader is the player, asked of her or of a
henchman; GetFirst/NextFactionMember(pc, TRUE) gives the player only, with FALSE the player then the henchmen in the
order they joined, the same from a henchman. **GetFactionStrongestMember and GetFactionWeakestMember return the
player** (never a henchman, whoever asks); OpenWinter takes the players of a party only. GetFactionAverageLevel reads
1 either way; the other Get*Member functions are not measured with henchmen.

## Spell-casting lines

Measured with the scenes `castlines` and `castlines2` (one client, a caster with Spellcraft 7); implemented in
`Spells/CastingLines.cs`, called by `CastSpellAction`:
- Two lines per cast: **"&lt;caster&gt; casting &lt;spell&gt;"** (8127) when the cast starts and **"&lt;caster&gt; casts
  &lt;spell&gt;"** (1501) at its release (about 2 s later for Barkskin), before the spell's own lines.
- **The caster's own player always reads the name** ("Aluvian Darkstar casting Magic Missile").
- **Everyone else rolls Spellcraft, once per cast**: a success names the spell in both lines, a failure gives
  "unknown spell" (1502) in both. Her own henchman's casts too ("Hench casting unknown spell" at Spellcraft 7,
  "Hench casting Barkskin" at 57). Barkskin (innate level 2) 5 m away was named 17 of 27 times at rank 7, 7 of 10 at
  12, 13 of 13 at 17 and always at 57: d20 + rank against **15 + the innate level** fits (10 + level does not). The
  DC is inferred from those rates. A specialist wizard rolls at +2 for her school and -5 for her opposition school
  (not measured). Spellcraft can't be used untrained, so a player with no ranks never reads a name (from
  skills.2da).
- **Range** (Spellcraft 57): both lines at 26 and 28 m; "casting" only at 30-35 m; nothing at 36 m and beyond. So
  "casting" to 35 m, "casts" to 28 m (`CastingRange`, `CastsRange`).
- **No lines** for instant casts (bInstantSpell); cheat casts print them.

Measured with `castlines3` and `castlines4` (Spellcraft 57):
- **Creature abilities and feats** (spells.2da UserType 2 and 3) print no "casting" / "casts" line. They print their
  **AltMessage** as they go off: "c_aura_5 is surrounded by an aura." (Aura of Unearthly Visage), "Thamat uses fear
  gaze." (a familiar's gaze), "Aluvian Darkstar summons familiar." (the Summon Familiar feat, the player's own and a
  wizard's). Read 5, 20 and 30 m away, not 40 m (`CastingLines.AltMessage`, `AltMessageRange` 32.5 m).
- **Items** (potion, scroll, wand) print no casting lines but **"&lt;user&gt; uses &lt;item&gt;"** (63264), the player's own
  and NPCs': an NPC's potion is read at 5, 20, 25 and 30 m, not 35 or 40 m (`CastingLines.Uses`, `UsesRange`
  32.5 m); a wizard's wand and the player's potion, scroll and wand print it too.
- **Lost Item**: a drunk potion and a read scroll go without it; a single-use wand used up prints it, as does a carried
  item a script destroys. OpenWinter marks a used-up item of a kind that stacks (`Item.UsedUp`; the stacking rule is
  inferred from those three). "Acquired Item" / "Lost Item" name an unidentified item by its base item ("Acquired
  Item: Magic Wand").
- **"Summoned a creature"** (68518) to the player when her summon or familiar appears: the Summon Familiar feat,
  SummonFamiliar, EffectSummonCreature and a wand; nothing for an NPC's familiar (`CreatureRules.SummonedLine`).
- **Fake casts** (ActionCastFakeSpellAtObject) play the cast and print nothing.
- **"will not stack"** (66247 natural, 66248 armour, 66249 shield, 66250 deflection): a player given an AC bonus of a
  kind she already has from another effect is told once as it lands, whether larger or smaller (four natural bonuses:
  three lines), Barkskin cast twice too (after its "casts" line). Dodge bonuses stack and say nothing; an NPC's tell
  the player nothing (`EffectSystem.NotStacking`). Items giving such a bonus (the line's "items equipped") are not
  measured.

## Loading-screen hints

Measured with `loadtips`: every area load prints **"Loading Screen: &lt;hint&gt;"** (7166 and ": ") in the chat window,
the module's first load included (before the joining line) and every transition, back to an area already visited
too; a jump within one area prints nothing. The hint is a random loadhints.2da HINT on each load. OpenWinter's
loading screen copies the hint it shows into the chat (`LoadingScreen.AreaHint`). When the Loading Hints option is
off it shows none and prints none.

## SetCommandingPlayer

nwscript.nss: "Makes oCreature controllable by oPlayer, if player party control is enabled". nwserver's
`server.player-party-control` is off by default. OpenWinter keeps the override (`Creature.CommandingPlayer`) and
consults it through `World.CommandingPlayer` / `AssociateService.CommandableBy` only when `World.PlayerPartyControl`
is on: then the named player gives the creature's associate commands and its master no longer does; an override
naming anything but a player leaves nobody. With party control off it changes nothing.

**Party control mode has no player-facing feature.** nwserver's settings schema lists `server.player-party-control`
(default false) without a label or talk-table string, so neither the server's options nor the client's show it. With
the setting on (scene `partycontrol`, using the scene option `serverSettings`) and a henchman given to the player with
SetCommandingPlayer, the henchman's radial from the world and from its party portrait has the usual associate ring and
henchman orders; clicking its portrait only shows its tooltip; movement keys walk the player, never the henchman;
GetIsPC, GetIsPossessedFamiliar and GetIsDMPossessed on the henchman read 0 throughout, and nothing is printed.
OpenWinter keeps the setting and the override as above.

## Testing

- Unit tests: `PartyTests`.
- Visual scenes on nwmain: `mp_partyui`, `castlines`, `castlines2`, `castlines3`, `castlines4`, `loadtips`,
  `partycontrol` (`python tools/visual/visual.py run <scene>`). See [MULTIPLAYER_HARNESS.md](MULTIPLAYER_HARNESS.md)
  for running two OpenWinter clients against one server.

## Measured with a second player through the NWN bridge

One original client on nwserver with NWNX WSNetLayer, and one or two more players joined through the NWN bridge
([NETWORKING.md](NETWORKING.md)), so the original client could watch another player's party actions and take part:
- Every line in the table above reached the players it names, with that text (the party lines in the feedback
  colour; the join and leave lines in the server's). The joining player reads her own name in "X has joined the
  party."; "Ignore" also refuses later invitations with "X is ignoring your Party Invitation." and no "sent" line.
- **The invitation window** shows "You have been invited by X to join a party.", "Never show this again." and the
  buttons **Accept, Reject and Ignore**. An unanswered invitation runs out after about 30 s: the invited player
  reads 6468 "The Party Invitation from X has already expired." and the window closes by itself.
- **The classic player list rows**: the character's name; the Attitude column reads **Party** for a member of your
  party, otherwise Like or Dislike; the Reaction column "Friendly" in green; no mark for the party's leader. The list
  updates while open. "PvP Zone" names the area's setting (No PvP in a No PvP area of a Party Protected server).
- **Dislike** is mutual and each player reads "X now dislikes you!" about the other (the line ends in a line break,
  shown as an empty line); **Like** changes only the liker's attitude.
- **A leader leaving the server**: the others read only "X has left as a player..", no party line and no new leader
  line.
- The party bar shows the other players of the party.

## Not measured

- The radial's Party ring and its places, the ignore time, who leads after the leader leaves (when three or more
  remain), the One Party Only join, the kill-experience split between players, a second player commanding another's
  associates.
- The colour of a disliked player's row in the classic player list.
- Creatures' spells cast at a target, for their casting lines (the test creatures never started such a cast).
- Whether the single-player server reads `server.player-party-control` at all.
- Encounter difficulty by the party's level (`EncounterSystem.PartyLevel`): see [ENCOUNTERS.md](ENCOUNTERS.md).
