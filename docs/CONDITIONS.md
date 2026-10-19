# Conditions (effects): rules and presentation

How every condition an NWScript `EffectXxx` call can apply is handled, matched against the original. Rules are
measured on nwserver with the parity suites `condition_actions`, `cond_confuse`, `cond_fear`, `cond_charm`,
`cond_charm_ai`, `cond_timestop`, `cond_misc`, `cond_poison`, `state_scripts` and `state_commandable`; appearance and
player behaviour on nwmain with the visual scenes `conditions`, `coupdegrace`, `conditionfreeze`, `conditionai` and
`conditionpcai`.

The parity modules need `"factions": "Prelude"` for these suites: without a repute.fac the original (and OpenWinter,
suite `faction_nofac`) reads every NPC faction as a friend (100) of every other, so nobody is anyone's enemy.

Plot and immortal creatures refuse most conditions in the original, so rule suites must use ordinary, mortal targets.

## Rules (server)

| Behaviour | The original (nwserver) | Where |
|---|---|---|
| Plot and immortal creatures | Refuse sleep, stun, paralysis, daze, confusion, fear, charm, domination, petrification and turning, even with IgnoreEffectImmunity. | `EffectImmunity.BlockedBy` |
| Plot creatures | Also refuse every other harmful effect: knockdown, entangle, cutscene paralysis and domination, slow, blindness, deafness, silence, curse, negative levels, poison, disease and every decrease. IgnoreEffectImmunity gets these through. Cutscene immobilisation and all bonuses still land. | `EffectImmunity.BlockedBy` |
| Immunity feedback | A player kept safe by an immunity reads "Aluvian Darkstar : Immune to Sleep." (dialog.tlk 62453-62474). | `EffectSystem.Refused` |
| Commandable flag | Cleared at once (GetCommandable reads FALSE in the script that applied it) by sleep, stun, paralysis, petrification, knockdown, confusion, domination, cutscene domination and cutscene paralysis; by charm, fear and turning once that script is over (TRUE in it, FALSE 0.1 s later, also when the state script never calls SetCommandable). It is back when the last of them ends. Daze, entangle and cutscene immobilisation leave the creature commandable. | `EffectSystem.Lock` |
| The action queue | Emptied when any of those conditions lands, daze included. | `EffectSystem.Lock` |
| Move orders | `ActionMoveToLocation` is accepted even by an uncommandable creature (SetCommandable(FALSE) included). A charmed, frightened, confused, dominated, turned or knocked-down creature walks when ordered. | `CommandFunctions.MoveToLocation` |
| Can't move | A creature that is held, asleep, stunned, petrified, entangled, cutscene-paralysed or immobilised drops a move at once, and doesn't take it up again when the condition ends. | `MoveAction.Step` |
| Attacks | None from helpless, dazed, frightened, turned or knocked-down creatures. A dazed creature drops an attack order. | `CombatSystem.CanAttack`, `AttackAction` |
| Waking | Damage wakes a sleeper. Nothing else breaks on damage (1 point of magic damage, taken while charmed, confused, stunned and so on). | `HitPoints.Damage` |
| Coup de grace | Any attack, melee or ranged, by a player or an NPC, on a creature that is asleep (`EffectSleep`) or held (`EffectParalyze`) kills it outright: no roll, hit points -11, no OnDamaged (OnPhysicalAttacked runs). Immunity to critical hits or to death doesn't help. Stunned, petrified, cutscene-paralysed, knocked-down, entangled and dazed creatures are attacked normally. The log reads `Coup De Grace : Aluvian Darkstar attacks Bandit : *automatic hit* : (20 + 0 = 20)`, and "*Coup De Grace!*" floats over the attacking player (not in the chat). | `CombatSystem.CoupDeGrace` |
| Etherealness | Hidden from foes only. The player sees an ethereal merchant. | `PerceptionSystem` |
| State scripts | Charm, confusion, fear, turning, stun, paralysis and sleep run their statescripts.2da script on every heartbeat, player or not, whatever the creature's event scripts (see "Control taken away"). | `StateScripts`, `World.Beat` |
| Attacking a faction mate | An NPC's ActionAttack on a member of its own faction that it regards as a friend is dropped at once (suite `attack_mate`: two members of the standard Hostile faction, reputation 100; members of a faction at 50 to each other, and a personal friend in another faction, are attacked). | `AttackAction` |
| Reputation under charm | A charmed NPC regards its charmer as a friend; nothing else changes. | `FactionTable.ReputationOf` |
| Sanctuary | An NPC that fails its Will save can't attack the protected creature; players are never kept away. A summon's save is shown to its master. | `PerceptionSystem`, `CombatSystem` |
| Deafness | One verbal spell in five fails, cheat casts included ("Spell failed!"); somatic-only spells never. | `CastSpellAction.DeafFailure` |
| Cutscene immobilisation | An attacker keeps its attack action and attacks what is in reach without stepping round it; with its target out of reach GetAttackTarget is OBJECT_INVALID. | `CombatSystem.StepAround`, `ReportedTarget` |
| Negative levels | No loss of hit points, hit dice, base attack or spell uses (`cond_misc`); plot creatures refuse them. | `EffectSystem` |
| Time stop | Never lands; nothing freezes (as nwserver and single-player nwmain, `cond_timestop`). | `EffectSystem` |
| Swarm | Summons the swarm's creatures in turn (`cond_misc` swarm.*). | `EffectSystem` |
| Poison scripts | poison.2da Script_1 runs on the victim when the first save fails, Script_2 when the second does (Sassone Leaf: 2d12 acid; Carrion Crawler: paralysis made by the victim itself, though the script links it with an unset effect; Blue Whinnis: sleep; Pit Fiend Ichor: death). A secondary that is only a script reads 2 in the poison's second integer. EffectAbilityDecrease with a negative amount (Shadow Essence) doesn't land. | `EffectSystem.Afflictions` |
| Death | The measured strip list: unyielding invisibility, sanctuary, etherealness, cutscene immobilisation and domination go; blindness, darkness and improved invisibility stay; the effect whose application killed stays (`death_keeps`). | `EffectSystem` |

