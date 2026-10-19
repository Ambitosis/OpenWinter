using System.Numerics;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Spells;

/// <summary>
/// Spell impacts and the state around them: runs a spell's ImpactScript (spells.2da) with OBJECT_SELF =
/// the caster and the spell context filled in (GetSpellId, GetCasterLevel, GetMetaMagicFeat, GetSpellSaveDC,
/// GetSpellTargetObject, GetSpellCastItem ...), hands that context to the areas of effect the spell creates,
/// keeps per-player spell targeting overrides (SetSpellTargetingData), and resolves counterspells.
/// Casting itself (range, animations, slots, interruption) is <see cref="CastSpellAction"/>.
/// </summary>
public sealed class SpellSystem : IWorldSystem
{
    readonly World _world;
    readonly Dictionary<(uint Player, int Spell), TargetingData> _targeting = new();

    public readonly record struct TargetingData(int Shape, float SizeX, float SizeY, int Flags);

    public SpellSystem(World world)
    {
        _world = world;
        world.ObjectAdded += OnObjectAdded;
        world.ObjectRemoved += o => { if (o is Creature) foreach (var key in _targeting.Keys.Where(k => k.Player == o.Id).ToList()) _targeting.Remove(key); };
    }

    public int Order => 12;
    public void Step(float dt) { }

    // ------------------------------------------------------------------ impact

    /// <summary>
    /// Runs the spell's impact script now as <paramref name="caster"/>. The spell becomes the caster's
    /// current spell (<see cref="GameObject.LastSpell"/>); hostile spells end the caster's invisibility and sanctuary.
    /// </summary>
    public void RunImpact(GameObject caster, SpellCastInfo info, Item? item = null)
    {
        caster.LastSpell = info;
        if (item != null) info.CastItem = item.Id;
        var script = SpellTable.ImpactScript(_world, info.SpellId);
        if (SpellTable.Hostile(_world, info.SpellId) && info.Target != caster.Id) BreakConcealment(caster);
        if (script.Length == 0 || _world.Scripts == null) return;
        var ctx = new EventContext().Set("SpellId", info.SpellId).Set("CasterLevel", info.CasterLevel);
        if (info.FeatId >= 0) ctx.Set("SpellFeat", info.FeatId);
        if (info.CastItem != GameObject.InvalidId) ctx.Set("SpellCastItem", info.CastItem);
        _world.Scripts.Run(script, caster.Id, ctx);
    }

    /// <summary>
    /// Casts an item's spell property (iprp_spells.2da: SpellIndex, CasterLvl, InnateLvl) at once: the
    /// item activation path once charges are checked. Item spells use a fixed save DC of
    /// 10 + spell level + the minimum ability bonus for that level.
    /// </summary>
    public void CastFromItem(GameObject user, Item item, int iprpSpell, GameObject? target, Location location)
    {
        var iprp = _world.Resources.TwoDA("iprp_spells");
        int spell = iprp.GetInt(iprpSpell, "SpellIndex", -1);
        if (spell < 0) return;
        int level = iprp.GetInt(iprpSpell, "InnateLvl", SpellTable.Innate(_world, spell));
        var info = new SpellCastInfo
        {
            SpellId = spell,
            CasterLevel = Math.Max(1, iprp.GetInt(iprpSpell, "CasterLvl", 1)),
            ClassType = NwRules.ClassInvalid,
            SaveDC = 10 + level + level / 2,
            SpellLevel = level,
            CastItem = item.Id,
            Target = target?.Id ?? GameObject.InvalidId,
            TargetLocation = target != null ? TargetLocationOf(target) : location,
        };
        RunImpact(user, info, item);
    }

    /// <summary>Where a target object is for GetSpellTargetLocation (an item's holder when it is in an inventory).</summary>
    public Location TargetLocationOf(GameObject target)
    {
        if (target is Item item && item.Area == null && _world.Inventory.Owner(item) is { } holder) return holder.Location;
        return target.Location;
    }

    /// <summary>Hostile spellcasting ends normal invisibility, sanctuary and etherealness.</summary>
    void BreakConcealment(GameObject caster)
    {
        var k = EffectConstants.Of(_world);
        int invis = k["EFFECT_TYPE_INVISIBILITY"], normal = k["INVISIBILITY_TYPE_NORMAL"];
        int sanctuary = k["EFFECT_TYPE_SANCTUARY"], ethereal = k["EFFECT_TYPE_ETHEREAL"];
        if (!caster.Effects.Any(e => (e.Type == invis && e.Ints[0] == normal) || e.Type == sanctuary || e.Type == ethereal)) return;
        _world.System<EffectSystem>().RemoveWhere(caster, e => (e.Type == invis && e.Ints[0] == normal) || e.Type == sanctuary || e.Type == ethereal);
    }

