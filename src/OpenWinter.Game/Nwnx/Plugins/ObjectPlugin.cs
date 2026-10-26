using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>NWNX_Object. Argument order follows the nwnx_object.nss wrappers (popped in reverse push order).</summary>
static partial class ObjectPlugin
{
    const string P = "Object";

    /// <summary>
    /// Local variables as the original lists them (parity suite nwnx_object_locals): one entry per name, newest
    /// first, skipping names that hold only default values; a name holding several types reports the lowest.
    /// The original lists from a hash table, so beyond a few names its order also depends on the names themselves.
    /// </summary>
    static List<(string Name, int Type)> Locals(GameObject o) => o.Locals.NewestFirst();

    [NwnxFunc(P, "GetLocalVariableCount")] static void LocalCount(NwnxCall n) => n.Push(n.PopObject() is { } o ? Locals(o).Count : -1);

    [NwnxFunc(P, "GetLocalVariable")]
    static void LocalVariable(NwnxCall n)
    {
        var o = n.PopObject();
        int index = n.PopInt();
        var list = o != null ? Locals(o) : new List<(string, int)>();
        var (name, type) = index >= 0 && index < list.Count ? list[index] : ("", -1);
        n.Push(type);
        n.Push(name);
    }

    /// <summary>A vector argument (NWNXPushVector), or three floats from older callers.</summary>
    static Vector3 PopPosition(NwnxCall n, bool zyx)
    {
        if (n.Args.Peek() is { Value: Vector3 v }) { n.Args.Pop(); return v; }
        float a = n.PopFloat(), b = n.PopFloat(), c = n.PopFloat();
        return zyx ? new Vector3(c, b, a) : new Vector3(a, b, c);
    }

    [NwnxFunc(P, "SetPosition")]
    static void SetPosition(NwnxCall n)
    {
        var o = n.PopObject();
        var pos = PopPosition(n, zyx: true);
        n.PopInt(); // bUpdateSubareas
        if (o == null) return;
        o.Position = pos;
        n.World.MarkChanged(o);
    }

    [NwnxFunc(P, "AddToArea")]
    static void AddToArea(NwnxCall n)
    {
        var o = n.PopObject();
        var area = n.PopObject<Area>();
        var pos = PopPosition(n, zyx: false);
        if (o != null && area != null) n.World.AddToArea(o, area, pos, o.Facing, exact: true);
    }

    [NwnxFunc(P, "GetCurrentHitPoints")]
    static void GetHp(NwnxCall n) => n.Push(n.PopObject() switch
    {
        Creature c => c.CurrentHitPoints,
        Placeable p => p.CurrentHitPoints,
        Door d => d.CurrentHitPoints,
        _ => 0,
    });

    [NwnxFunc(P, "SetCurrentHitPoints")]
    static void SetHp(NwnxCall n)
    {
        var o = n.PopObject();
        int hp = n.PopInt();
        switch (o)
        {
            case Creature c: c.CurrentHitPoints = hp; break;
            case Placeable p: p.CurrentHitPoints = hp; break;
            case Door d: d.CurrentHitPoints = hp; break;
            default: return;
        }
        n.World.MarkChanged(o);
    }

    [NwnxFunc(P, "SetMaxHitPoints")]
    static void SetMaxHp(NwnxCall n)
    {
        var o = n.PopObject();
        int hp = n.PopInt();
        switch (o)
        {
            case Creature c: c.MaxHitPoints = hp; break;
            case Placeable p: p.HitPoints = hp; break;
            case Door d: d.HitPoints = hp; break;
        }
    }

    [NwnxFunc(P, "GetDialogResref")]
    static void GetDialog(NwnxCall n)
    {
        var o = n.PopObject();
        n.Push(o switch
        {
            Creature c => c.Conversation,
            Placeable p => p.Template.GetString("Conversation"),
            Door d => d.Template.GetString("Conversation"),
            _ => "",
        });
    }

    [NwnxFunc(P, "SetDialogResref")]
    static void SetDialog(NwnxCall n)
    {
        var o = n.PopObject();
        var dlg = n.PopString();
        switch (o)
        {
            case Creature c: c.Conversation = dlg; break;
            case Placeable p: p.Template.SetResRef("Conversation", dlg); break;
            case Door d: d.Template.SetResRef("Conversation", dlg); break;
        }
    }

