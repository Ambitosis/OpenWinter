using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;

namespace OpenWinter.Game.Objects;

/// <summary>Builds creatures from UTC/BIC-shaped GFF data (blueprints, GIT instances, character files).</summary>
public sealed class CreatureFactory
{
    /// <summary>Body part field names in BodyParts index order (matches Appearance.BodyParts on the wire).</summary>
    public static readonly string[] BodyPartFields =
    {
        "Appearance_Head", "BodyPart_Neck", "BodyPart_Torso", "BodyPart_Pelvis", "BodyPart_Belt",
        "BodyPart_RShoul", "BodyPart_LShoul", "BodyPart_RBicep", "BodyPart_LBicep", "BodyPart_RFArm",
        "BodyPart_LFArm", "BodyPart_RHand", "BodyPart_LHand", "BodyPart_RThigh", "BodyPart_LThigh",
        "BodyPart_RShin", "BodyPart_LShin", "BodyPart_RFoot", "BodyPart_LFoot",
    };

    static readonly string[] CreatureScripts =
    {
        "ScriptAttacked", "ScriptDamaged", "ScriptDeath", "ScriptDialogue", "ScriptDisturbed", "ScriptEndRound",
        "ScriptHeartbeat", "ScriptOnBlocked", "ScriptOnNotice", "ScriptRested", "ScriptSpawn", "ScriptSpellAt", "ScriptUserDefine",
    };

    readonly World _world;

    public CreatureFactory(World world) { _world = world; }

    public Creature FromTemplate(GffStruct s, bool validateStartingEquipment = false)
    {
        var c = new Creature
        {
            Template = s,
            Tag = s.GetString("Tag"),
            ResRef = s.GetString("TemplateResRef"),
            AppearanceType = (int)s.GetInt("Appearance_Type"),
            Gender = (byte)s.GetInt("Gender"),
            Phenotype = (byte)s.GetInt("Phenotype"),
            Race = (byte)s.GetInt("Race"),
            Portrait = s.GetString("Portrait"),
            PortraitId = (int)s.GetInt("PortraitId"),
            Conversation = s.GetString("Conversation"),
            Faction = (int)s.GetInt("FactionID"),
            CurrentHitPoints = (int)s.GetInt("CurrentHitPoints", 1),
            MaxHitPoints = (int)s.GetInt("MaxHitPoints", 1),
            Plot = s.GetInt("Plot") != 0,
            IsImmortal = s.GetInt("IsImmortal") != 0,
            Tail = (int)s.GetInt("Tail_New", s.GetInt("Tail")),
            Wings = (int)s.GetInt("Wings_New", s.GetInt("Wings")),
        };
        var first = s.GetLocString("FirstName");
        var last = s.GetLocString("LastName");
        c.Name = new LocString(string.Join(" ", new[] { first.Resolve(_world.Resources.Talk), last.Resolve(_world.Resources.Talk) }.Where(x => x.Length > 0)));
        c.Description = s.GetLocString("Description").FirstString;
        // A blueprint without body part fields (the whole-body NPC models: nw_bandit001, nw_elfmage001) has none: turned
        // part-based by SetCreatureAppearanceType, the original draws such a creature without a head (parity/visual,
        // scene pavel). It draws no neck, arms or hands either, though the clothes have pieces there (1, or 2 set by
        // script); that part isn't understood yet.
        for (int i = 0; i < BodyPartFields.Length; i++) c.BodyParts[i] = (ushort)Wide(s, BodyPartFields[i], 0);
        // Character files store the right foot under the armour label (a long-standing quirk).
        if (!s.Has("BodyPart_RFoot") && s.Has("ArmorPart_RFoot")) c.BodyParts[17] = (ushort)Wide(s, "ArmorPart_RFoot", 0);
        foreach (var entry in s.GetList("Equip_ItemList"))
            if (_world.Items.FromListEntry(entry) is { } item)
            {
                _world.Register(item);
                item.Possessor = c.Id;
                c.Equipment[(int)entry.StructType] = item;
            }
        foreach (var entry in s.GetList("ItemList"))
            if (_world.Items.FromListEntry(entry) is { } item)
            {
                _world.Register(item);
                c.Inventory.Add(item);
            }
        c.Colors = new[] { (byte)s.GetInt("Color_Skin"), (byte)s.GetInt("Color_Hair"), (byte)s.GetInt("Color_Tattoo1"), (byte)s.GetInt("Color_Tattoo2") };
        foreach (var f in CreatureScripts) c.Scripts[f] = s.GetString(f);
        c.Locals.LoadFrom(s);
        c.LoadStats(s, _world.Resources.TwoDA("classes"));
        Rules.CreatureOverridesPersistence.Load(c);
        if (validateStartingEquipment)
            _world.System<Inventory.InventorySystem>().ValidateStartingEquipment(c);

        // creaturespeed.2da: WalkRate in the template indexes it; PC rows use the player rates.
        var speeds = _world.Resources.TwoDA("creaturespeed");
        int rate = (int)s.GetInt("WalkRate", 4);
        if (s.GetInt("IsPC") != 0) rate = 0;
        if (rate == 7)
        {
            // "Default": use the appearance's movement rate.
            var mr = _world.Resources.TwoDA("appearance").Get(c.AppearanceType, "MOVERATE");
            rate = speeds.Find("2DAName", mr) is int r && r >= 0 ? r : 4;
        }
        // GetMovementRate reports the resolved rate (a goblin's "default" is 4, normal), as the original does.
        c.MovementRateIndex = rate;
        c.WalkRate = speeds.GetFloat(rate, "WALKRATE", 1.75f);
        c.RunRate = speeds.GetFloat(rate, "RUNRATE", 3.5f);
        if (c.RunRate <= 0) { c.WalkRate = 0; c.RunRate = 0; }
        Persistence.QuickbarPersistence.BindItems(c);
        return c;
    }

    /// <summary>EE keeps part numbers above 255 in a parallel "x"-prefixed field.</summary>
    public static long Wide(GffStruct s, string field, long fallback = 0) =>
        s.Has("x" + field) ? s.GetInt("x" + field) : s.GetInt(field, fallback);

    public Creature? FromBlueprint(string resref)
    {
        var utc = _world.Resources.ReadGff(resref, ResType.Utc);
        if (utc == null) return null;
        var c = FromTemplate(utc, validateStartingEquipment: true);
        c.ResRef = resref;
        return c;
    }
}
