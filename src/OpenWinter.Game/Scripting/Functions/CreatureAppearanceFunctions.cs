using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>Appearance, body parts, colours, wings and tails, portraits, sound set, footsteps and identity details.</summary>
static class CreatureAppearanceFunctions
{
    static Creature? Cr(Call c, int i) => c.Obj<Creature>(i);

    /// <summary>CREATURE_PART_* to the index in Creature.BodyParts (CreatureFactory.BodyPartFields order).</summary>
    static int PartIndex(int part) => part switch
    {
        0 => 17, // right foot
        1 => 18, // left foot
        2 => 15, // right shin
        3 => 16, // left shin
        4 => 14, // left thigh
        5 => 13, // right thigh
        6 => 3,  // pelvis
        7 => 2,  // torso
        8 => 4,  // belt
        9 => 1,  // neck
        10 => 9, // right forearm
        11 => 10, // left forearm
        12 => 7, // right bicep
        13 => 8, // left bicep
        14 => 5, // right shoulder
        15 => 6, // left shoulder
        16 => 11, // right hand
        17 => 12, // left hand
        20 => 0, // head
        _ => -1,
    };

    static string ModelType(Call c, Creature cr) => c.World.Resources.TwoDA("appearance").Get(cr.AppearanceType, "MODELTYPE").ToUpperInvariant();
    static bool PartBased(Call c, Creature cr) => ModelType(c, cr) == "P";

    static void Changed(Call c, GameObject o) => c.World.MarkChanged(o);

    [NwFunc("GetAppearanceType")] static object? Appearance(Call c) => c.Obj(0) switch { Creature cr => cr.AppearanceType, Placeable p => p.Appearance, _ => -1 };

    [NwFunc("SetCreatureAppearanceType")]
    static object? SetAppearance(Call c)
    {
        if (Cr(c, 0) is not { } cr || c.I(1) < 0) return null;
        CreatureRules.EnsureInit(c.World, cr);
        cr.AppearanceType = c.I(1);
        cr.Size = CreatureRules.AppearanceSize(c.World, cr.AppearanceType);
        Changed(c, cr);
        return null;
    }

    [NwFunc("GetPhenoType")] static object? Pheno(Call c) => Cr(c, 0) is { } cr ? (int)cr.Phenotype : 0;

    [NwFunc("SetPhenoType")]
    static object? SetPheno(Call c)
    {
        if (Cr(c, 1) is { } cr && PartBased(c, cr) && c.I(0) >= 0 && c.I(0) < 256)
        {
            cr.Phenotype = (byte)c.I(0);
            Changed(c, cr);
        }
        return null;
    }

    [NwFunc("GetCreatureBodyPart")]
    static object? BodyPart(Call c)
    {
        int idx = PartIndex(c.I(0));
        return Cr(c, 1) is { } cr && idx >= 0 && PartBased(c, cr) ? (int)cr.BodyParts[idx] : -1;
    }

    [NwFunc("SetCreatureBodyPart")]
    static object? SetBodyPart(Call c)
    {
        int idx = PartIndex(c.I(0));
        if (Cr(c, 2) is { } cr && idx >= 0 && PartBased(c, cr) && c.I(1) >= 0 && c.I(1) <= ushort.MaxValue)
        {
            cr.BodyParts[idx] = (ushort)c.I(1);
            Changed(c, cr);
        }
        return null;
    }

    [NwFunc("GetCreatureWingType")] static object? Wings(Call c) => Cr(c, 0)?.Wings ?? 0;
    [NwFunc("GetCreatureTailType")] static object? Tail(Call c) => Cr(c, 0)?.Tail ?? 0;

    [NwFunc("SetCreatureWingType")]
    static object? SetWings(Call c)
    {
        if (Cr(c, 1) is { } cr && ModelType(c, cr) is "P" or "W" && c.I(0) >= 0) { cr.Wings = c.I(0); Changed(c, cr); }
        return null;
    }

    [NwFunc("SetCreatureTailType")]
    static object? SetTail(Call c)
    {
        if (Cr(c, 1) is { } cr && ModelType(c, cr) is "P" or "T" && c.I(0) >= 0) { cr.Tail = c.I(0); Changed(c, cr); }
        return null;
    }

    [NwFunc("GetColor")]
    static object? GetColor(Call c)
    {
        int ch = c.I(1);
        return Cr(c, 0) is { } cr && ch >= 0 && ch < cr.Colors.Length ? (int)cr.Colors[ch] : -1;
    }

    [NwFunc("SetColor")]
    static object? SetColor(Call c)
    {
        int ch = c.I(1), value = c.I(2);
        if (Cr(c, 0) is { } cr && ch >= 0 && ch < cr.Colors.Length && value >= 0 && value <= 255)
        {
            cr.Colors[ch] = (byte)value;
            Changed(c, cr);
        }
        return null;
    }

    // ---------------------------------------------------------------- portraits

    const int PortraitInvalid = 65535;

    static string PortraitFromId(Call c, int id)
    {
        var baseRef = c.World.Resources.TwoDA("portraits").Get(id, "BaseResRef");
        return baseRef.Length == 0 ? "" : "po_" + baseRef;
    }

