# Ordering associates

Right-clicking one of your own associates (summon, familiar, henchman, dominated creature, and presumably the animal
companion) gives the commands that order it about, not the ordinary creature ring. The same ring opens from the
associate's party portrait. Both the classic and the modern interfaces build it from the shared radial model
(`client/UI/Hud/RadialMenu.cs`, `AssociateRoot` / `AssociateActions` / `AssociateAbilities`).

## The ring (measured)

Root, clockwise from N, the same for every associate type and from the world or the party portrait:

| Dir | Entry | Icon | TLK | Command |
|---|---|---|---|---|
| N | Heal Me | `ir_healme` | 1483 | ASSOCIATE_COMMAND_HEALMASTER (-4) |
| NE | Cast Spell | (spell ring) | | |
| E | Attack Nearest | `ir_attacknearest` | 5345 | ATTACKNEAREST (-3) |
| SE | Follow | `ir_follow` | 682 | FOLLOWMASTER (-5) |
| S | Stand Your Ground | `ir_standgrnd` | 1477 | STANDGROUND (-2) |
| SW | Guard Me | `ir_guard` | 1474 | GUARDMASTER (-7) |
| W | Special Abilities | | | the player's own ring |
| NW | associate actions | `ir_assoc_action` | 1478 (inferred) | sub-ring below |

There is no Attack and no Special Attacks on it, and the associate's portrait sits in the centre. The W ring is the
player's own Special Abilities ring (Craft N, Taunt NE, Search E, Summon/Possess Familiar SW, Stealth W) with Taunt
offered against the associate. Using it is refused (below).

NW ring, Examine N in every case, then:

| Associate | Entries |
|---|---|
| Summon | Unsummon SE (`ir_unsummon`, 1569, -10) |
| Familiar | Possess Familiar E (`ife_possess`), Unsummon SE (-8) |
| Dominated | Release from Domination SE (`ir_reldom`, 6393, -12) |
| Henchman | Pick Lock NE (`ir_picklock`, 1467, -18), Inventory E (`ir_inventory`, -19), Remove From Party SE (`ir_kick`, 1068, -17), Disarm trap S (`ir_hand_trap`, -20), Toggle Spell-Casting SW (`ir_spell`, 83327, -21), Toggle Stealth Mode W (`ir_stealth`, 83410, -22), Toggle Search Mode NW (`ir_search`, 83411, -23) |
| Animal companion | not measured; Unsummon SE (-9) like the summon |

The player's own radial keeps its Issue Command ring (SW: Attack Nearest E, Follow SE, Stand Your Ground S, Guard Me
SW, Heal Me W). It orders every associate at once through the voice-chat request, using sound-set slots 22, 10, 9
and 4 for Follow, Stand Your Ground, Guard Me and Heal Me.

## How a command reaches the associate

The client sends `associate-command { target, command }`. `AssociateService.Command` accepts it only for the
requesting player's own associate and only with a command its ring offers (`AssociateService.Offers`). Then:
- **Unsummon** ends the master's summon effect, which plays its unsummon visual and destroys the creature. A familiar
  or companion is removed with `VFX_IMP_UNSUMMON`.
- **Release from Domination** removes the master's domination effects, which ends the service, and prints TLK 61909
  ("You are no longer dominating ..."). `EffectDominated` itself makes its target the creator's dominated
  associate (measured: the target reads ASSOCIATE_TYPE_DOMINATED, the master's faction, reputation 100), as Animal
  Empathy does.
- **Every other command** goes to `AssociateService.IssueCommand`, the path the V-menu voice commands already used.
  It records GetLastAssociateCommand and runs the associate's OnConversation with GetListenPatternNumber() = the
  ASSOCIATE_COMMAND_* value and GetLastSpeaker() = the master, provided the associate called
  SetAssociateListenPatterns (the stock spawn scripts `nw_ch_ac9`, `nw_ch_summon_9`, `x0_ch_hen_spawn` do).

The stock OnConversation scripts (`nw_ch_ac4`, `x0_ch_hen_conv`) pass any non-negative-one pattern from the master to
`bkRespondToHenchmenShout` (`x0_inc_henai`). That function sets the NW_ASC_MODE_* flags (Stand Ground, Defend
Master), queues `ActionForceFollowObject`, runs `DetermineCombatRound` / `HenchmenCombatRound`, toggles stealth,
search and casting, opens the inventory, and fires the henchman. Unsummon and release numbers are not handled there,
which is why the engine does that work itself.

## Hostile acts on one's own associate (measured)

In the original's party-PvP area (single player's default):
- `GetIsEnemy` both ways is 0, `GetIsFriend` 1, reputation 100, same faction, for summon, familiar, henchman and
  dominated creature.
- A scripted `ActionAttack` and `ActionUseSkill(SKILL_TAUNT)` by the player on their summon are refused at once with
  TLK 66245, "You cannot perform that action on a friendly target due to PvP settings". There is no attack and no
  combat.
- The default action (left click) on the summon is Talk: `GetCurrentAction` = ACTION_DIALOGOBJECT while the player
  walks up.

OpenWinter does the same. `World.PvpAllows` refuses a player-driven creature's attack on its own associate unless
PvP is Full. `World.RefusesPvp` sends 66245 for the `attack` request, an `AttackAction` and a hostile skill
(skills.2da `HostileSkill`: Animal Empathy, Pick Pocket, Taunt). The associates' own AI is not affected.

## Testing

Visual scenes `associateradial`, `associatesubmenus` and `associatedominated` (`parity/visual/scenes/`) measure the
original's rings, run with `python tools/visual/visual.py run <scene> --only nwn --native-framebuffer`. Sub-rings are
entered with numpad keys while the right button is held; icons are identified against the installed `ir_*` textures.
The original reads hover labels from the real pointer only, so posted hovers don't show them.

`associateclient` is an OpenWinter-only scene (`--only openwinter`, `--openwinter-args "--ui classic"` or `"--ui
modern"`) that opens each ring with a posted right-click and enters NW and W with numpad keys. The classic captures
match the original's rings for the summon, familiar, henchman (world and party portrait) and dominated creature; the
modern captures show the same entries with labels. Scene `rightdrag` presses on the summon, drags across the ring
with the button held and lets go, then does the same on the ground ring released over a wedge; `rightdragreal`
(`tools/visual/realdrag.py`) does the same with the real pointer.

Unit tests (`AssociateCommandTests`) run the real stock scripts.

## Not matched yet

- **Inferred labels**: NW (1478 "Non-Combat Commands"), Inventory (3731) and Disarm trap (3842) are TLK strings
  chosen to fit. The original's hover labels for them were not captured.
- **Modern party frame**: its right-click opens the ring but has no capture yet.
- **Animal companion's ring**: not measured.
- **Voice**: whether the player speaks a voice line when ordering one associate from its ring is not measured. The
  ring sends no voice, while the Issue Command and V-menu orders still do.
- **Full PvP**: whether a player may attack their own associate there is not measured (allowed here).
- **Hostile spells on one's own associate**: not measured, unchanged.
- **Unsummon visual**: the familiar's and companion's unsummon visual is not measured.
