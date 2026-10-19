namespace OpenWinter.Game.Objects;

/// <summary>Portrait texture base names ("po_hu_f_04_"; the client adds the size letter h/l/m/s/t).</summary>
public static class Portraits
{
    /// <summary>
    /// A creature's portrait: its Portrait resref when set (player characters, scripted
    /// SetPortraitResRef), otherwise "po_" + BaseResRef of its PortraitId row in portraits.2da.
    /// </summary>
    public static string ResRef(World world, Creature c)
    {
        if (c.Portrait.Length > 0) return c.Portrait;
        if (c.PortraitId <= 0) return "";
        var baseRef = world.Resources.TwoDA("portraits").Get(c.PortraitId, "BaseResRef");
        return baseRef.Length == 0 ? "" : "po_" + baseRef;
    }

    /// <summary>Object portraits include the authored door/placeable template or scripted replacement.</summary>
    public static string ResRef(World world, GameObject target)
    {
        if (target is Creature creature) return ResRef(world, creature);
        var template = target switch { Placeable p => p.Template, Door d => d.Template, _ => null };
        if (template == null) return "";
        var portrait = template.GetString("Portrait");
        if (portrait.Length > 0) return portrait;
        int id = (int)template.GetInt("PortraitId", 65535);
        // The installed normal-door blueprint leaves PortraitId at zero; the original
        // resolves that default to this portrait (radialobjdetail's public getter + menu).
        if (target is Door && id == 0) return "po_door01_";
        var baseRef = world.Resources.TwoDA("portraits").Get(id, "BaseResRef");
        return baseRef.Length == 0 ? "" : "po_" + baseRef;
    }

    /// <summary>The original's stand-ins for a portrait that can't be found: a shadowy man and a hooded woman.</summary>
    public const string UnknownMale = "po_hu_m_99_", UnknownFemale = "po_hu_f_99_";

    /// <summary>
    /// The portrait to show a player: <see cref="ResRef"/>, or the shadowy default for the creature's gender when
    /// that portrait isn't in the game data or haks (e.g. a character made on a server with its own portraits).
    /// Scripts still see the real value (GetPortraitResRef).
    /// </summary>
    public static string Display(World world, Creature c)
    {
        var r = ResRef(world, c);
        if (r.Length == 0 && !c.IsPC) return "";
        if (r.Length > 0 && Exists(world, r)) return r;
        return c.Gender == 1 ? UnknownFemale : UnknownMale;
    }

    static bool Exists(World world, string baseRef)
    {
        var res = world.Resources;
        foreach (var size in new[] { "m", "l", "h", "s" })
            if (res.Contains(baseRef + size, OpenWinter.Aurora.Resources.ResType.Tga) || res.Contains(baseRef + size, OpenWinter.Aurora.Resources.ResType.Dds)) return true;
        return false;
    }
}
