# Game difficulty

GAME_DIFFICULTY_* (`World.GameDifficulty`, GetGameDifficulty): 1 Easy, 2 Normal, 3 D&D Hardcore Rules, 4 Very
Difficult. 0 ("DM Easy") exists as a constant, but EE's settings offer 1-4 only.

## Where it comes from

- **The original's default is 1 (Easy)**, for single player and servers alike. The settings schema the game writes
  into its `settings.tml` has `ruleset.difficulty` with `default = 1`, `values = [1, 2, 3, 4]`, the ini name
  "Difficulty Level" under "Game Options", label strref 7212 ("Difficulty") and value names 66786/66788/66790/66792
  (Easy, Normal, D&D Hardcore Rules, Very Difficult). nwserver started without `-difficulty` reports 1 (parity suite
  `difficulty_default`, which omits the flag: `"difficulty": 0` in suite.json).
- **OpenWinter**: `World.GameDifficulty` defaults to 1, as does the dedicated server (`ruleset.difficulty` in settings.tml,
  `-difficulty`, docs/SERVER_CONFIG.md). Single player takes it from Options → Gameplay → Game difficulty
  (`GameplaySettings.Difficulty`, default 1); a change applies at once to the running game. `--difficulty N` sets it
  for a capture or the visual harness (`visual.py --difficulty N`, or `"difficulty"` in scene.json, which also
  writes `[ruleset] difficulty` into the original's sandbox settings). A loaded game and a module transition keep
  the current difficulty. Both interfaces share the Options window, so the choice is in both.

## What it changes, measured

Visual scenes `difficulty`, `difficultyhits` and `difficultypc`, each run on nwmain at difficulties 1-4 (`pc` at 1-3),
with the chat window copied to chat.txt; parity suites `difficulty_npc1` and `difficulty_npc4`; and `diffscale` (below),
which checks the rules against about 1,800 blows per difficulty.

The engine reads seven switches per difficulty from **diffsettings.2da** (rows NoCriticalOnPC, NoAoOWithRanged,
NoAoOWithPotion, MinPCDamagePercent, MaxNPCDamagePercent, MinHP1, MassiveDamage; columns DMEasy, Easy, Normal,
Hardcore, DMPlayers for difficulties 0-4; `****` is 0). Stock values:

| | Easy | Normal | D&D Hardcore | Very Difficult |
| --- | --- | --- | --- | --- |
| MinPCDamagePercent: a player's weapon dice on a creature | 50: topped up | - | - | - |
| MaxNPCDamagePercent: a creature's weapon dice on a player | 50: cut | 100 | 100 | 200: raised |
| NoCriticalOnPC: critical hits on a player | no (threat rolled and shown, blow is a hit) | no | yes | yes |

Only the weapon's own dice change (base item NumDice x DieToRoll, a creature weapon's monster damage, unarmed or monk
dice), and only between a player and a creature that isn't one. R is the roll of those dice, M their highest total,
P the percentage, random(k) a whole number 0 to k - 1, and every division rounds down:

- **A player hitting a creature** (MinPCDamagePercent): min(M, R + ((P + random(100 - P)) x M) / 100), a top-up of P%
  to 99% of the highest roll; P 100 gives M; P 0 leaves R. At Easy: longsword 1d8 came up 8 in 81% of 91 blows (rule
  81%), greatsword 2d6 12 in 92% of 88 (rule 90.5%, never below 8), a fist 1d3 3 in 89% of 122 (rule 88.7%).
- **A creature hitting a player** (MaxNPCDamagePercent): below 100, max(1, R - (M x (100 - P)) / 100), a flat cut that
  leaves at least 1 from the dice; above 100, R + (M x random(P - 100)) / 100, with no cap. At Easy a 1d8 gave 1 in 63%
  of 405 blows and 2, 3, 4 in about 12% each (rule 62.5 / 12.5%), a 1d4 1 in 74% of 388 (rule 75%); at Very Difficult a
  1d8 gave 1 to 15, mean 8.1 over 209 (rule 7.98), a 1d4 1 to 7, mean 4.2 over 205 (rule 4.0).
