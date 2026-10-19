using System.Globalization;
using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Functions;
using OpenWinter.Game.WorldObjects;

namespace OpenWinter.Game.DungeonMaster;

/// <summary>
/// The DM creator: places a palette blueprint in the world. Creatures, placeables, items,
/// waypoints and stores are created like CreateObject (creatures run their OnSpawn script);
/// items can go straight into an inventory. Encounters become an active encounter region around
/// the point that spawns its first wave at once; triggers and sounds get a small default shape.
/// Raises NWNX_ON_DM_SPAWN_OBJECT_BEFORE/_AFTER (AREA, OBJECT, OBJECT_TYPE, POS_X/Y/Z, RESREF).
/// </summary>
public static class DMCreator
{
    /// <summary>Half the side of the square DM-placed triggers and encounters cover, in metres.</summary>
    public const float RegionHalfSize = 5f;

    public static GameObject? Spawn(World w, PlayerSession s, string kindKey, string resref, Area area, Vector3 pos, float facing, GameObject? into = null)
    {
        var dms = w.System<DungeonMasterSystem>();
        if (!s.IsDM || dms.Avatar(s) is not { } dm || Palettes.Kind(kindKey) is not { } kind || resref.Length == 0) return null;
        if (w.Resources.Read(resref, kind.ResType) == null) return null;
        string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        var data = new Dictionary<string, string>
        {
            ["AREA"] = DungeonMasterSystem.Hex(area.Id), ["OBJECT_TYPE"] = kind.ObjectType.ToString(CultureInfo.InvariantCulture),
            ["POS_X"] = F(pos.X), ["POS_Y"] = F(pos.Y), ["POS_Z"] = F(pos.Z), ["RESREF"] = resref,
        };
        if (dms.Before(dm, "SPAWN_OBJECT", data)) return null;
        GameObject? o = kind.Key switch
        {
            "item" when into != null => DMActions.GiveItem(w, s, into, resref),
            "encounter" => Encounter(w, resref, area, pos, dm),
            "trigger" => Trigger(w, resref, area, pos),
            "sound" => Sound(w, resref, area, pos),
            _ => ObjectFunctions.Spawn(w, kind.ObjectType, resref, area, pos, facing),
        };
        if (o != null) data["OBJECT"] = DungeonMasterSystem.Hex(o.Id);
        dms.After(dm, "SPAWN_OBJECT", data);
        return o;
    }

    static List<Vector3> Square(Vector3 c) => new()
    {
        c + new Vector3(-RegionHalfSize, -RegionHalfSize, 0), c + new Vector3(RegionHalfSize, -RegionHalfSize, 0),
        c + new Vector3(RegionHalfSize, RegionHalfSize, 0), c + new Vector3(-RegionHalfSize, RegionHalfSize, 0),
    };

    static void Common(GameObject o, GffStruct s, string resref, string nameField)
    {
        o.Tag = s.GetString("Tag");
        o.ResRef = resref;
        o.Name = s.GetLocString(nameField);
        o.Description = s.GetLocString("Description").FirstString;
        o.Locals.LoadFrom(s);
        WorldObjectFields.Load(o, s);
    }

    /// <summary>An encounter region at the point; its creatures spawn now, scaled to the party in the area.</summary>
    static Encounter? Encounter(World w, string resref, Area area, Vector3 pos, Creature dm)
    {
        var ute = w.Resources.ReadGff(resref, ResType.Ute);
        if (ute == null) return null;
        var e = new Encounter { Template = ute, Active = true, Geometry = Square(pos) };
        Common(e, ute, resref, "LocalizedName");
        foreach (var f in new[] { "OnEntered", "OnExhausted", "OnExit", "OnHeartbeat", "OnUserDefined" }) e.Scripts[f] = ute.GetString(f);
        e.State.Load(ute);
        e.State.SpawnPoints.Clear();
        e.State.SpawnPoints.Add((pos, 0f));
        w.AddToArea(e, area, pos, 0);
        // The first wave is sized for the strongest player in the area (or the DM's level when alone).
        var party = area.Objects.OfType<Creature>().Where(c => c.IsPC && c.DMOwner == null && !c.IsDMPossessed).OrderByDescending(c => c.TotalLevel).FirstOrDefault();
        w.System<EncounterSystem>().TrySpawn(e, party ?? dm);
        return e;
    }

    static Trigger? Trigger(World w, string resref, Area area, Vector3 pos)
    {
        var utt = w.Resources.ReadGff(resref, ResType.Utt);
        if (utt == null) return null;
        var t = new Trigger
        {
            Geometry = Square(pos),
            TriggerType = (int)utt.GetInt("Type"),
            LinkedTo = utt.GetString("LinkedTo"),
            LinkedToFlags = (byte)utt.GetInt("LinkedToFlags"),
            Cursor = (int)utt.GetInt("Cursor"),
        };
        Common(t, utt, resref, "LocalizedName");
        foreach (var f in new[] { "OnClick", "OnDisarm", "OnTrapTriggered", "ScriptHeartbeat", "ScriptOnEnter", "ScriptOnExit", "ScriptUserDefine" })
            t.Scripts[f] = utt.GetString(f);
        w.AddToArea(t, area, pos, 0);
        return t;
    }

    static Sound? Sound(World w, string resref, Area area, Vector3 pos)
    {
        var uts = w.Resources.ReadGff(resref, ResType.Uts);
        if (uts == null) return null;
        var o = new Sound
        {
            Template = uts,
            Active = uts.GetInt("Active") != 0,
            Continuous = uts.GetInt("Continuous") != 0,
            Looping = uts.GetInt("Looping") != 0,
            Positional = uts.GetInt("Positional") != 0,
            MaxDistance = uts.GetFloat("MaxDistance"),
            MinDistance = uts.GetFloat("MinDistance"),
            Volume = (int)uts.GetInt("Volume", 127),
        };
        foreach (var snd in uts.GetList("Sounds")) o.Sounds.Add(snd.GetString("Sound"));
        Common(o, uts, resref, "LocName");
        w.AddToArea(o, area, pos, 0);
        return o;
    }
}
