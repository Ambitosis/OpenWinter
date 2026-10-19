# Notify kinds: skills package

Sent by `OpenWinter.Game.Skills.SkillSystem` with `IWorldOutput.Notify(kind, subject, data, player)`.
"Player" means the notify goes only to that player's client; "area" means everyone in the subject's area.
Combat-log lines (the text players read) also go through `IWorldOutput.SendMessage`, in the game's own
format (dialog.tlk 10484): `Open Lock : *success* : (12 + 5 = 17 vs. DC: 15)`, with `Take 20` in place of
the roll when the check takes 20.

| kind | to | subject | data fields | meaning |
| --- | --- | --- | --- | --- |
| `skill-check` | player (the one checking, and a PC on the other side) | the creature making the check | `creature`, `skill` (SKILL_*), `roll` (d20, 20 when taking 20), `take20`, `bonus` (skill modifier incl. tools/kit/synergy), `total`, `dc` (the opposed roll's total for opposed checks), `success`, `target` (object or OBJECT_INVALID), `text` (the combat-log line) | A skill check was rolled: Open Lock, Disable Trap (examine/flag/disarm/recover), Set Trap, Pick Pocket, Taunt, Heal, Animal Empathy, Parry. For a feedback panel or floating roll display. Passive Search and the per-round Hide/Spot rolls are not reported. |
| `action-mode` | area | the creature | `creature`, `mode` (ACTION_MODE_*), `on`, `name`, `feedback` (localized TLK text, empty when the mode has no native indicator) | A mode changed, including script changes and cancellation of an exclusive combat mode. Show `*feedback*` in white over the visible creature, with successive messages stacking and rising briefly. Only modes 0/1 update the client's Detect/Stealth input state. Manual Detect and Stealth force walking; Keen Sense provides automatic active searching without a toggle or walking penalty. |
| `stealth-visibility` | player | the stealthy creature | `target`, `stealth` (false once the creature left stealth or the area), `seen`, `heard` | What this player currently perceives of a creature in stealth mode. `seen: false` → hide the creature (and its selection/name plate); `heard: true, seen: false` → it may be shown as a heard-only marker; `stealth: false` → show it normally again. Sent on change only. Party members, masters/associates and DMs always get `seen: true`. |
| `item-identified` | player | the item | `item`, `by` (the creature whose Lore identified it) | Lore identified an item (auto-identify when a player acquires it, or an explicit Lore use). The item's name/description should switch to the identified ones. |

Trap state changes reuse the world package's kinds: `trap-detected` (Search found a trap, Flag Trap made it
visible to everyone: `flagged: true`), `trap-disarmed`, `trap-triggered` (also on a botched disarm/recover
or Set Trap in combat).

## Client requests

| player action | call |
| --- | --- |
| Toggle stealth / detect mode (radial or hotkey) | `SkillSystem.SetActionMode(creature, ACTION_MODE_STEALTH or ACTION_MODE_DETECT, on)` (returns false and tells the player when stealth is refused in combat) |
| Radial skill on a target (Open Lock, Disable/Examine/Flag/Recover Trap, Pick Pocket, Taunt, Animal Empathy, Heal) | queue `new UseSkillAction { Skill, SubSkill, ItemId, Target, Range = UseSkillAction.RangeFor(skill) }` on the creature (same as ActionUseSkill) |
| Use a healer's kit / thieves' tools / trap kit from the inventory | `InventorySystem.UseItemProperty` hands these properties to `SkillSystem.UseSkillItem` |
| Examine an unidentified item | `SkillSystem.TryIdentify(pc, item)` |

## Event data keys set by this package

| event | keys |
| --- | --- |
| creature `ScriptDisturbed` after a noticed pick pocket | `Disturber` (the thief), `DisturbItem` (the stolen item or OBJECT_INVALID), `DisturbType` = INVENTORY_DISTURB_TYPE_STOLEN |
| door/placeable `OnUnlock` (picked lock) | `UnlockedBy` (via `DoorService`) |
| trap `OnDisarm` | `Disarmer` (via `TrapSystem.Disarm`) |