    /// <summary>Areas of effect created by a spell inherit its context (caster level, metamagic, class, save DC).</summary>
    void OnObjectAdded(GameObject o)
    {
        if (o is not AreaOfEffect aoe || aoe.LastSpell != null) return;
        var creator = _world.Get(aoe.Creator);
        var info = creator?.LastSpell;
        if (creator == null || info == null || info.SpellId != aoe.SpellId || aoe.SpellId < 0) return;
        var copy = info.Copy();
        if (copy.SaveDC < 0) copy.SaveDC = CreatureRules.SpellSaveDC(_world, creator);
        if (aoe.CasterLevel > 0) copy.CasterLevel = aoe.CasterLevel;
        else aoe.CasterLevel = copy.CasterLevel;
        aoe.LastSpell = copy;
    }

    // ------------------------------------------------------------------ targeting

    /// <summary>SetSpellTargetingData: overrides the spells.2da targeting shape for one player this session.</summary>
    public void SetTargeting(Creature player, int spell, int shape, float sizeX, float sizeY, int flags)
    {
        _targeting[(player.Id, spell)] = new TargetingData(shape, sizeX, sizeY, flags);
        _world.Output?.Notify("spell-targeting", player, new { spell, shape, sizeX, sizeY, flags }, player);
    }

    public TargetingData? Targeting(Creature player, int spell) =>
        _targeting.TryGetValue((player.Id, spell), out var t) ? t : null;

    // ------------------------------------------------------------------ counterspells

    static readonly int[] DispelLadder = { 94, 41, 67, 122 }; // lesser dispel, dispel magic, greater dispelling, disjunction
    static readonly string[] DispelThresholds =
    {
        "COUNTERSPELL_LESSER_DISPEL_THRESHOLD", "COUNTERSPELL_DISPEL_THRESHOLD",
        "COUNTERSPELL_GREATER_DISPEL_THRESHOLD", "COUNTERSPELL_MORDENKAINENS_DISJUNCTION",
    };
    static readonly int[] DispelDefaults = { 2, 3, 5, 9 };

    /// <summary>
    /// A creature started casting: anyone counterspelling it (ActionCounterSpell at the front of its queue)
    /// identifies the spell (Spellcraft DC 15 + spell level) and spends the same spell, a spells.2da counter
    /// spell, or a dispel strong enough for the level. Returns the counterspeller, or null.
    /// </summary>
    public Creature? TryCounter(GameObject caster, SpellCastInfo info)
    {
        if (caster.Area == null) return null;
        int level = Math.Max(0, info.SpellLevel);
        foreach (var c in caster.Area.Objects.OfType<Creature>().ToList())
        {
            if (c == caster || c.IsDead || c.Actions.Count == 0 || c.Actions[0] is not CounterSpellAction cs || cs.Target != caster.Id) continue;
            if (!CreatureRules.SkillCheck(_world, c, 16, 15 + level)) continue; // SKILL_SPELLCRAFT
            var candidates = new List<int> { info.SpellId, SpellTable.Counter1(_world, info.SpellId), SpellTable.Counter2(_world, info.SpellId) };
            for (int i = 0; i < DispelLadder.Length; i++)
                if (level <= Ruleset.Int(_world, DispelThresholds[i], DispelDefaults[i])) candidates.Add(DispelLadder[i]);
            foreach (int counter in candidates.Where(s => s >= 0).Distinct())
            {
                var slot = SpellSlots.Find(_world, c, counter, SpellSlots.MetaAny, -1, 0, false);
                if (slot == null) continue;
                SpellSlots.Spend(_world, c, slot);
                cs.Countered = true;
                Notify("spell-countered", caster, new { spell = info.SpellId, counter, by = c.Id });
                _world.Output?.PlayAnimation(c, SpellTable.CastAnimation("out"), 1, 1f);
                return c;
            }
        }
        return null;
    }

    // ------------------------------------------------------------------ client data

    /// <summary>Tells clients in the subject's area.</summary>
    public void Notify(string kind, GameObject subject, object data)
    {
        if (subject is Areas.Area || subject.Area != null) _world.Output?.Notify(kind, subject, data);
    }

    public static float[] Vec(Vector3 v) => new[] { v.X, v.Y, v.Z };
}
