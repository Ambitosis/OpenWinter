# Crafting

## Engine or script

Every kind of crafting in NWN EE is decided by stock scripts; the engine only supplies the actions and functions they
call. OpenWinter runs the scripts unchanged.

| What | Stock scripts | Engine part |
|---|---|---|
| Scribe Scroll, Brew Potion, Craft Wand | Every spell script calls `X2PreSpellCastCode` (x2_inc_spellhook). For a player whose spell targets an item it runs `x2_pc_craft`, i.e. `CIGetSpellWasUsedForItemCreation` (x2_inc_craft): blank scroll / bottle / wand (base items 102 / 101 / 103), the feat (944 / 945 / 946), des_crft_spells restrictions, level limits, gold and XP, then the new item (`des_crft_scroll`, `x2_it_pcpotion`, `x2_it_pcwand`) | casting a spell at an item in the caster's backpack (spells.2da TargetType bit 8), GetSpellTargetObject / GetSpellId / GetLastSpellCastClass / GetSpellCastItem, the spell slot spent, item creation and properties, gold and XP |
| Craft Weapon / Craft Armor (mundane) | The craft tools (base items 109/110) cast spells 656/657/742/743 at a component: `x2_s2_crafting` reads des_crft_weapon / des_crft_armor and opens `x2_p_craftskills`, whose choices call `CIDoCraftItemFromConversation` / `CIUseCraftItemSkill` | GetIsSkillSuccessful, CreateItemOnObject, CopyObject, conversation tokens |
| Item appearance (the radial's Craft) | The radial Craft opens `x0_skill_ctrap` on the player. Its armour / weapon branches are the `x2_im_*` scripts (x2_inc_craft, x2_inc_itemprop): a mannequin copy of the player, the camera (StoreCameraFacing / SetCameraFacing / RestoreCameraFacing), part cycling with `IPGetModifiedArmor` / `IPGetModifiedWeapon` over parts_*.2da, cost (`CIGetArmorModificationCost`, armorparts CraftCost, item value) and DC (`CIGetArmorModificationDC`) | CopyItemAndModify, GetItemAppearance, CopyObject, camera functions |
| Trap kits | the same conversation's Craft Trap branch (`x0_skctrap_*`, x0_inc_skills) | GetIsSkillSuccessful, item creation and destruction |

## Measured

Run with `dotnet run --project src/OpenWinter.Tools -c Release -- parity-run craft_feats` and
`python tools/visual/visual.py run craftscroll`.


- **nwserver `craft_feats`** (160 readings match): an NPC wizard casts Magic Missile, Bull's Strength and Mage Armor at
  blank scrolls, bottles and wands in its backpack; its impact scripts are replaced by a stand-in that runs the stock
  `x2_pc_craft` (the spell hook itself skips non-players) and logs what it returns. Readings: the items made (resref,
  base item, properties, value), gold and XP after, the spell spent, the blank destroyed, with no feat, no gold, too
  little XP, a sword target, a scroll used on a blank, the 50-charge and disable switches; Craft Wand's d20 charges as a
  range. Also `IPGetModifiedArmor` next/previous for every third armour part of four armours, cost and DC, torso
  cycling, colour changes, weapon parts, the mundane recipe tables and `CIUseCraftItemSkill` at a sure success and
  failure. CopyItemAndModify keeps a negative armour part as a word (-1 reads 65535; the stock "previous" of a part
  at its minimum asks for -1) and copies a weapon unchanged for model index 3.
- **nwmain `craftscroll`** (visual scene, a real player): the test character given Scribe Scroll
  (characterFixture `addFeats`) casts Magic Missile at a blank scroll and Mage Armor at a bottle through the stock hook:
  the scroll is made, 225 GP and 9 XP are spent, and Brew Potion is refused, with the same items, gold, XP and chat
  lines ("* Success - Item created! *", "* Failure - You do not have the necessary feat to enchant this item *") on
  both engines.
- **Client**: a spell or ability that can target items takes a clicked item in the classic inventory while it
  waits for its target (that is how a player casts at a blank item). The modern inventory doesn't yet.

## Not measured

The conversation-driven parts on the original: the appearance-modification camera, the mannequin and the menu's
costs as the player sees them, and trap kits from the conversation (`x0_inc_skills` doesn't compile with the
official compiler, so the suite can't include it; its functions use only engine functions measured elsewhere).