## Native and scripted

What the engine does by itself and what the stock scripts add: the creature's event scripts (`nw_c2_default*` with
`nw_i0_generic` / `x0_inc_generic`) and the state scripts named in statescripts.2da (`nw_g0_charm`, `NW_G0_confuse`,
`NW_G0_fear`, `x2_sig_state`, `NW_G0_sleep`). The native part was measured on creatures with every event script
removed, on the player with every stock AI script replaced by a stand-in that only logs (scene `conditionpcai`), and
with statescripts.2da overridden by scripts that only log (`state_scripts`). Fleeing, wandering and charmed fighting
all come from the state scripts, which OpenWinter runs unchanged; the native part is in `EffectSystem.Lock`,
`StateScripts`, `World.Beat`, `MoveAction`, `CombatSystem` and `FactionTable.ReputationOf`.

| Condition | Native (engine) | Stock scripts |
|---|---|---|
| Sleep, stun, paralysis, petrification, cutscene paralysis | Queue emptied, uncommandable at once, can't move or attack; sleep and hold give coup de grace. | `bkEvaluationSanityCheck` gives up the combat round for paralysis, stun and sleep; `DetermineCombatRound` returns at once when petrified. |
| Daze | Queue emptied, attack orders dropped, stays commandable, walks. | The round is given up. |
| Fear, turning | Uncommandable once the applying script is over; NW_G0_fear runs on each heartbeat. | `NW_G0_fear`: ClearAllActions, then ActionMoveAwayFromObject (run, the default 40 m) from the nearest enemy within 5 m, if any. Event scripts: fear gives up the round; turning is not checked (the AI's orders are refused: uncommandable). |
| Confusion | Uncommandable at once; NW_G0_confuse runs on each heartbeat. | `NW_G0_confuse`: d10, 1 ActionRandomWalk, 2-6 nothing, 7-10 ActionAttack on the nearest creature (dropped by the engine when that is a friendly faction mate). Event scripts: `DetermineCombatRound` runs, its orders are refused, and it clears an ACTION_RANDOMWALK it finds. |
| Charm | Uncommandable once the applying script is over. An NPC regards the charmer as a friend. `nw_g0_charm` runs on each heartbeat, player or NPC. | `nw_g0_charm`: SetCommandable(TRUE), the generic `DetermineCombatRound` (weapon, protection spells, harmful spells and scrolls at the nearest enemy, summons, melee: see "Control taken away"), SetCommandable(FALSE). |
| Domination | Uncommandable at once. By a player: a dominated associate ([ASSOCIATE_COMMANDS.md](ASSOCIATE_COMMANDS.md)), whose henchman scripts then run. By an NPC: nothing else; it stands. | The associate scripts, for a player's. |
| Knockdown, entangle, cutscene immobilise | Movement only (knockdown also empties the queue and locks). | Not checked. |
| Deafness, silence | Silence: no verbal spells. Deafness: one verbal spell in five fails. | Not checked. |
| Sanctuary | An NPC that fails its Will save can't attack the protected creature; players are never kept away. | `InvisibleTrue` (nw_i0_generic) counts the creature's own sanctuary as being hidden. |
| Poison | Saves, damage, and the poison.2da scripts. | The scripts named in poison.2da (run on the victim). |

## Control taken away

The engine runs the state's statescripts.2da script on the creature's heartbeat (suite `state_scripts`, with every
row's script replaced by one that logs):

- **When.** The condition brings the heartbeat forward: OnHeartbeat (if any) and then the state script run in the
  frame it lands. Then on every heartbeat, 6.0-7.2 s apart at random (6.6 s on average), with or without an
  OnHeartbeat script. Charm, confusion, fear, turning, daze, stun, domination, paralysis, sleep, petrification and
  cutscene paralysis bring the heartbeat forward; knockdown and entangle don't.
- **Which.** The highest statescripts.2da row among the creature's states: confused + frightened runs row 3 (fear),
  charmed + confused row 2, asleep + confused row 9. Rows 1 charm, 2 confusion, 3 fear, 4 turning, 6 stun, 8 paralysis
  and 9 sleep run their script. Daze (5) and domination by an NPC (7) never run theirs, and while either is the
  highest row nothing runs at all.
- **Associates.** A creature with a master skips its own OnHeartbeat while it has a state (a confused henchman of an
  NPC runs only the state script); others run OnHeartbeat first, then the state script.
- **Commandable** (suite `state_commandable`): confusion, stun, paralysis, sleep and domination make the creature
  uncommandable inside the script that applies them; fear, charm and turning once that script is over (still
  commandable in it, not 0.1 s later); inside any state script it reads uncommandable. A daze makes it commandable,
  even one a script had made uncommandable, and it stays so.
- **One state at a time.** A state applied under an equal or higher one does nothing yet: confused while dazed, the
  creature stays commandable and keeps its queued ActionWait. When the daze ends the confusion takes over as if it had
  just landed: its script runs at once, the queue is emptied, uncommandable. Frightened and stunned, the stun removed:
  the fear script runs at the removal and the creature stays uncommandable. When the last state ends the creature is
  commandable once the removing script is over (still not inside it), even if a script had set it uncommandable
  before. `EffectSystem.Lock` / `Unlock`.

So the choices below are the stock scripts', and a module that overrides statescripts.2da or the scripts changes
them. Whatever the creature is doing when the condition ends, it goes on doing.

- **Fear and turning** (`cond_fear`, `NW_G0_fear`): the first decision comes at once (0.25 s after the effect). If a
  creature it regards as an enemy stands within 5 m (4 m: yes; 5 m and 5.6 m: no), it runs straight away from it
  (ActionMoveAwayFromObject, which goes on until it is 40 m further off than where it started); at the next heartbeat
  it stops unless an enemy is near again (one that walks or fights its way up to 1.8-3.2 m sends it running again).
  Who made the effect doesn't matter: frightened by the module or by a friend with nobody hostile about, it stands. A
  fear that ends mid-flight leaves the run going to its full 40 m. A frightened player does the same (scene
  `conditionai`).
- **Confusion** (`cond_confuse`, traces of 25 creatures for 60 s; `NW_G0_confuse`): the first decision comes at once.
  One heartbeat in ten it walks at random (ACTION_RANDOMWALK: a walk of 5-8 m, a pause of 2-3 s, a walk back); four in
  ten it attacks the nearest creature (any reputation: a personal friend in another faction is attacked; it runs 13-18 m
  to the nearest when nobody is close), unless that creature is a faction mate it regards as a friend, when the engine
  drops the attack and it does nothing (`attack_mate`); otherwise it does nothing, which stops a walk or an attack
  under way. The creator gets no special treatment. The player behaves the same (`conditionai`, `conditionpcai`).
- **Charm** (`cond_charm`, `cond_charm_ai`, `rulesfollowpc`): the charmed NPC's own view of the charmer becomes friend
  (100) while it lasts; its view of the charmer's faction, the charmer's view of it and all faction tables stay as they
  were; it is not an associate. A charm made by the module (the effect's creator is whoever constructed it) changes
  nothing. `nw_g0_charm` then runs the generic combat AI each heartbeat, which gives (measured with stripped level-1
  wizards carrying the pregenerated sorceress's spells, and with the player herself):
  - Its enemy is the nearest creature it **sees** and regards as an enemy; a charmed player leaves her charmer out of
    this although she still regards it as an enemy, but goes for it in melee when it is the nearest. With no enemy in
    sight it does nothing (a creature that hasn't perceived anyone yet).
  - The first decision comes at once; it takes up its most damaging melee weapon then (unless that first action is a
    summoning), even with nobody in sight. Later decisions come 6.0-7.1 s apart (6.6 on average).
  - Each round one action: a protection spell (spells.2da Category 12/13) it doesn't have on, a harmful spell at the
    enemy (Category 1 area, then 2 ranged, then 3 touch) or a summoning (Category 15) while it has no summons, preferring
    them in that order but not the same kind twice running when another is open. With the enemy within 5 m (5 m yes,
    6 m no) it neither protects itself nor summons. Its own spells come before its items' (scrolls), highest level
    first, never the same spell twice running when another is left; spells of one level are picked among at random.
    With nothing left it fights in melee.
  - Example, against an enemy 9 m off: Mage Armor, Ray of Frost, Summon Creature I, Daze, then the dagger; the player
    with scrolls: Mage Armor, the Sleep scroll, Summon Creature I, Ray of Frost, Daze, the Magic Missile scroll, the
    dagger.
  - A creature struck by harmful spells sometimes strikes back. That is an attack of opportunity: the caster's next
    spell, cast within its reach while it is in combat and facing it, provokes one, and a creature with no attack
    target is then ordered to attack the provoker (suite `strike_back`). The server never orders a counterattack
    otherwise (`CombatSystem.AttackOfOpportunity`, casts by NPCs provoking).
- **Domination by an NPC** (`cond_charm`): no master, no associate, no change of reputation; the creature stands.
  A dominated player stands too.

## Presentation (client)

The server sends conditions as `ObjectFlags` on spawn and update (`GameServer.ConditionFlags`):

| Flag | Shown as | Where |
|---|---|---|
| `Asleep` | Falls forwards (kdfnt) and lies face down (deadfnt). When woken it pushes itself up (gutokdf) and stands (gustandf). A sleeper killed by a coup de grace stays lying. | `CreatureAnimator.Conditions` |
| `KnockedDown` | Falls back (kdbck) and sits up (kdbckps) for the whole duration, then stands (gustandb). | same |
| `KnockedForward` | Knocked down from behind (the effect's creator behind it): falls forwards (kdfnt), kneels (kdfntps), stands (gustandf). | same |
| `Held` | Stands still: animation frozen. | `CreatureView.Conditions` |
| `Petrified` | Frozen, with progfx Petrify's stone skin (`vdu_tex_stone`). | same |
| `Stunned` | Blue stars round the head: VFX_DUR_MIND_AFFECTING_DISABLED's model (vdr_mindhit03), with no visual effect from the script. | `EffectPlayer.Conditions` |
| `Translucent` | Invisibility, sanctuary or etherealness: the player sees herself half transparent. Other creatures with them are not translucent. | same |
| `Blinded` | Blindness or darkness on the player: the world drops to about a ninth of its brightness. | same |

- Head visual effects (`headconjure`) follow the head, so the Zs of Sleep and the stun stars go down with a
  creature that lies down, as in the original. In the models that node hangs off the root (`HeadConjureFollow`).
- **Portrait icons** use the original's rows: blindness shows BLIND (BLINDNESS has no icon), etherealness
  ETHEREALNESS and petrification PETRIFIED. A dazed player also shows the movement-speed-decrease icon before the
  daze icon.
- **Chat.** No line is written when a condition lands, only "Immune to ..." when one is refused.

## Condition by condition

Presentation is compared with the original's frames in the scenes `conditions`, `coupdegrace`, `conditionfreeze` and
`conditionai`.

| Condition | Rules | Presentation | Ending |
|---|---|---|---|
| Sleep | Plot/immortal refuse; queue emptied; moves dropped; damage wakes; coup de grace | Falls, lies face down, gets up; Zs follow the head | Damage or expiry |
| Stun | Plot/immortal refuse; queue emptied | Stars | Expiry |
| Paralysis (hold) | Plot/immortal refuse; queue emptied; coup de grace | Frozen | Expiry |
| Daze | Plot/immortal refuse; queue emptied; attack orders dropped | Two icons; no body change, as in the original | Expiry |
| Confusion | Plot/immortal refuse; locked; queue emptied; NW_G0_confuse on each heartbeat | Icon | Rates match within tolerance (`cond_confuse`) |
| Fear | Locked after the applying script; queue emptied; plot/immortal refuse; NW_G0_fear flees from an enemy within 5 m | Icon; the player runs | Stops at the next heartbeat; a flight under way runs on |
| Charm | Locked after the applying script; queue emptied; plot/immortal refuse; an NPC's charmer is a friend; nw_g0_charm's generic AI round for NPCs and players | Icon | |
| Domination | Locked; queue emptied; plot/immortal refuse; by a player a dominated associate, by an NPC nothing | Icon | |
| Knockdown | Queue emptied; moves allowed; plot refuses | Sits up, or kneels when knocked from behind | Expiry, stands |
| Entangle | Move dropped; plot refuses | Icon; no body change, as in the original | Expiry |
| Petrify | Plot/immortal refuse; queue emptied | Stone and frozen | Expiry |
| Cutscene paralysis | Plot refuses; queue emptied | Stands | Expiry |
| Cutscene immobilise | Move dropped; no side-steps; attack action kept; no attack target out of reach | Stands | |
| Turned | Locked after the applying script; plot/immortal refuse; NW_G0_fear (row 4) flees like fear | Icon | |
| Slow, haste | Matched | Icons | |
| Blindness | Matched; plot refuses | Icon row, dark view | |
| Deafness, silence | Deafness fails one verbal spell in five; silence blocks verbal spells; plot refuses | Icons; "Spell failed!" as the original | |
| Poison, disease, curse | Matched (suites `fx_poison`, `fx_disease`, curse); poison.2da scripts (`cond_poison`); plot refuses | Icons | |
| Negative levels | No loss of hit points, hit dice, base attack or spell uses; plot refuses | Icon row LEVELDRAIN | |
| Ability/AC/attack/skill/save/damage/SR decreases | Matched; plot refuses | Icons | |
| Invisibility | Matched | Own player translucent; others vanish | Broken by attacking |
| Sanctuary | NPCs that fail the save can't attack; players unaffected; a summon's save shown to its master | Icon, own player translucent | No line when a player's sanctuary stops an NPC |
| Ethereal | Hidden from foes only | Icon, own player translucent | |
| Darkness | Matched | Dark view, icon | |
| Concealment, see invisible, true seeing, ultravision | Matched | "Aluvian Darkstar attacks Goblin : *target concealed: 50%* : (12 + 0 = 12)"; an invisible creature is not drawn, and with see invisibility or true seeing it is drawn whole (not translucent); see invisibility's portrait icon | |
| Damage reduction, resistance, immunity, regeneration, temporary hit points | Matched | OpenWinter prints dialog.tlk's "Goblin : Damage Reduction absorbs 3 damage" | |
| Polymorph | Matched (`fx_poly` suites) | See [POLYMORPH.md](POLYMORPH.md) | |
| Spell resistance, spell level absorption, spell immunity | Matched | "Goblin attempts to resist spell : spell resisted" (spell resistance, no roll shown); "Goblin : Immune to Magic Missile" then "Goblin attempts to resist spell : magic immunity" (spell immunity); "... : magic immunity" for a globe (unlimited absorption) | |
| Death | The measured strip list (see the rules table) | | |
| Time stop | Never lands, nothing freezes | Nothing to show | |
| Swarm | Matched (`cond_misc` swarm.*) | The summons' own appearance | |

## Testing

```
dotnet run --project src/OpenWinter.Tools -c Release -- parity-run cond_fear
python tools/visual/visual.py run conditions
```

Server suites: `condition_actions`, `cond_confuse`, `cond_fear`, `cond_charm`, `cond_charm_ai`, `cond_timestop`,
`cond_misc`, `cond_poison`, `state_scripts`, `state_commandable`, `attack_mate`, `strike_back`, `death_keeps`,
`faction_nofac`. Visual scenes on nwmain: `conditions`, `coupdegrace`, `conditionfreeze`, `conditionai`,
`conditionpcai`, `rulesfollowpc`.

## Not matched yet

- **A creature chasing one that flees** (`cond_fear` fear.attacked, fear.stock.attacked): on nwserver the attacker
  falls behind, from 4.7 m to 6.5 m in 3 s (about 85% of the runner's speed), so at the next heartbeat no enemy is
  within 5 m and the frightened creature stops. In OpenWinter the attacker keeps pace 4.8 m behind and the creature
  runs again.
- **Strike back**: GetAttackTarget flickers to OBJECT_INVALID between rounds on nwserver; OpenWinter keeps it.
- A bandit ordered to walk sets off about 0.7 s later on nwserver than in OpenWinter, whatever the condition
  (`*.move.dx` tolerance 1.4 m).
- nwserver equips arrows by itself when a bow is equipped from a script. OpenWinter doesn't (the suite equips
  them explicitly).
- On a kill, the original prints "Experience Points Gained: 20" and "<killer> killed <victim>" before the attack
  line. OpenWinter prints neither.
- The original's frightened player stopped 20.7 m out, short of the 40 m aim, near the area's edge; OpenWinter's ran
  25 m before the next decision stopped her (the edge's walkable limit differs).
- The original prints "Aluvian Darkstar casting Light" and "... casts Light" for spells a script makes the player
  cast; OpenWinter prints neither.
- The order of the poison and ability-decrease portrait icons differs.
- That a held creature's animation freezes is not proven: the original's idle is too still to tell apart in paired
  shots.
- Not measured: negative levels on a level-1 player (who dies); the attacker's own miss-chance line ("attacker miss
  chance") and ultravision; a mantle's "spell absorbed" line; spell resistance that fails (dialog.tlk has no line, and
  OpenWinter prints nothing); damage reduction's line in the original (the test attacks missed or pierced).
