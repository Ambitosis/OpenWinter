# Notify kinds: items package

Sent through `IWorldOutput.Notify` by `OpenWinter.Game.Inventory.InventorySystem` and the item/store
script functions.

| kind | subject | data fields | meaning |
| --- | --- | --- | --- |
| `creature-equipment` | the creature | `item` (id), `slot` (INVENTORY_SLOT_*, or -1 when the item came off), `visual` (null, or `{slot, baseItem, modelParts[3], colors[6], armorParts[20]}` with `slot` as the slot bit, same layout as `ItemVisual`) | A creature's equipment changed: equipped, unequipped, or SetHiddenWhenEquipped toggled (hidden items send `visual: null`). Sent to the creature's area. |
| `gold-changed` | the player's creature | `gold` (new total), `delta` | A PC gained or lost gold. Sent to that player only. |
| `item-acquired` | the acquiring player's creature | `item` (surviving item/stack id), `sound` (inventory placement resref) | Successful ground pickup or container loot; plays the placement cue on the Interface bus in both UI modes. Player only. The localized acquisition line is delivered separately through normal chat. |
| `store-open` | the store | `store`, `name`, `markUp`, `markDown` (percent, including OpenStore bonuses), `gold` (-1 = unlimited), `maxBuyPrice` (-1 = none), `identifyCost` (-1 = won't identify), `blackMarket`, `items[]` of `{id, name, resref, baseItem, stack, panel, infinite, identified, price}` | OpenStore: show the store window. `panel` is the UTM category (0 armour, 1 misc, 2 potions, 3 rings, 4 weapons); `price` is what this player pays. Re-sent with fresh contents after every buy or sell. Player only. |
| `store-close` | the store | `store` | The store window was closed (`InventorySystem.CloseStore`). Player only. |
| `inventory-open` | the creature whose inventory is shown | `creature`, `gold`, `equipped[]` of `{slot, id, name, baseItem}`, `items[]` of `{id, name, baseItem, stack, container}` | OpenInventory: show a creature's inventory panel. `container` is the bag (or the creature) holding the item. Player only. |
| `camera-store` | the player's creature | (none) | StoreCameraFacing: remember the current camera mode and position for a later RestoreCameraFacing. Player only. |

Client requests the store window needs (buy, sell, identify, close) map to `InventorySystem.Buy`,
`Sell`, `Identify` and `CloseStore`; prices come from `SellPrice` / `BuyPrice`.