    [NwnxFunc(P, "GetAppearance")] static void GetAppearance(NwnxCall n) => n.Push(n.PopObject() is Placeable p ? p.Appearance : 0);
    [NwnxFunc(P, "SetAppearance")]
    static void SetAppearance(NwnxCall n)
    {
        var p = n.PopObject<Placeable>();
        int app = n.PopInt();
        if (p == null) return;
        p.Appearance = app;
        n.World.MarkChanged(p);
    }

    [NwnxFunc(P, "GetPlaceableIsStatic")] static void GetStatic(NwnxCall n) => n.Push(n.PopObject<Placeable>()?.Static == true);
    [NwnxFunc(P, "SetPlaceableIsStatic")]
    static void SetStatic(NwnxCall n)
    {
        var p = n.PopObject<Placeable>();
        bool s = n.PopInt() != 0;
        if (p != null) { p.Static = s; n.World.MarkChanged(p); }
    }

    [NwnxFunc(P, "SetHasInventory")]
    static void SetHasInventory(NwnxCall n)
    {
        var p = n.PopObject<Placeable>();
        bool has = n.PopInt() != 0;
        if (p != null) p.HasInventory = has;
    }

    [NwnxFunc(P, "SetFacing")]
    static void SetFacing(NwnxCall n)
    {
        var o = n.PopObject();
        float dir = n.PopFloat();
        if (o == null) return;
        o.FacingDegrees = dir;
        n.World.MarkChanged(o);
    }

