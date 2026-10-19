# Items on the ground

How the original draws an item lying on the ground: which model it shows, how that model is turned and lifted, and
which way it faces. Measured black-box on nwmain with the visual harness (scenes `grounditems` and `grounddrop`,
`--native-framebuffer`). OpenWinter builds the model in `client/Views/PartBasedBuilder.Ground.cs` from the item's
parts and colours, which the server sends in the item's spawn (`Appearance.Equipment[0]`, an
`ItemVisual` from `ItemFactory.Visual`).

Run both engines with:

```
python tools/visual/visual.py run grounditems grounddrop --native-framebuffer
```

`grounditems` makes one item of each kind with `CreateObject` in rows of five, 2 m apart, at facing 0, and shoots
each group from straight above and from low down: part-built weapons; one-piece and throwing weapons, the wand,
shields, the torch and arrows; worn items; everything else; the same kinds with other parts and colours
(`CopyItemAndModify`, then `CopyObject` to the ground); and longswords, crossbows, a potion, a shield and a bag at
facings 0, 45, 90, 180 and 270. `grounddrop` has the player put down a longsword facing north and another facing
east (`ActionPutDownItem`), beside armour, a cloak, a helmet and a bag made at facing 90. Each item's base item,
appearance values, facing and position are logged.

## Which model

The item's own model when the game has one, built from its parts as when it is held or worn, and tinted with the
item's colours. Otherwise baseitems.2da `DefaultModel`.

| Kind | Model on the ground |
| --- | --- |
| Part-built weapons (ModelType 2): longsword, battleaxe, bows, crossbows, greatsword, dagger, morning star, halberd, staff, wand, throwing axe | the item's own bottom, middle and top parts (`wswls_b_061` ...); a changed bottom part shows |
| One-piece weapons (ModelType 0): sling, shuriken, dart | own model (`wbwsl_001` ...) |
| Shields: small, large, tower | own model (`ashsw_011` ...); a changed part (41) shows |
| Torch | own model, unlit (no flame) |
| Helmet (ModelType 1) | own model (`helm_001`), tinted: a changed metal colour shows |
| Cloak | `gi_cloak01` whatever the cloak's part number (3 and 1 look the same), tinted: a changed cloth colour shows |
| Armour and clothing (ModelType 3) | a flat torso panel lying on its back (the shape of gifp's torso mesh), painted per armour; see below |
| Gold | own model `it_gold_001` (a pile and a pouch) |
| Miscellaneous items | own model when one exists (part 151: `it_midmisc_151`), else the bag |
| Potions | DefaultModel `it_potion_000`, standing up |
| Scrolls | DefaultModel `gi_scrl` |
| Arrows, bolts, bullets | DefaultModel `gi_wamar_001`, `gi_wambo_001`, `gi_wambu_001` (quiver, quiver, pouch) |
| Trap kits | DefaultModel `it_trap_001` |
| Boots, gloves, belts, amulets, rings, bracers, gems, healer's kits, thieves' tools, misc without a model | the bag, `it_bag` |

Boots are ModelType 2 but have no `it_boots_b_*` parts, so they fall back like the others. A CEP-style custom model
was not measured (no haks in the harness); by the same rule an item whose `ItemClass_NNN` model exists in a hak
shows that model.

## Orientation, height and facing

- **RotateOnGround** (baseitems.2da) turns the model before its facing: 1 is a quarter turn about the model's Y axis
  (+90 degrees), so swords lie with the guard flat and shields lie face up, boss up; 2 is a quarter turn about its X
  axis, so crossbows lie flat with the limbs level and the potion bottle stands upright. 0 leaves the model as it is.
  The turn applies to DefaultModels too (the potion); OpenWinter leaves the plain bag upright.
- **Height**: models rest on the ground: shields that sit entirely below the origin once turned are lifted onto
  the floor, the potion stands on it. OpenWinter lifts each model so its lowest point touches the ground.
- **Facing** follows the item's own facing with the creature convention: the model's +Y points along the facing. A
  longsword made at facing 0 points its blade east, at 90 north, at 180 west, at 270 south; 45 is diagonal. Armour,
  cloaks and helmets turn with their facing too (`grounddrop`, facing 90).
- **Dropped items face east.** An item put down with `ActionPutDownItem` gets facing 0 whichever way its dropper
  faces (dropped facing north: `GetFacing` 0.0 and the blade pointing east), at the dropper's position. OpenWinter does the same for
  scripted put-downs and for items dragged onto the ground.

## Armour

The original lays armour on its back with the neck towards its facing (at facing 0 the belt is west and the collar
east), as a flat panel with the outline of a torso: flared waist, shoulders, a notch at the neck. That outline is
gifp's `torso_g` mesh lying in its own space; gifp's node rotation would stand it on its side. Leather armour
(torso 16) and clothing (torso 19) are painted differently (brown leather with a buckle and rivets; a pale tunic
with two rows of buttons), and a different torso (30) shows a different panel.

**Not matched:** the painting. OpenWinter paints the panel with the armour's own chest PLT (`pmh0_chestNNN`)
stretched across it and tinted with the item's colours, which gives the right outline, orientation and a picture of
the right chest, but the original's pictures did not match any texture tried: the chest PLT through gifp's own UVs,
the chest PLT stretched (male and female), the chest mesh's own texture, the 3D chest part, or the inventory icon
PLTs (`ipm_chestNNN`). Changing the armour's Leather1 colour from 1 to 40 made no visible change in the original,
so its tint is also unknown.

## Picked-up items over windows

Picking an item off the ground with the inventory open attaches it to the pointer (`GroundItemCarry`), and counting
gold out of the amount dialog does the same (`GoldCarry`). Godot draws a drag preview in the canvas layer of the
control that started the drag, so both carries live on their own canvas layer (`PointerCarry`, layer 150, below only
the harness pointer at 200) and draw over every window: the classic and modern inventories and the dialogs. Items
dragged out of the inventory start from the inventory's own layer.

Scenes (OpenWinter only): `groundcarrytop` (classic: the carried longsword over the world, the inventory and the
chat window) and `groundcarrytopmodern` (modern: the item lies left of the window, the icon is shot over the
paperdoll and the backpack).

```
python tools/visual/visual.py run groundcarrytop --only openwinter --openwinter-args "--ui classic" --tag _classic
python tools/visual/visual.py run groundcarrytopmodern --only openwinter --openwinter-args "--ui modern"
```
