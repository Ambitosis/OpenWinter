using System.Globalization;
using OpenWinter.Game.Objects;
using OpenWinter.Net;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Appearance: how one player sees a creature (its appearance, gender, colours, phenotype, head, tail,
/// wings, portrait...). The creature itself is unchanged; the player is sent the overridden look whenever it is
/// sent the creature's look. Hit points, sound set and footsteps are kept and reported but not yet sent, as the
/// client has no per-player channel for them. There are no players on the measuring server, so this follows the
/// header's documentation (tests: NwnxObjectsTests).
/// </summary>
public static class AppearancePlugin
{
    const string P = "Appearance";
    const int Count = 12;
    const int Appearance = 0, Gender = 1, HitPoints = 2, HairColor = 3, SkinColor = 4, Phenotype = 5, Head = 6, SoundSet = 7, Tail = 8, Wings = 9,
        Footstep = 10, Portrait = 11;

    static string Key(uint player, int type) => "appearance:" + player.ToString(CultureInfo.InvariantCulture) + ":" + type.ToString(CultureInfo.InvariantCulture);

    [NwnxFunc(P, "SetOverride")]
    static void SetOverride(NwnxCall n)
    {
        var player = n.PopObject<Creature>();
        var creature = n.PopObject<Creature>();
        int type = n.PopInt(), value = n.PopInt();
        if (player is not { IsPC: true } || creature == null || type >= Count) return;
        var state = n.State(creature);
        if (type < 0)
        {
            for (int t = 0; t < Count; t++) state.Remove(Key(player.Id, t));
            return;
        }
        if (value < 0) state.Remove(Key(player.Id, type));
        else state[Key(player.Id, type)] = NwnxValue.Int(value);
    }

    [NwnxFunc(P, "GetOverride")]
    static void GetOverride(NwnxCall n)
    {
        var player = n.PopObject<Creature>();
        var creature = n.PopObject<Creature>();
        int type = n.PopInt();
        n.Push(player != null && creature != null ? Get(n.Registry, player.Id, creature.Id, type) : -1);
    }

    static int Get(NwnxRegistry r, uint player, uint creature, int type) =>
        r.HasState(creature) && r.ObjectState(creature).TryGetValue(Key(player, type), out var v) && v.Value is int i ? i : -1;

    /// <summary>
    /// The look <paramref name="viewer"/> is sent for <paramref name="subject"/>: a changed copy of
    /// <paramref name="look"/> (and the portrait) when the viewer has overrides for it, else null.
    /// </summary>
    public static Net.Appearance? ForViewer(World world, Creature? viewer, GameObject subject, Net.Appearance look, ref string portrait)
    {
        if (viewer == null || subject is not Creature || world.Systems.OfType<NwnxRegistry>().FirstOrDefault() is not { } r || !r.HasState(subject.Id)) return null;
        var values = new int[Count];
        bool any = false;
        for (int t = 0; t < Count; t++) { values[t] = Get(r, viewer.Id, subject.Id, t); any |= values[t] >= 0; }
        if (!any) return null;
        var a = new Net.Appearance
        {
            Type = look.Type, GenericDoorType = look.GenericDoorType, Gender = look.Gender, Phenotype = look.Phenotype,
            BodyParts = (ushort[])look.BodyParts.Clone(), Colors = (byte[])look.Colors.Clone(), Tail = look.Tail, Wings = look.Wings,
            Scale = look.Scale, Equipment = look.Equipment,
        };
        if (values[Appearance] >= 0) a.Type = values[Appearance];
        if (values[Gender] >= 0) a.Gender = (byte)values[Gender];
        if (values[Phenotype] >= 0) a.Phenotype = (byte)values[Phenotype];
        if (values[SkinColor] >= 0 && a.Colors.Length > 0) a.Colors[0] = (byte)values[SkinColor];
        if (values[HairColor] >= 0 && a.Colors.Length > 1) a.Colors[1] = (byte)values[HairColor];
        if (values[Head] >= 0 && a.BodyParts.Length > 0) a.BodyParts[0] = (ushort)values[Head];
        if (values[Tail] >= 0) a.Tail = values[Tail];
        if (values[Wings] >= 0) a.Wings = values[Wings];
        if (values[Portrait] >= 0 && world.Resources.TwoDA("portraits").Get(values[Portrait], "BaseResRef") is { Length: > 0 } baseRef) portrait = "po_" + baseRef;
        return a;
    }
}
