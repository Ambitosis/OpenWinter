using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Rules;

/// <summary>Familiars and animal companions (hen_familiar.2da / hen_companion.2da).</summary>
public static partial class CreatureRules
{
    /// <summary>Whether the creature can have a familiar (Summon Familiar feat) or animal companion (Animal Companion feat).</summary>
    public static bool HasAssociateAbility(World w, Creature c, int associateType)
    {
        EnsureInit(w, c);
        return associateType == NwRules.AssociateFamiliar
            ? OwnsFeat(w, c, NwRules.FeatSummonFamiliar)
            : OwnsFeat(w, c, NwRules.FeatAnimalCompanion);
    }

    /// <summary>The level of a summoned associate: familiars follow wizard + sorcerer levels, companions the greater of druid and ranger levels.</summary>
    public static int AssociateLevel(Creature c, int associateType) => associateType == NwRules.AssociateFamiliar
        ? ClassLevel(c, NwRules.ClassWizard) + ClassLevel(c, NwRules.ClassSorcerer)
        : Math.Max(ClassLevel(c, NwRules.ClassDruid), ClassLevel(c, NwRules.ClassRanger));

    /// <summary>
    /// "Summoned a creature" (dialog.tlk 68518) to a player whose summon or familiar just appeared: measured on nwmain
    /// (visual scenes castlines3, castlines4) for the Summon Familiar feat, SummonFamiliar, EffectSummonCreature and a
    /// wand of Lesser Summoning; an NPC's familiar told the player nothing. (Animal companions: not measured.)
    /// </summary>
    public static void SummonedLine(World w, Creature master)
    {
        if (master.IsPC) w.Output?.SendMessage(master, w.TlkStringOverridden(SummonedStrref));
    }

    public const int SummonedStrref = 68518;

    /// <summary>
    /// SummonFamiliar / SummonAnimalCompanion: unsummons the current one and creates the blueprint
    /// for the master's type and level (BASERESREF + two-digit level, falling back to lower levels).
    /// </summary>
    public static Creature? SummonAssociate(World w, Creature master, int associateType)
    {
        EnsureInit(w, master);
        if (master.Area is not { } area) return null;
        bool familiar = associateType == NwRules.AssociateFamiliar;
        int type = familiar ? master.FamiliarType : master.CompanionType;
        if (type < 0 || type == 255) type = 0;
        var baseRef = w.Resources.TwoDA(familiar ? "hen_familiar" : "hen_companion").Get(type, "BASERESREF");
        if (baseRef.Length == 0) return null;

        Creature? summoned = null;
        for (int lvl = Math.Clamp(AssociateLevel(master, associateType), 1, 40); lvl >= 1 && summoned == null; lvl--)
            summoned = w.Creatures.FromBlueprint($"{baseRef}{lvl:00}".ToLowerInvariant());
        summoned ??= w.Creatures.FromBlueprint(baseRef.ToLowerInvariant());
        if (summoned == null) return null;

        foreach (var old in w.AllObjects.OfType<Creature>().ToList())
            if (old.Master == master.Id && old.AssociateType == associateType && !old.IsDestroyed) w.Destroy(old);

        var name = familiar ? master.FamiliarName : master.CompanionName;
        if (name.Length > 0) summoned.Name = new LocString(name);
        summoned.Master = master.Id;
        summoned.AssociateType = associateType;
        summoned.Faction = master.Faction;
        var offset = new Vector3(MathF.Cos(master.Facing + MathF.PI * 0.75f), MathF.Sin(master.Facing + MathF.PI * 0.75f), 0) * 1.5f;
        var pos = master.Position + offset;
        if (area.Walkmesh.GroundHeight(pos.X, pos.Y, pos.Z, 2f) is { } z) pos.Z = z; else pos = master.Position;
        w.Inventory.FireSpawnAcquires(summoned);
        w.AddToArea(summoned, area, pos, master.Facing);
        w.RunEvent(summoned, "ScriptSpawn", new Scripting.EventContext());
        SummonedLine(w, master);
        if (familiar)
        {
            // Native effectidentity replaces every candidate VFX with authored bit markers:
            // SummonFamiliar selects vff_summund (row 36), not polymorph or summon-monster.
            // The independent ground burst stays at the master's position while the familiar appears.
            w.Output?.Notify("appear", summoned, new { animation = 1 });
            var k = EffectConstants.Of(w);
            var arrival = new Effect { Type = k.VisualEffect, Creator = master.Id };
            arrival.Ints[0] = k["VFX_FNF_SUMMON_UNDEAD"];
            arrival.Floats[0] = 1;
            w.System<EffectSystem>().ApplyAtLocation(arrival, k.Instant,
                new Location(area.Id, master.Position, master.Facing * 180 / MathF.PI), 0);
        }
        return summoned;
    }
}
