using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Requests;

/// <summary>
/// Examining things: the "examine" request (right-click → Examine in the client) and the
/// ActionExamine script function both send the player an "examine" notify with the object's
/// name, description, picture and, for items, their properties.
/// </summary>
public static class Examine
{
    [ClientRequest("examine")]
    static void ExamineRequest(Request r)
    {
        if (r.World.Get(r.Id("object")) is not { } o) return;
        // Things in view, the player's own items, and the stock of the store they have open.
        bool visible = o switch
        {
            Item item => item.Area == r.Player.Area && item.Area != null || r.World.Inventory.Owner(item) == r.Player
                || r.World.Inventory.SessionOf(r.Player)?.Store is { } store && r.World.Inventory.Holder(item) == store,
            _ => o.Area == r.Player.Area,
        };
        if (!visible) return;
        // Examining an unidentified item of your own tries Lore on it first.
        if (o is Item { Identified: false } unknown && r.World.Inventory.Owner(unknown) == r.Player)
            r.World.System<Skills.SkillSystem>().TryIdentify(r.Player, unknown);
        Send(r.World, r.Player, o);
    }

    /// <summary>void ActionExamine(object oExamine): the player examines an object (shows the examine window).</summary>
    sealed class ExamineAction : GameAction
    {
        public uint Target;
        public override ActionStatus Step(World world, GameObject owner, float dt)
        {
            if (owner is Creature { IsPC: true } pc && world.Get(Target) is { } o) Send(world, pc, o);
            return ActionStatus.Done;
        }
    }

    [NwFunc("ActionExamine")]
    static object? ActionExamine(Call c)
    {
        c.Self?.Actions.Add(new ExamineAction { Target = c.O(0) });
        return null;
    }

    public static void Send(World w, Creature pc, GameObject o) =>
        w.Output?.Notify("examine", o, Describe(w, o, pc), pc);

    /// <summary>The examine window contents for any object.</summary>
    public static object Describe(World w, GameObject o, Creature? viewer = null)
    {
        var talk = w.Resources.Talk;
        string Strref(int strref) => strref >= 0 ? w.Resources.TlkString(unchecked((uint)strref)) : "";
        string description = o.Description;
        string portrait = "";
        string[] icon = [];
        string[] properties = [];
        string kind = o.Type.ToString().ToLowerInvariant();
        string subtitle = "";
        object? info = null;
        switch (o)
        {
            case Item item:
            {
                var t = item.Template;
                var desc = item.Identified ? t.GetLocString("DescIdentified") : t.GetLocString("Description");
                if (description.Length == 0 || !item.Identified) description = desc.Resolve(talk);
                // Items without their own text use the base item's description.
                if (description.Length == 0) description = Strref(w.Resources.TwoDA("baseitems").GetInt(item.BaseItem, "Description", -1));
                icon = w.Inventory.Icon(item, viewer);
                info = w.Inventory.ItemInfo(item, viewer);
                properties = item.Identified
                    ? item.Properties.Where(p => p.DurationType == 2 || p.DurationType == 1).Select(p => PropertyText(w, p)).Where(s => s.Length > 0).ToArray()
                    : ["Unidentified"];
                break;
            }
            case Creature c:
                if (description.Length == 0) description = c.Template.GetLocString("Description").Resolve(talk);
                // The portrait to show, or the shadowy default when it is missing from the game data.
                portrait = Portraits.Display(w, c);
                subtitle = CreatureSubtitle(w, c);
                break;
            default:
                if (description.Length == 0 && o is Placeable p) description = p.Template.GetLocString("Description").Resolve(talk);
                break;
        }
        return new
        {
            @object = o.Id,
            kind,
            name = o.Name.Resolve(talk),
            description,
            portrait,
            icon,
            properties,
            subtitle,
            info,
            identified = o is not Item { Identified: false },
        };
    }

    /// <summary>"Female Elf · Wizard 3 / Rogue 1" for the examine card of a creature.</summary>
    static string CreatureSubtitle(World w, Creature c)
    {
        var res = w.Resources;
        string Tlk(int strref) => strref >= 0 ? res.TlkString(unchecked((uint)strref)) : "";
        var races = res.TwoDA("racialtypes");
        string race = c.Race < races.RowCount ? Tlk(races.GetInt(c.Race, "Name", -1)) : "";
        string gender = res.TwoDA("gender") is var g && c.Gender < g.RowCount ? Tlk(g.GetInt(c.Gender, "NAME", -1)) : "";
        var classTable = res.TwoDA("classes");
        string classes = string.Join(" / ", c.Classes.Where(x => x.Level > 0 && x.Class < classTable.RowCount)
            .Select(x => $"{Tlk(classTable.GetInt(x.Class, "Name", -1))} {x.Level}".Trim()));
        string who = string.Join(" ", new[] { gender, race }.Where(s => s.Length > 0));
        return string.Join(" · ", new[] { who, classes }.Where(s => s.Length > 0));
    }

    /// <summary>
    /// The text the original shows for an item property: its name (itempropdef GameStrRef), then
    /// the subtype, the cost value and the parameter value names from their tables.
    /// </summary>
    public static string PropertyText(World w, ItemProperty p)
    {
        var res = w.Resources;
        var def = res.TwoDA("itempropdef");
        if (p.Type < 0 || p.Type >= def.RowCount) return "";
        string Name(string table, int row) =>
            table.Length > 0 && res.TwoDA(table.ToLowerInvariant()) is var t && row >= 0 && row < t.RowCount && t.GetInt(row, "Name", -1) is var s and >= 0
                ? res.TlkString(unchecked((uint)s)) : "";
        int nameRef = def.GetInt(p.Type, "GameStrRef", -1);
        if (nameRef < 0) nameRef = def.GetInt(p.Type, "Name", -1);
        var parts = new List<string> { nameRef >= 0 ? res.TlkString(unchecked((uint)nameRef)) : def.Get(p.Type, "Label") };
        if (def.Get(p.Type, "SubTypeResRef") is { Length: > 0 } sub) parts.Add(Name(sub, p.SubType));
        var costTables = res.TwoDA("iprp_costtable");
        if (p.CostTable > 0 && p.CostTable < costTables.RowCount) parts.Add(Name(costTables.Get(p.CostTable, "Name"), p.CostTableValue));
        var paramTables = res.TwoDA("iprp_paramtable");
        // Script-built properties leave Param1 at -1; the table is then itempropdef's.
        int paramTable = p.Param1 is >= 0 and < ItemPropertyRules.NoParam ? p.Param1 : def.IsEmpty(p.Type, "Param1ResRef") ? -1 : def.GetInt(p.Type, "Param1ResRef", -1);
        if (paramTable >= 0 && paramTable < paramTables.RowCount && paramTables.Get(paramTable, "TableResRef") is { Length: > 0 } param)
            parts.Add(Name(param, p.Param1Value));
        return string.Join(" ", parts.Where(s => s.Length > 0));
    }
}
