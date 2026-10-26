using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>NWNX_Object. Argument order follows the nwnx_object.nss wrappers (popped in reverse push order).</summary>
static class ObjectPlugin
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

    [NwnxFunc(P, "SetPosition")]
    static void SetPosition(NwnxCall n)
    {
        var o = n.PopObject();
        float z = n.PopFloat(), y = n.PopFloat(), x = n.PopFloat();
        n.PopInt(); // bUpdateSubareas
        if (o == null) return;
        o.Position = new Vector3(x, y, z);
        n.World.MarkChanged(o);
    }

    [NwnxFunc(P, "AddToArea")]
    static void AddToArea(NwnxCall n)
    {
        var o = n.PopObject();
        var area = n.PopObject<Area>();
        float x = n.PopFloat(), y = n.PopFloat(), z = n.PopFloat();
        if (o != null && area != null) n.World.AddToArea(o, area, new Vector3(x, y, z), o.Facing, exact: true);
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
        float x = n.PopFloat(), y = n.PopFloat(), z = n.PopFloat();
        n.Push(t != null && t.Contains(new Vector3(x, y, z)));
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

    // Persistent object variables (separate from locals; NWNX stores them in the object's GFF).
    static string VarKey(string kind, string name) => "var:" + kind + ":" + name;

    [NwnxFunc(P, "GetInt")]
    static void GetInt(NwnxCall n)
    {
        var o = n.PopObject(); var name = n.PopString();
        n.Push(o != null && n.State(o).TryGetValue(VarKey("i", name), out var v) && v.Value is int i ? i : 0);
    }
    [NwnxFunc(P, "SetInt")]
    static void SetInt(NwnxCall n)
    {
        var o = n.PopObject(); var name = n.PopString(); int value = n.PopInt(); n.PopInt();
        if (o != null) n.State(o)[VarKey("i", name)] = NwnxValue.Int(value);
    }
    [NwnxFunc(P, "DeleteInt")]
    static void DeleteInt(NwnxCall n)
    {
        var o = n.PopObject(); var name = n.PopString();
        if (o != null) n.State(o).Remove(VarKey("i", name));
    }
    [NwnxFunc(P, "GetString")]
    static void GetString(NwnxCall n)
    {
        var o = n.PopObject(); var name = n.PopString();
        n.Push(o != null && n.State(o).TryGetValue(VarKey("s", name), out var v) && v.Value is string s ? s : "");
    }
    [NwnxFunc(P, "SetString")]
    static void SetString(NwnxCall n)
    {
        var o = n.PopObject(); var name = n.PopString(); var value = n.PopString(); n.PopInt();
        if (o != null) n.State(o)[VarKey("s", name)] = NwnxValue.String(value);
    }
    [NwnxFunc(P, "DeleteString")]
    static void DeleteString(NwnxCall n)
    {
        var o = n.PopObject(); var name = n.PopString();
        if (o != null) n.State(o).Remove(VarKey("s", name));
    }
    [NwnxFunc(P, "GetFloat")]
    static void GetFloat(NwnxCall n)
    {
        var o = n.PopObject(); var name = n.PopString();
        n.Push(o != null && n.State(o).TryGetValue(VarKey("f", name), out var v) && v.Value is float f ? f : 0f);
    }
    [NwnxFunc(P, "SetFloat")]
    static void SetFloat(NwnxCall n)
    {
        var o = n.PopObject(); var name = n.PopString(); float value = n.PopFloat(); n.PopInt();
        if (o != null) n.State(o)[VarKey("f", name)] = NwnxValue.Float(value);
    }
    [NwnxFunc(P, "DeleteFloat")]
    static void DeleteFloat(NwnxCall n)
    {
        var o = n.PopObject(); var name = n.PopString();
        if (o != null) n.State(o).Remove(VarKey("f", name));
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
        var state = n.State(o);
        foreach (var key in state.Keys.Where(k => k.StartsWith("var:", StringComparison.Ordinal) && re.IsMatch(k[(k.IndexOf(':', 4) + 1)..])).ToList())
            state.Remove(key);
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
