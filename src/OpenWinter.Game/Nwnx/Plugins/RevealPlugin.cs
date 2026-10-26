using OpenWinter.Game.Objects;
using OpenWinter.Game.Skills;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Reveal: stealthy creatures revealed to chosen observers or their party (<see cref="SkillSystem.RevealTo"/>).
/// Measured only for NPC observers, which nwserver leaves unaffected (parity suite nwnx_reveal). What a reveal does
/// for a player observer follows the header's description and is unmeasured: the NWNX server runs without players.
/// </summary>
static class RevealPlugin
{
    const string P = "Reveal";

    [NwnxFunc(P, "RevealTo")]
    static void RevealTo(NwnxCall n)
    {
        var hider = n.PopObject<Creature>(); var observer = n.PopObject<Creature>(); int method = n.PopInt();
        if (hider != null && observer != null) n.World.System<SkillSystem>().RevealTo(hider, observer, method == SkillSystem.RevealSeen ? SkillSystem.RevealSeen : SkillSystem.RevealHeard);
    }

    [NwnxFunc(P, "SetRevealToParty")]
    static void SetRevealToParty(NwnxCall n)
    {
        var hider = n.PopObject<Creature>(); bool reveal = n.PopInt() != 0; int method = n.PopInt();
        if (hider != null) n.World.System<SkillSystem>().SetRevealToParty(hider, reveal, method == SkillSystem.RevealSeen ? SkillSystem.RevealSeen : SkillSystem.RevealHeard);
    }
}