    [NwnxFunc(P, "GetIsDestroyable")] static void GetIsDestroyable(NwnxCall n) => n.Push(n.PopObject() switch { Creature c => c.IsDestroyable, null => false, _ => true });
    [NwnxFunc(P, "GetAILevel")] static void GetAILevel(NwnxCall n) => n.Push(n.PopObject<Creature>()?.AILevel ?? -1);
    [NwnxFunc(P, "SetAILevel")]
    static void SetAILevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        int level = n.PopInt();
        if (c != null) c.AILevel = level;
    }

    [NwnxFunc(P, "GetPositionIsInTrigger")]
    static void InTrigger(NwnxCall n)
    {
        var t = n.PopObject<Trigger>();
        var pos = PopPosition(n, zyx: false);
        n.Push(t != null && t.Contains(pos));
    }

    /// <summary>"{x, y, z}{x, y, z}..." as NWNX formats trigger polygons.</summary>
    [NwnxFunc(P, "GetTriggerGeometry")]
    static void GetGeometry(NwnxCall n)
    {
        var t = n.PopObject<Trigger>();
        var sb = new StringBuilder();
        if (t != null)
            foreach (var v in t.Geometry)
                sb.Append('{').Append(F(v.X)).Append(", ").Append(F(v.Y)).Append(", ").Append(F(v.Z)).Append('}');
        n.Push(sb.ToString());
        static string F(float f) => f.ToString("0.000", CultureInfo.InvariantCulture);
    }

    [NwnxFunc(P, "SetTriggerGeometry")]
    static void SetGeometry(NwnxCall n)
    {
        var t = n.PopObject<Trigger>();
        var text = n.PopString();
        if (t == null) return;
        var points = new List<Vector3>();
        foreach (Match m in Regex.Matches(text, @"\{([^}]*)\}"))
        {
            var parts = m.Groups[1].Value.Split(',').Select(s => float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f).ToArray();
            if (parts.Length >= 2) points.Add(new Vector3(parts[0], parts[1], parts.Length > 2 ? parts[2] : 0));
        }
        if (points.Count >= 3) t.Geometry = points;
    }

    [NwnxFunc(P, "GetMapNote")]
    static void GetMapNote(NwnxCall n)
    {
        var w = n.PopObject<Waypoint>();
        n.PopInt(); // id
        n.PopInt(); // gender
        n.Push(w?.MapNote.FirstString ?? "");
    }

    [NwnxFunc(P, "SetMapNote")]
    static void SetMapNote(NwnxCall n)
    {
        var w = n.PopObject<Waypoint>();
        var note = n.PopString();
        if (w == null) return;
        w.MapNote = new OpenWinter.Aurora.Formats.LocString(note);
        w.HasMapNote = true;
    }

    [NwnxFunc(P, "PeekUUID")] static void PeekUUID(NwnxCall n) { n.PopObject(); n.Push(""); }

    // The plugin's own object variables (separate from locals). With bPersist they travel in the object's GFF
    // (NWNX_POS: saved characters, serialized objects; parity suite nwnx_object_serialize).
    static string VarKey(string kind, string name) => "NWNX_Object!PER" + kind + "!" + name;
    const string IntKind = "INT", StringKind = "STR", FloatKind = "FLT";

    static NwnxValue? GetVar(NwnxCall n, GameObject? o, string kind, string name) => o == null ? null : NwnxPersistence.Get(n.Registry, o.Id, VarKey(kind, name));

    [NwnxFunc(P, "GetInt")]
    static void GetInt(NwnxCall n)
    {
        var o = n.PopObject(); var name = n.PopString();
        n.Push(GetVar(n, o, IntKind, name) is { Value: int i } ? i : 0);
    }
    [NwnxFunc(P, "SetInt")]
    static void SetInt(NwnxCall n)
    {
        var o = n.PopObject(); var name = n.PopString(); int value = n.PopInt(); bool persist = n.PopInt() != 0;
        if (o != null) NwnxPersistence.Set(n.Registry, o.Id, VarKey(IntKind, name), NwnxValue.Int(value), persist);
    }
    [NwnxFunc(P, "DeleteInt")]
    static void DeleteInt(NwnxCall n)
    {
        var o = n.PopObject(); var name = n.PopString();
        if (o != null) NwnxPersistence.Delete(n.Registry, o.Id, VarKey(IntKind, name));
    }
    [NwnxFunc(P, "GetString")]
    static void GetString(NwnxCall n)
    {
        var o = n.PopObject(); var name = n.PopString();
        n.Push(GetVar(n, o, StringKind, name) is { Value: string s } ? s : "");
    }
    [NwnxFunc(P, "SetString")]
    static void SetString(NwnxCall n)
    {
        var o = n.PopObject(); var name = n.PopString(); var value = n.PopString(); bool persist = n.PopInt() != 0;
        if (o != null) NwnxPersistence.Set(n.Registry, o.Id, VarKey(StringKind, name), NwnxValue.String(value), persist);
    }
    [NwnxFunc(P, "DeleteString")]
    static void DeleteString(NwnxCall n)
    {
        var o = n.PopObject(); var name = n.PopString();
        if (o != null) NwnxPersistence.Delete(n.Registry, o.Id, VarKey(StringKind, name));
    }
    [NwnxFunc(P, "GetFloat")]
    static void GetFloat(NwnxCall n)
    {
        var o = n.PopObject(); var name = n.PopString();
        n.Push(GetVar(n, o, FloatKind, name) is { Value: float f } ? f : 0f);
    }
    [NwnxFunc(P, "SetFloat")]
    static void SetFloat(NwnxCall n)
    {
        var o = n.PopObject(); var name = n.PopString(); float value = n.PopFloat(); bool persist = n.PopInt() != 0;
        if (o != null) NwnxPersistence.Set(n.Registry, o.Id, VarKey(FloatKind, name), NwnxValue.Float(value), persist);
    }
    [NwnxFunc(P, "DeleteFloat")]
    static void DeleteFloat(NwnxCall n)
    {
        var o = n.PopObject(); var name = n.PopString();
        if (o != null) NwnxPersistence.Delete(n.Registry, o.Id, VarKey(FloatKind, name));
    }

    /// <summary>Removes only the plugin's own variables (Set/Get Int, String, Float), never locals (nwnx_object_locals).</summary>
    [NwnxFunc(P, "DeleteVarRegex")]
    static void DeleteVarRegex(NwnxCall n)
    {
        var o = n.PopObject();
        var pattern = n.PopString();
        if (o == null) return;
        Regex re;
        try { re = new Regex("^(?:" + pattern + ")$"); }
        catch (ArgumentException) { return; }
        foreach (var key in NwnxPersistence.Keys(n.Registry, o.Id))
        {
            foreach (var kind in new[] { IntKind, StringKind, FloatKind })
                if (key.StartsWith(VarKey(kind, ""), StringComparison.Ordinal) && re.IsMatch(key[VarKey(kind, "").Length..]))
                    NwnxPersistence.Delete(n.Registry, o.Id, key);
        }
    }

    /// <summary>NWNX internal object type ids (CGameObject types).</summary>
    [NwnxFunc(P, "GetInternalObjectType")]
    static void InternalType(NwnxCall n) => n.Push(n.PopObject() switch
    {
        Module => 3,
        Area => 4,
        Creature => 5,
        Item => 6,
        Trigger => 7,
        Placeable => 9,
        Door => 10,
        Waypoint => 12,
        Encounter => 13,
        Store => 14,
        Sound => 16,
        { Type: ObjectType.AreaOfEffect } => 11,
        _ => 0,
    });
}