    [NwFunc("GetPortraitId")]
    static object? PortraitId(Call c) => c.Obj(0) switch
    {
        Creature { IsPC: true } => PortraitInvalid,
        Creature cr => cr.PortraitId > 0 && cr.PortraitId != PortraitInvalid ? cr.PortraitId : PortraitInvalid,
        Placeable p => (int)p.Template.GetInt("PortraitId", PortraitInvalid),
        Door d => (int)d.Template.GetInt("PortraitId", PortraitInvalid),
        _ => PortraitInvalid,
    };

    [NwFunc("SetPortraitId")]
    static object? SetPortraitId(Call c)
    {
        int id = c.I(1);
        switch (c.Obj(0))
        {
            case Creature cr:
                cr.PortraitId = id;
                cr.Portrait = PortraitFromId(c, id);
                Changed(c, cr);
                break;
            case Placeable p: p.Template.SetWord("PortraitId", id); p.Template.SetResRef("Portrait", PortraitFromId(c, id)); break;
            case Door d: d.Template.SetWord("PortraitId", id); d.Template.SetResRef("Portrait", PortraitFromId(c, id)); break;
        }
        return null;
    }

    [NwFunc("GetPortraitResRef")]
    static object? PortraitResRef(Call c) => c.Obj(0) switch
    {
        Creature cr => cr.Portrait.Length > 0 ? cr.Portrait : cr.PortraitId > 0 && cr.PortraitId != PortraitInvalid ? PortraitFromId(c, cr.PortraitId) : "",
        Placeable p => p.Template.GetString("Portrait") is { Length: > 0 } s ? s : PortraitFromId(c, (int)p.Template.GetInt("PortraitId", PortraitInvalid)),
        Door d => Portraits.ResRef(c.World, d),
        _ => "",
    };

    [NwFunc("SetPortraitResRef")]
    static object? SetPortraitResRef(Call c)
    {
        var resref = c.S(1);
        switch (c.Obj(0))
        {
            case Creature cr:
                cr.Portrait = resref;
                cr.PortraitId = PortraitInvalid;
                Changed(c, cr);
                break;
            case Placeable p: p.Template.SetResRef("Portrait", resref); p.Template.SetWord("PortraitId", PortraitInvalid); break;
            case Door d: d.Template.SetResRef("Portrait", resref); d.Template.SetWord("PortraitId", PortraitInvalid); break;
        }
        return null;
    }

    // ---------------------------------------------------------------- sound

    [NwFunc("GetSoundset")] static object? Soundset(Call c) => Cr(c, 0)?.SoundSet ?? -1;

    [NwFunc("SetSoundset")]
    static object? SetSoundset(Call c)
    {
        if (Cr(c, 0) is { } cr && c.I(1) >= 0) { cr.SoundSet = c.I(1); Changed(c, cr); }
        return null;
    }

    const int FootstepDefault = 65535;

    [NwFunc("GetFootstepType")]
    static object? Footstep(Call c)
    {
        if (Cr(c, 0) is not { } cr) return -1;
        CreatureRules.EnsureInit(c.World, cr);
        if (cr.FootstepOverride >= 0) return cr.FootstepOverride;
        return c.World.Resources.TwoDA("appearance").GetInt(cr.AppearanceType, "FOOTSTEPTYPE", -1);
    }

    [NwFunc("SetFootstepType")]
    static object? SetFootstep(Call c)
    {
        if (Cr(c, 1) is not { } cr) return null;
        CreatureRules.EnsureInit(c.World, cr);
        cr.FootstepOverride = c.I(0) == FootstepDefault ? -1 : c.I(0);
        Changed(c, cr);
        return null;
    }

    // ---------------------------------------------------------------- identity

    [NwFunc("GetGender")] static object? Gender(Call c) => Cr(c, 0) is { } cr ? (int)cr.Gender : 0;

    [NwFunc("SetGender")]
    static object? SetGender(Call c)
    {
        if (Cr(c, 0) is { } cr && c.I(1) >= 0 && c.I(1) <= 4) { cr.Gender = (byte)c.I(1); Changed(c, cr); }
        return null;
    }

    [NwFunc("GetAge")] static object? Age(Call c) => Cr(c, 0)?.Age ?? 0;
    [NwFunc("SetAge")] static object? SetAge(Call c) { if (Cr(c, 0) is { } cr) cr.Age = c.I(1); return null; }
    [NwFunc("GetDeity")] static object? Deity(Call c) => Cr(c, 0)?.Deity ?? "";
    [NwFunc("SetDeity")] static object? SetDeity(Call c) { if (Cr(c, 0) is { } cr) cr.Deity = c.S(1); return null; }
    [NwFunc("GetSubRace")] static object? SubRace(Call c) => Cr(c, 0)?.Subrace ?? "";
    [NwFunc("SetSubRace")] static object? SetSubRace(Call c) { if (Cr(c, 0) is { } cr) cr.Subrace = c.S(1); return null; }

    [NwFunc("SetObjectHiliteColor")]
    static object? Hilite(Call c)
    {
        if (c.Obj(0) is not { } o) return null;
        if (o is Creature cr) cr.HiliteColor = c.I(1);
        c.World.Output?.Notify("hilite-color", o, new { color = c.I(1) });
        return null;
    }
}
