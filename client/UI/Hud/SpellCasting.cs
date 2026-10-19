using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>A castable spell as the spellbook and quickbar know it.</summary>
public sealed record SpellInfo(int Spell, string Name, string Icon, int TargetType, bool Hostile);

/// <summary>
/// Casting from the client: self-only spells go off at once; others enter targeting mode and the
/// next click picks a creature/object or a point on the ground.
/// </summary>
public sealed class SpellCasting
{
    public required ClientSession Session;
    public required Interaction Interaction;
    public readonly Dictionary<int, SpellInfo> Info = new();

    public void Cast(int spell, int cls = -1, int meta = 255)
    {
        Info.TryGetValue(spell, out var info);
        // TargetType bits: 1 self, 2 creature, 4 ground, 8 item, 16 door, 32 placeable, 64 trigger.
        if (info == null || info.TargetType is 0 or 1)
        {
            Session.Request("cast-spell", new { spell, @class = cls, meta });
            return;
        }
        Interaction.Targeting = (target, ground) =>
        {
            Interaction.Targeting = null;
            if (target != null) Session.Request("cast-spell", new { spell, @class = cls, meta, target = target.ObjectId });
            else if (ground is { } g && (info.TargetType & 4) != 0) Session.Request("cast-spell", new { spell, @class = cls, meta, pos = new[] { g.X, g.Y, g.Z } });
        };
        if ((info.TargetType & 8) != 0)
            Interaction.ItemTargeting = item => Session.Request("cast-spell", new { spell, @class = cls, meta, target = item });
    }
}
