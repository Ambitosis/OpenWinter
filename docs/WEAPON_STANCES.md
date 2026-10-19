# Weapon stances, swings and swinging weapon parts

Measured on the original client with the visual scenes `weaponstance1..3`, `weaponready1..6` and `weaponclose`
(`python tools/visual/visual.py run <scene>`). Implemented in `src/OpenWinter.Game/Combat/WeaponStances.cs` (shared by the server's swing
choice in `CombatSystem.Resolve.cs` and the client), `client/Views/CreatureView.Weapons.cs` (ready stance and arm
clips) and `client/Views/PartBasedBuilder.cs` (animated weapon parts). Tests: `WeaponStanceTests`.

## How it was measured

The original exposes no "current animation" to scripts, so the scenes use the public `ReplaceObjectAnimation`:
each phase replaces one group of candidate clips on every actor with an unmistakable marker clip (`sitcross`,
seated; `castuplp`, arms raised) and shoots the actors; whoever changes played a replaced clip. Stock bandits
(and halflings, size 2) hold one weapon set each, attack a skeleton dummy, then defend against it.
`weaponready*` puts four actors large in the frame and tests one group per phase; `weaponstance*` covers twelve
actors and also walking and running (arm clips).

Markers to avoid: a knockdown marker (`kdbckps`) or `getlowlp` leaves the actor down after the replacement is
cleared, and clearing the replacement of an arm clip that is playing (`plpause1`, `xbowr`) leaves the marker on
until the clip restarts, which spoils later phases for those actors.

## Ready stance and swings

| What the right hand holds | Prefix | Measured with |
|---|---|---|
| nothing (or a torch, shield only) | `nw` | unarmed |
| a weapon in each hand | `2w` (`2wreadyr`, `2wslashl` ...) | shortsword + dagger |
| WeaponWield 4 (polearms, staves, short spear) or 8 (double weapons) | `pl` | halberd, scythe, short spear, quarterstaff, magic staff, two-bladed sword, dire mace |
| WeaponSize above the creature's size | `2h` | greatsword, greataxe, heavy flail, trident (human); club, morning star (halfling) |
| any other melee weapon | `1h` | dagger, shortsword, longsword, bastard sword, light mace, morning star, light flail, club, longsword with shield |
| bow / crossbow | `bowrdy`+`bowshot` / `xbowrdy`+`xbowshot` | longbow, shortbow, light and heavy crossbow |
| sling, thrown weapon | `1hreadyr`, attack `throwr` | sling, throwing axes (the throw for the sling is from its model, which animates `throwr`) |

The swing is the prefix's `slashl`, `slashr` or `stab`, weighted by baseitems `%AnimSlashL/R/S`; the off-hand
blow is `<prefix>slasho`. Note that double weapons use `pl`, not `2w`. Each humanoid's combat stance follows this
table; `nwreadyr` (fists up) is only for an empty right hand.

## Arm clips over idling, walking and running

| Held | Idle | Walk / run |
|---|---|---|
| one-handed weapon (thrown too) | - | `walk_swordr` / `run_swordr` |
| weapon in the left hand | - | `walk_swordl` / `run_swordl` |
| shield | - | `walk_shieldl` / `run_shieldl` |
| bow | - | `walk_bowl` / `run_bowl` |
| crossbow | `xbowr` | `xbowr` (walking not measured, see below) |
| two-handed or polearm weapon | `plpause1` | `plpause1` |
| two-handed flail (heavy flail; a morning star on a halfling) | - | - |

The base clips stay `pause1`, `walk` and `run` for everyone. The client composes each arm clip over its base clip
when the model is built: the arm's bones take the arm clip's keys.

## Swinging parts

No weapon uses a danglymesh for its moving part (`item-anims` in the tools lists them: only a club's fins and a
horn's banner are danglymesh). The flails' and morning star's chain (supermodel `wblfh_t_001`), the whip, the sling,
the bows' limbs (`bowshot`) and the crossbows' arms (`xbowshot`) are baked animation: the part model has clips named
like the creature's (`1hslashl`, `1hreadyr`, `default` ...). The client keeps such parts' node hierarchy under
the hand, renames their nodes per hand, and adds their tracks to the creature's clip of the same name, or their
`default` pose when they have none, so the chain hangs and swings with the arm.

## Creature models

Measured on the original client with the visual scenes `creatureswing`, `creatureswing2..7` and `creaturereach` (the same ReplaceObjectAnimation method, marker `ckdbckdie`, sampled by 6 fps bursts). Non-humanoid
models (appearance.2da MODELTYPE S or L) have the creature animation set instead of the humanoid one; the server
picks from it (`WeaponStances.CreatureSwing`, `CombatSystem.Animation`):

| Where the target is | Clip |
|---|---|
| within the attacker's PREFATCKDIST, centre to centre | `ccloseh` |
| farther (where a creature closing in stops: PREFATCKDIST plus the target's HITDIST) | `ca1slashl`, `ca1slashr` or `ca1stab`, by the creature weapon's %AnimSlash weights (a third each) |

- Rooted dire badgers (PREFATCKDIST 2.6) played ccloseh at 1.0, 1.6, 2.1, 2.2, 2.3, 2.45 and 2.6 m and the ca1
  clips at 2.7, 2.8 and 2.9 m; wolves (2.3) ccloseh at 1.4 to 2.1 m and ca1 at 2.5 m.
- Which creature weapon attacks doesn't matter: the left claw alone, the right claw alone, the bite alone and all
  three each played all three ca1 clips. A dire badger without creature weapons used ccloseh at its stand-off too.
- `ca2slashl/r/stab`, `creach` and `cclosel` were never played, nor the humanoid names (`nwslashl` ...).
- The combat stance is `creadyr` (and `creadyl`); dodging plays `cdodgelr` / `cdodges`.
- The client plays the clip the server names, and maps a swing the model lacks to the other set's
  (`WeaponStances.SwingAlias`: `nwslashl` as `ca1slashl`, `ccloseh` as `nwcloseh`). The weapon's sounds play at the
  played clip's "hit" event (0.5 s on most models, 0.467 s on the giant spider's; the dire badger's swings have no
  events and keep 0.5 s).

## Not matched yet

- Crossbow walking and running: the actors were stuck with a marker from the idle phase; `xbowr` is assumed.
- Slings: no walking arm clip was seen; none is applied.
- Shield blocks (`shieldl`) and parries (`*parryl/r`) were never seen in ordinary defence (no Parry mode); the
  client keeps `damages` / `dodges` / `dodgelr`. Parry mode was not tested.
- `closeh` / `closel` / `reach` swings were not seen at the dummies' 1.3 m; when the original uses them is unknown.
- The ready stance alternates between the `readyr` and `readyl` clips in the original (a left slash ends in the
  `l` pose); the client holds `readyr`.
- `pausesh` was not seen in use.
- Large and huge humanoids were not measured.
- Creature models: whether the close range depends on the target (only skeleton dummies were measured); the
  damage and parry reactions while fighting back (none showed in the defence phases); what a creature model
  without `ccloseh` (the EE oozes, jellies and leeches) plays close up; the original's attack cadence with
  EffectModifyAttacks (about one swing every 1.8 s against OpenWinter's one a second) was seen in passing only.
- Humanoids at 1.3 m, inside their PREFATCKDIST of 1.7, never played `closeh`: the creature rule does not carry over.
