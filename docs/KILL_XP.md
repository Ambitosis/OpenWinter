# Kill experience

What the original gives when a creature dies, measured black-box on `nwmain` with the visual scenes `killxp`,
`killxp2`, `killmsg` and `killxp3` (run with `python tools/visual/visual.py run <scene>`; `killxp3` at difficulties 1
and 4, every reading identical). Implemented in `src/OpenWinter.Game/Rules/KillRewards.cs`; the numbers are in
`tests/OpenWinter.Tests/KillRewardTests.cs`. The chat lines are in [COMBAT_PRESENTATION.md](COMBAT_PRESENTATION.md).

The scenes kill copies of `nw_goblina` with 1 hit point and a set challenge rating (the visual fixture field
`"challengeRatings": [..]` makes one blueprint per value) with the player's EffectDamage, and log every party
member's experience before and after. The player is a fixed test character, an elf wizard 1; her row
of the table is chosen with SetXP, her classes with LevelUpHenchman.

## The amount

    xp = trunc( xptable.2da[party level][C(CR + 0.49)] x (scale / 10) / (players + associates / 4) )

in single precision, then the multiclass penalty (below). It is given **after the victim's OnDeath script** (GetXP
there still reads the old value).

- **Row: the party level**, the counted players' average level, truncated and kept to 1..40, where a player's level
  is the level her experience gives if that is higher than her hit dice. A level 1 player with 200,000 experience gets
  level 20's row (4 for a CR 1 kill); a level 2 player with 780,000 gets level 40's; 35,999 experience is still level
  8's row. With one player that is her own level; the average over several players is not measured
  (the harness has one client).
- **Column: the victim's challenge rating plus 0.49, truncated**, kept to 0..40: 0.5 and 0.505 give C0, 0.51 and 1.505
  give C1, 1.51, 2.5, 2.505 and 2.509 give C2, 2.51 gives C3; -1 gives C0; 40.5 and 60 give C40. There is no
  fractional-CR treatment (`fractionalcr.2da` plays no part).
- **Scale** is the module's XP scale (`Mod_XPScale`, SetModuleXPScale) in tenths: 10 gives the table as it is,
  1 a tenth, 200 double. SetModuleXPScale(250) reads back 200. At 0 nobody gets anything.
- **Single precision, truncated**: 50 at scale 21 gives 104 (50 x 2.1 = 104.99999 in single precision), at 42 209,
  at 47 234; 90 at scale 13 gives 116; 396.75 gives 396, 100.5 at scale 199 gives 1999, 4.8195 gives 4.
- **Game difficulty plays no part** (killxp3 at Easy and Very Difficult gave the same 23 kills).
- Rows checked cell by cell: levels 1-5 (scale phase), 8, 9, 11, 13, 14, 18, 19, 20, 21, 30, 40 against
  `xptable.2da`; every reading matched.

## Who shares

- Only a death with a **killer in a player's party** counts: the player, or a henchman, familiar, summon or other
  associate whose master is a player. A neutral bystander's kill, the module's or an area script's EffectDamage or
  EffectDeath give nothing to anyone.
- The party members that count are **the killer, wherever it is, and every party member within 40 m of the
  victim** (a henchman 40 m from the victim shared, 41 m did not; measured from the victim, not the player).
- Each counted associate weighs a quarter of a player: one associate leaves the player 1/1.25 (300 -> 240),
  two 1/1.5, three 1/1.75, four 1/2. **Associates gain nothing** themselves.
- A **summon's, familiar's or henchman's kill** pays its master the same share as her own kill would.
- A player far from the victim who did not kill it gets nothing (her henchman's kill 54 m away gave her 0).
- **No experience for killing a party member** (her own henchman), nor for a **summoned creature, familiar or animal
  companion** of anyone (a hostile bandit's summoned dire badger gave 0; the same blueprint created plainly gave 144);
  another creature's **henchman** gives full experience (300). Commoner, merchant and defender victims give full
  experience like hostile ones.
- **Dead party members don't count**: a dead henchman beside the victim left the player the whole 300 (a live one
  makes it 240).
- **Multiclass penalty:** the amount is then cut by 20% for each class more than one level below the highest,
  leaving out prestige classes (`classes.2da` XPPenalty 0) and the race's favoured class (`racialtypes.2da` Favored;
  `****` means the highest class). An elf wizard 1 / rogue 1 / fighter 3 got 100 for a 126 kill and 3 for 4.2;
  wizard 36 / rogue 1 got the full amount; wizard 1 / rogue 4 / fighter 2 (fighter two below rogue) got 59 for 74,
  and with fighter 3 (one below) the full 54. **GiveXPToCreature takes the same penalty** (100 gave 80, 7 gave 5);
  SetXP does not.

## Not measured

- **More than one player.** The harness has one client, so the split between several players is not measured.
  OpenWinter gives every counted player the same share from the party-average row, divided by
  (players + associates / 4). A possessed familiar's kill is not measured either.
- **Killing a player** (PvP): OpenWinter gives no experience for a player victim; not measured.
- **Traps** (whether a trap's creator is credited) and the official campaigns' CR shift: not measured, not in OpenWinter.
- **Encounter creatures** (GetIsEncounterCreature): the scene builder can't place encounters; OpenWinter makes no
  difference.
- **Plot creatures** can't be killed, so the question doesn't arise. A friend made with SetIsTemporaryFriend was
  not tried.
- The favoured class of a human with a monster or prestige class: OpenWinter picks the highest class that takes the
  penalty; whether the original can pick a monster class here is not measured.
- The DM client's give-experience: OpenWinter leaves it without the penalty.
