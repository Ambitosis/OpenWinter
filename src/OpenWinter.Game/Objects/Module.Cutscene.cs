namespace OpenWinter.Game.Objects;

public sealed partial class Module
{
    /// <summary>The last actual player to request cutscene cancellation; persists after the callback/mode ends.</summary>
    public uint LastCutsceneCanceller = InvalidId;
}
