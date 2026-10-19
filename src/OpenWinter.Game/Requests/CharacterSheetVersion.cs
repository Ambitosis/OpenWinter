using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Requests;

/// <summary>
/// Cheap raw-input fingerprint for conditional sheet requests. No derived combat,
/// skill or feat calculations, localized descriptions, sorting, or sheet allocation.
/// Keep this beside CharacterRequests.Sheet when adding new sheet dependencies.
/// Direct script mutations are included even when they do not call MarkChanged.
/// </summary>
public static class CharacterSheetVersion
{
    public static string Read(World w, Creature c)
    {
        CreatureRules.EnsureInit(w, c);
        var h = new InputHash();
        h.Add(c.Id); h.Add(c.Name.Resolve(w.Resources.Talk)); h.Add(c.Description);
        h.Add(c.Portrait); h.Add(c.PortraitId); h.Add(c.Race); h.Add(c.Subrace);
        h.Add(c.Gender); h.Add(c.Deity); h.Add(c.Age);
        h.Add(c.LawfulChaotic); h.Add(c.GoodEvil); h.Add(c.Experience);
        h.Add(c.CurrentHitPoints); h.Add(c.MaxHitPoints); h.Add(c.BaseHitPoints); h.Add(c.HitPointOffset);
        h.Add(c.NaturalAC); h.Add(c.Size); h.Add(c.AppearanceType); h.Add(c.BaseAttackOverride);
        h.Add(c.FortBonus); h.Add(c.RefBonus); h.Add(c.WillBonus);
        h.Add(c.WalkRate); h.Add(c.RunRate); h.Add(c.IsDead); h.Add(c.InCombat);
        h.Add(c.Abilities.Length); foreach (int v in c.Abilities) h.Add(v);
        h.Add(c.Skills.Length); foreach (int v in c.Skills) h.Add(v);
        h.Add(c.Feats.Count); foreach (int v in c.Feats) h.Add(v);
        h.Add(c.FeatUses.Count); foreach (var v in c.FeatUses) { h.Add(v.Key); h.Add(v.Value); }
        h.Add(c.ActionModes.Count); foreach (int v in c.ActionModes) h.Add(v);
        h.Add(c.Classes.Count);
        foreach (var cl in c.Classes)
        { h.Add(cl.Class); h.Add(cl.Level); h.Add(cl.Domain1); h.Add(cl.Domain2); h.Add(cl.School); }
        var history = c.Template.GetList("LvlStatList");
        h.Add(history.Count); foreach (var level in history) h.Add(level.GetInt("LvlStatClass"));
        h.Add(w.Limits.AbilityBonus); h.Add(w.Limits.AbilityPenalty); h.Add(w.Limits.AttackBonus);
        h.Add(w.Limits.DamageBonus); h.Add(w.Limits.SavingThrowBonus); h.Add(w.Limits.SkillBonus);
        h.Add(c.Effects.Count);
        foreach (var e in c.Effects)
        {
            h.Add(e.UniqueId); h.Add(e.Type); h.Add(e.SubType); h.Add(e.Hidden);
            h.Add(e.Creator); h.Add(e.SpellId); h.Add(e.CasterLevel);
            h.Add(e.VersusRace); h.Add(e.VersusLawChaos); h.Add(e.VersusGoodEvil); h.Add(e.VersusTraps);
            h.Add(e.CurseBlockedAbilities);
            // Curse Constitution becomes active after its application tick.
            h.Add(e.Type == NwRules.EffectCurse && e.AppliedAt < w.Time);
            foreach (int v in e.Ints) h.Add(v);
            foreach (float v in e.Floats) h.Add(v);
            foreach (string v in e.Strings) h.Add(v);
        }
        h.Add(c.Equipment.Count);
        foreach (var (slot, item) in c.Equipment) { h.Add(slot); Item(w, item, ref h); }
        h.Add(c.Inventory.Count); foreach (var item in c.Inventory) Item(w, item, ref h);
        h.Add(c.PolymorphForm != null);
        if (c.PolymorphForm is { } p)
        {
            h.Add(p.Row); h.Add(p.Str); h.Add(p.Con); h.Add(p.Dex); h.Add(p.NaturalAC); h.Add(p.Ending);
            h.Add(p.Stored.Count);
            foreach (var (slot, item) in p.Stored) { h.Add(slot); h.Add(p.StillHeld(c, item)); Item(w, item, ref h); }
        }
        return h.Value.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
    }

    static void Item(World w, Item item, ref InputHash h, int depth = 0)
    {
        h.Add(item.Id); h.Add(item.BaseItem); h.Add(item.StackSize); h.Add(item.IsDestroyed);
        h.Add(item.Name.Resolve(w.Resources.Talk));
        h.Add(CreatureFactory.Wide(item.Template, "ArmorPart_Torso"));
        h.Add(item.Properties.Count);
        foreach (var p in item.Properties)
        {
            h.Add(p.Type); h.Add(p.SubType); h.Add(p.CostTable); h.Add(p.CostTableValue);
            h.Add(p.Param1); h.Add(p.Param1Value); h.Add(p.Useable); h.Add(p.UsesPerDay);
        }
        h.Add(item.Inventory.Count);
        // The weight calculation also limits malformed/cyclic container nesting.
        if (depth < 4) foreach (var child in item.Inventory) Item(w, child, ref h, depth + 1);
    }

    struct InputHash()
    {
        public ulong Value = 14695981039346656037;
        public void Add(long value)
        {
            unchecked { for (int i = 0; i < 8; i++) { Value = (Value ^ (byte)value) * 1099511628211; value >>= 8; } }
        }
        public void Add(bool value) => Add(value ? 1L : 0L);
        public void Add(float value) => Add(BitConverter.SingleToInt32Bits(value));
        public void Add(string value) { Add(value.Length); foreach (char c in value) Add((long)c); }
    }
}
