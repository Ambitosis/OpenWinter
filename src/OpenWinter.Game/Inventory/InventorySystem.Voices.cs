using OpenWinter.Game.Objects;
using OpenWinter.Game.WorldObjects;

namespace OpenWinter.Game.Inventory;

public sealed partial class InventorySystem
{
    // Actual-player native controls identify slot32 on entering either nonzero
    // load state, including overloaded -> encumbered, but not on returning to
    // normal or remaining in the same state. Compare each completed mutation,
    // not coalesced inventory snapshots: intermediate transitions are audible.
    int VoiceLoadBefore(GameObject? holder) => holder is Creature { IsPC: true, Destroying: false } pc
        ? Rules.Encumbrance(pc) : -1;

    void VoiceLoadChanged(GameObject? holder, int before)
    {
        if (before < 0 || holder is not Creature { IsPC: true, Destroying: false, IsDestroyed: false } pc) return;
        int after = Rules.Encumbrance(pc);
        if (after != 0 && after != before)
            VoiceService.Play(_world, pc, 32, caption: false);
        // The load line and the movement stage follow each change too (EncumbranceSystem).
        _world.System<EncumbranceSystem>().Update(pc);
    }
}
