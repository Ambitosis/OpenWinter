# Player barter

Two players trade items and gold through the barter window. The original can't run two clients on one licence (see
[MULTIPLAYER_HARNESS.md](MULTIPLAYER_HARNESS.md)), so the rules below are **inferred** from the game's own window layout and strings, not
measured. They are checked between two OpenWinter clients with `python tools/multiplayer/mp.py run mp_barter --silent`
(add `--client-args "--ui classic"` for the classic windows).

## Engine or script

Barter is all engine: no stock script takes part, and NWScript has no barter function. The items that change hands
fire the ordinary OnAcquireItem / OnUnAcquireItem events (the inventory system's moves), so module scripts see a trade
as items lost and acquired.

## Flow (server: `src/OpenWinter.Game/Requests/BarterRequests.cs`)

1. **Asking.** Another player's radial menu has Barter (dialog.tlk 6835, icon `ir_barter`; its wedge, SW, is inferred).
   It sends `barter-request`. The asked player gets `barter-prompt` and a yes/no popup reading "Barter with
   <FullName>" (6836). Refusing tells the asker "<FirstName> refused to barter with you." (6797).
2. **Refusals.** A creature that isn't a player: "<FirstName> cannot be bartered with." (6794). The asker already
   bartering, asking, being asked, talking or at a store: "You are too busy to barter now." (6796). The other player
   busy the same way: "<FirstName> is too busy to deal with you." (6795). Possessing a familiar: 63821.
3. **Offers.** Both windows open, each with the inventory beside it (like a store). A player puts backpack items into
   her own offer (dragged onto her box, or clicked in the modern inventory) and takes them back (dragged out or clicked),
   and sets an amount of gold (the gold button). Equipped, cursed and undroppable items can't be offered; items inside
   bags stay there (the bag itself can be offered). An offered item that leaves the backpack leaves the offer.
4. **Offer** (`barter-lock`) locks the player's own offer: it can't change afterwards. The partner's Offer lamp lights.
5. **Accept** (`barter-accept`) works only when both offers are locked. Any change to an offer takes back both
   acceptances (only possible before locking). When both have accepted, the items and gold change hands at once. Items
   that don't fit the receiver's backpack are put at her feet. The game's item and gold lines follow ("Lost Item:",
   "Acquired Item:", "Lost 25GP", "Acquired 25GP").
6. **Cancel** (`barter-cancel`, the Cancel or X button, closing the window), either player dying, leaving the area,
   logging out, one of them moving to more than 10 m (3-D) from the other, a jump or teleport, taking damage
   (temporary hit points too) or starting a conversation ends it for both with "Barter cancelled" (66221). The
   10 m and the interruptions are not measured (one licence).

## Windows

- **Classic** (`client/UI/Classic/Inventory/ClassicBarterWindow.cs`) draws `ingame_barter.gui`: on the left the
  partner's portrait (`BarratorPortrait`), offer (`BarterItems`, `inv_box_3x10`) and Offer / Accept lamps
  (`RightDeclineBut`, `RightAcceptBut`, greyed until she has done it); on the right the player's portrait, offer
  (`PlayerItems`) and Cancel / Offer / Accept (`LeftCancelBut`, `LeftDeclineBut`, `LeftAcceptBut`); the gold and
  examine buttons. The title reads "Barter with <FullName>" (6836; the .gui caption is "Barter"). The gold under each
  offer and the lamps' look are inferred.
- **Modern** (`client/UI/BarterPanel.cs`): the same two offers in uniform slots, a gold box, Cancel / Offer / Accept.

## Not measured

Whether the original makes the asker walk up to the other player first (and from how far the window opens), the time
a question stays open, the distance that ends a barter (OpenWinter uses 10 m), whether plot items can be bartered, where the original puts
the request popup and the barter window and the lamps' look, and whether the original prints anything when a trade
completes besides the item and gold lines.

These need two original clients on one server. `tools/multiplayer/mp.py run mp_barter --only nwn` starts nwserver
and two sandboxed nwmain clients, but with a single licence the server refuses the second client ("Your CD Key is
already in use on that server"); a second key goes in `config/local.json` as `"nwnClientKeys"` (see
[MULTIPLAYER_HARNESS.md](MULTIPLAYER_HARNESS.md)). A single client can't stand in: Barter is only on another
player's radial menu. With a second key the steps can be driven by hand through `MP_NWN_INTERACTIVE=1` (`MPA rclick`,
`MPA click`, `MPA shot`).
