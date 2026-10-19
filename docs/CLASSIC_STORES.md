# Classic stores

How stores behave and look with the classic interface style, measured on NWN EE at 1280x720. The store window reads
the installed stores and GUI assets at run time.

## Input and feedback

- A single left click on a ware opens `buysell_confirm`, at any price (including below 1,000 GP). The original's
  Shift, Ctrl, Shift+Ctrl and Alt clicks still ask. OpenWinter adds Shift-click quick purchase as a deliberate
  [quality-of-life upgrade](QUALITY_OF_LIFE.md).
- Dragging a ware into inventory buys immediately, with or without Shift. Picking up an inventory item and dropping
  it onto the shop sells immediately. A single inventory click still picks up the item. Cancel performs no trade.
- Hover prices and confirmation use TLK 61619/61620 ("Buy/Sell item for N GP"). The price line stays white for wares
  the player can't afford: the original has no red warning. Tooltips show the name.
- Successful trades report the acquired/lost item, gold and "Transaction Successful" through the installed TLK text.
- Hovered unusable inventory/store items have red cells; usable cells are white. Unusable items are clear while
  idle. The same grid code covers backpack, bag, container and store grids.
- Right-clicks on wares and on backpack items open radial menus (see [CLASSIC_UI.md](CLASSIC_UI.md)).

### Sounds

Clicking a ware plays `it_pickup`, including a Shift-click quick buy. A confirmed purchase plays the item's placement
sound (baseitems.InvSoundType → inventorysnds.InventorySound) only after the server has transferred the item; both
interface styles play it from that acknowledgement, and a drag purchase plays it once. Failures and cancellation play
no placement sound.

## Closing

- A store closes once the player is more than two metres from where it was opened. Turning and small moves keep it
  open. The distance is three-dimensional: on a 32 degree ramp the original closes at 1.70 m of ground distance,
  which is 2.02 m counting the 1.1 m climb, and OpenWinter closes at the same point.
- Both games close at the first position past 2 m, so the exact reading depends on each server's movement step
  (2.026 m on the original, 2.066 m on OpenWinter); the store rule itself is the same.
- A store opened remotely anchors at the player's position, not the merchant's.
- Calling OpenStore again on the *same* store keeps the original boundary. Opening a *different* store closes the
  first at once (its OnStoreClosed runs) and the new one closes 2 m from where it was opened.
- OnStoreClosed fires once.

## Panels

- Panels fill left, right, then middle. A fourth evicts the oldest opened panel. Closing an edge promotes the
  middle; closing the left with only a right panel promotes it to the left. Opening inventory closes Memorize.
- The map occupies a normal slot at its own offset. Opening another panel closes an existing map; opening the map
  afterwards places it beside the shop.
- OpenStore closes inventory, then the current left panel, then opens the shop and inventory. Closing the shop leaves
  inventory open; closing inventory leaves the shop.
- With a live conversation (a store opened by a dialogue answer), OpenStore moves the conversation to the middle; the
  store takes the left and inventory the right. The conversation is never the panel a fourth one evicts: the
  character sheet evicts the store (the conversation moves back to the left), the journal the inventory. Opening the
  map, like the inventory, closes Memorize and leaves the spell book.

## Prices and terms

- The player pays `floor(value * (MarkUp + bonus) / 100)` and gets `floor(value * (MarkDown + bonus) / 100)`, with
  `BM_MarkDown` for stolen items at a black market.
- Unidentified items sell at their unidentified value; stacks and charged items at their whole value.
- MaxBuyPrice caps what the store pays; a store with less gold than the price refuses the sale. Stolen items are
  refused outside a black market.
- The player's Appraise skill changes nothing by itself (the stock `nw_i0_plot` appraise is a conversation script
  that passes OpenStore a bonus).
- Reopening the same store keeps the first opening's bonus mark-up and mark-down (`OpenStore(s, pc, 20, -10)` then
  `OpenStore(s, pc)` still trades at 170% and 40%); opening another store first resets it.
- Values come from GetGoldPieceValue, which is computed lazily: creating, copying, adding or removing a property and
  identifying only mark the value stale, and the next read fixes it with the charges of that moment (SetItemCharges
  after a read keeps the old value). A property removed in the running script still counts until the script ends.
- A store's window refreshes when a script creates or destroys its wares, or gives the player an item.

## Stock and art

- Store grids are 10x10 with category-specific page high-water marks and row zero at the bottom.
- Loaded stock is placed sorted ascending by baseitems.StorePanelSort, then unit whole-GP value, then reverse
  authored order for ties. Script inventory iteration order stays independent. New wares are placed first-fit
  without repacking.
- CreateItemOnObject works in stores. Only incoming stock matching an existing infinite ware is absorbed (selling an
  identical item into infinite stock destroys the incoming item and keeps one ware); finite duplicate wares and
  potion stacks remain separate.
- Tabs clamp authored UVs instead of repeating their atlas. Layered helmets render installed PLTs with item colours.
  Confirmation dialogs use the installed brass scrollbar.

## Testing

- Parity suites: `items_store` (store rules) and `items_value_charges` (item values with charges, stacks, cast-spell
  uses, limitations and identification): `dotnet run --project src/OpenWinter.Tools -c Release -- parity-run <suite>`.
- Visual scenes: `storeinput_reference`, `storeprices`, `storeswitch`, `storeslope`, `storepanels`,
  `storemapafter`, `panelorder`, `storejournalpair`, `storeconvopanels`, `storestock`, `storesell` and `storesound`:
  `python tools/visual/visual.py run <scene>`. `python tools/visual/check_store_audio.py` compares the pickup,
  placement and cancel cues of a `storesound` run.

## Not matched yet

- The screen is not pixel-for-pixel: armour/helmet palette shading, font metrics and world lighting differ, and the
  confirmation scrollbar's lower cap sits a few pixels lower.
- Identification, refusal and insufficient-funds dialog presentation were not measured.
- Not measured: IdentifyPrice from the window, Appraise with a store owner in a scripted conversation, the bonus
  clamp, an examine gesture on the eye button, rapid double-click selling.
- Exact audio mixing and output gain of the purchase cues.
