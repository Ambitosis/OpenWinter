# Weapon sounds, melee sidesteps and the kill line

Measured on the original client with the visual scenes `combatsound`, `combatarmor` and `combatsteps`
(`python tools/visual/visual.py run <scene>`). Implemented in `src/OpenWinter.Game/Combat/CombatSystem.Sounds.cs` and `CombatSystem.Steps.cs`, and
played by `client/Views/EffectPlayer.cs`. Tests: `CombatPresentationTests`.

## How the sounds were found

`tools/visual/combat_audio.py` swaps every sound `weaponsounds.2da` names for its own pure tone, 60 Hz apart, in
the sandbox's override folder only and just for the run. It records the harness-owned `nwmain` with the
process-loopback recorder and lists each tone it hears, with the scene's phase. The scenes script fights with
forced hits (+20 attack) or misses (+20 AC), changing one thing at a time: the attacker's weapon and the armour
the player wears. The original always plays its first pregenerated character, who can't wear heavy armour, so a
ring of armour proficiency feats goes on first.

## Weapon sounds

- **Hit:** the row's two sounds for what the blow lands on, both at once (for example `cb_ht_bladechan1` and
  `cb_ht_bladechan2`). A critical hit adds `cb_ht_critical`.
- **Miss:** one of the row's two swing sounds, picked at random (`cb_sw_blade1` or `cb_sw_blade2`).
- **Parry:** a parried blow plays `Parry0`.
- **Row:** the weapon's baseitems `WeaponMatType` (longsword 1 Blade, club 4 Wood_Blunt). A creature without a
  weapon takes its appearancesndset `WeapType` column for the swing, or Unarmed (0) when that column is empty.
- **Column:** the target's appearancesndset `ArmorType` (a badger: leather). A humanoid (no ArmorType) is matched
  by its body armour's base AC (`combatarmor`):

| Base AC | Column |
|---|---|
| 0 (no armour), 2, 3 | Leather |
| 4, 5 | Chain |
| 6, 7, 8 | Plate |

The sounds play at the swing's "hit" event, 0.5 s into the 1 s attack clips, at the target.

Not measured:
- ranged attacks (the bow rows have only their shot sounds, which are played on every shot);
- blows on doors and placeables (taken as wood);
- crystal creatures (taken as stone);
- when the original plays a parry sound on a miss that isn't a parry (it was heard on some unarmed misses).

## Melee sidesteps

The attacker steps sideways round its target between rounds (`combatsteps`; a dummy logs each attack, and positions
are logged every 0.1 s):
- **Steps:** each step is 0.8 m along the circle round the target, keeping the distance (about 29° at 1.6 m, 23° at
  2 m). The position changes at once. The attacker keeps facing its target through each step, so the client keeps
  the target in front while the sideways clip plays. (GetFacing in a script log may lag the client's turn.)
- **Turning:** OpenWinter faces the target from each step's end at once, and the server sends every turn towards a
  moving target as an update.
- **How many:** usually two steps after a round of one attack, about one after a round of two, so the steps fill the
  rest of the round.
- **Timing:** the first comes 0.4–2.6 s after the swing, the next 0.2–1.5 s later.
- **Side:** chosen per round. Pairs mostly keep it with a weapon and go out and back about half the time unarmed;
  OpenWinter keeps the side three times in four.
- **Targets:** a target that isn't attacking doesn't step.

OpenWinter sends each step as the existing `creature-bump` event, so the client plays a `cwalkl`/`cwalkr` step
that keeps the facing. Steps never go into walls or other creatures; the other side is tried, or the step is
skipped.

Not measured:
- rounds of three or more attacks;
- ranged attackers (they don't step here);
- whether a step can provoke an attack of opportunity.

In the original, an unarmed bandit stood 2.0 m off and one with a longsword 1.6 m. OpenWinter's unarmed distance
there is 1.6 m.

## The kill line

Measured with the `killmsg` and `killxp` scenes (GUI and chat log on, `--native-framebuffer` shots). The rules
for the experience are in [KILL_XP.md](KILL_XP.md); OpenWinter prints both in `Rules/KillRewards.cs`.

- **Text:** dialog.tlk 10474 `<CUSTOM0> killed <CUSTOM1>`: "Aluvian Darkstar killed Goblin".
- **Colour:** like the other combat lines: the killer's name cyan when it is the player's own character, pink for
  anyone else, then " killed Goblin" orange. "Experience Points Gained:  50" (TLK 8263, two spaces) is yellow,
  like any server message.
- **Order:** the blow's own lines first, then the experience, then the kill:
  `Aluvian Darkstar attacks Goblin : *hit* : (17 - 1 = 16)`, `Aluvian Darkstar damages Goblin: 8 (8 Physical)`,
  `Experience Points Gained:  50`, `Aluvian Darkstar killed Goblin`. EffectDamage is the same without the attack
  line; EffectDeath prints no damage line. "Your character has gained enough experience to advance a level."
  comes between the experience and the kill. A coup de grace prints the kill before its attack line.
- **Who sees it:** each player in the killer's party who shares the kill (the killer, or within 40 m of the
  victim): her own kill, her henchman's, summon's or familiar's beside her. Not a bystander's kill, not the
  module's or an area script's EffectDeath, not her henchman's kill 60 m away. Killing her own henchman prints the
  line. A player who is killed sees it whoever killed her ("Goblin killed Aluvian Darkstar"); a henchman's master
  does not see her henchman's death ("Goblin damages Bandit: 5 (5 Fire)" only).
- **Associates' attack and damage lines** reach their master (the henchman's, summon's and familiar's swings, a
  goblin's damage on a henchman), but not when the target is 60 m from her. OpenWinter shows them when she is within
  40 m of the target; the exact distance isn't measured.
- **At scale 0** the kill line still prints and no experience line does.

Not matched or not measured:
- The original names a creature the player hasn't perceived yet "Someone" ("Aluvian Darkstar damages Someone",
  "killed Someone": a goblin made and killed within the same moment). OpenWinter always uses the name.
- The player's own crossbow and Magic Missile kills (a spell's damage is the caster's EffectDamage, which was
  measured).
- Trap kills. OpenWinter prints nothing for it (a trap
  is not a creature), as for the module's EffectDeath.
- What other players in the party, or players nearby, see.
