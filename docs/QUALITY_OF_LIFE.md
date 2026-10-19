# OpenWinter quality-of-life upgrades

These are intentional conveniences beyond the original game's behaviour.

## Quick store purchases (classic interface)

**Shift + left-click a store item to buy it immediately**, without a confirmation
dialog. Normal left-click still asks first. The shortcut buys the displayed ware,
including its displayed stack, at the displayed price; normal gold, inventory-space
and store rules still apply. It never bypasses server validation.

Each Shift-click is one purchase, so a Shift-double-click buys twice. That is
intended: rapid Shift-clicks buy several of an infinite ware quickly, and the
double-click never falls through to the item's ordinary activate action.

Both paths play the pickup cue when the item is clicked and the item's own
inventory/drop cue once a successful purchase puts it in your inventory. Cancelling
or failing a purchase does not play the transfer cue. Drag purchases also play it
once, after the server accepts the transfer.

The original game still shows the confirmation on Shift-click (visual scenes `storetrade` and `storesound`).
Shift-click stack splitting in the inventory and the normal examine mode are unchanged.