- **Critical hits**: each multiplier's roll of the dice is scaled on its own. A player's keen longsword criticals at
  Easy: 16 (both rolls topped up to 8) in 60% of 48 (rule 65.6%; scaling the doubled dice as one 2d8 would give 89%).
- **Unchanged**: Strength, enhancement, damage-bonus properties (a dagger's +2 fire), sneak attack and every other
  bonus; a player's own dice at Normal, D&D Hardcore and Very Difficult (uniform over 1-8 and 2d6 in diffscale);
  **henchmen and other associates** on either side (a henchman's 1d8 on a creature and creatures' 1d4 on the
  henchman were uniform at every difficulty); EffectDamage on a player from a hostile creature, a commoner, a
  placeable, the module or herself (20, 7, 3 and 1 points all landed in full, also through 5 fire resistance and 25%
  fire immunity); a hostile creature's Magic Missile; damage she deals by script; weapon hits, critical hits and
  scripted damage between creatures that aren't players (`difficulty_npc1/4` match the difficulty-3 combat suites).
- **Attacks of opportunity** on a player shooting: at D&D Hardcore and above only (diffsettings.2da NoAoOWithRanged,
  read by `CombatSystem.ProvokeShot`; nwmain `plotshot`). A plot guard never makes one. Drinking potions, casting and walking away: none at any
  difficulty in the scenes so far; NoAoOWithPotion is not read.
- **Scripted, not engine**: the rest of the descriptions is the stock scripts reading GetGameDifficulty, so OpenWinter
  gets it by running them: friendly fire of area spells (`spellsIsTarget` in x0_i0_spells: above Normal a caster's own
  area spells hit herself and her associates), shorter sleep/hold/stun durations on players (`GetScaledDuration` in
  nw_i0_spells: a quarter at Easy, half at Normal), charm and domination turned into daze and fear into an attack
  penalty on players (`GetScaledEffect`), maximised cure spells on players below Hardcore (`spellsCure`).

### diffscale

`python tools/visual/visual.py run diffscale --only nwn --difficulty N --tag _dN`, then
`python tools/visual/check_diffscale.py out/visual/shots/vis_diffscale_dN/nwn` prints each kind of blow's values next
to the rule. The player (Aluvian Darkstar with a fighter level for the weapons, Strength 10) hits a crit-immune dummy
with a longsword and a greatsword for 55 s each, then a dummy that isn't immune with a keen longsword and Improved
Critical; at the same time her henchman (a bandit, longsword) hits a dummy, two bandits with daggers hit the henchman
and four bandits (two daggers, two longswords) hit her. Every attacker has Strength 14 (+2). Dummies log each hit from
OnDamaged; hits on her come from the chat. At the end her bonuses are removed and she takes 9999 damage (MinHP1).
`--difficulty 0` reads back as 1: the original's settings don't reach Very Easy, so its column (always the highest
roll for players, R - 75% for creatures, MinHP1) was not seen.

## In OpenWinter

`Combat/Difficulty.cs` reads diffsettings.2da and holds the formulas; `CreatureCombatRules.DifficultyDice` rolls the
weapon's dice and scales them once per critical multiplier, and `CombatSystem.Resolve` applies NoCriticalOnPC.
"Player" means a creature with a player attached (`IsPC`); the attacker of the scaled hits is any creature that isn't
one. `DifficultyScalingTests` holds the formulas' exact distributions.

## Not matched or not measured

- Player against player (PvP) at any difficulty, and a possessed familiar (OpenWinter treats it as a player both when
  hit and when hitting).
- Difficulty 0 (Very Easy) and its MinHP1 switch: the original's settings don't reach it; OpenWinter reads its
  column like the others but doesn't apply MinHP1.
- NoAoOWithPotion is not read (see above).
- The original's own Options page for the difficulty was not looked at; the choice sits at the top of OpenWinter's
  Gameplay page.
