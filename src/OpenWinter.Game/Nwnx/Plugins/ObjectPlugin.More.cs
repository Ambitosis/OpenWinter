using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Spells;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>NWNX_Object: locks and traps, damage immunity, visual effects, areas of effect, spells and UUIDs (parity suite nwnx_object).</summary>
static partial class ObjectPlugin
{
    [NwnxFunc("Object", "GetAutoRemoveKey")]
    static void GetAutoRemoveKey(NwnxCall n) => n.Push(n.PopObject() is ILockable l and (Door or Placeable) ? (l.Lock.AutoRemoveKey ? 1 : 0) : -1);

    [NwnxFunc("Object", "SetAutoRemoveKey")]
    static void SetAutoRemoveKey(NwnxCall n)
    {
        var o = n.PopObject();
        bool remove = n.PopInt() != 0;
        if (o is ILockable l and (Door or Placeable)) l.Lock.AutoRemoveKey = remove;
    }

    /// <summary>The object GetEnteringObject reports in a door's or placeable's trap script.</summary>
    [NwnxFunc("Object", "SetLastTriggered")]
    static void SetLastTriggered(NwnxCall n)
    {
        var o = n.PopObject();
        uint last = n.PopObjectId();
        if (o is ITrappable t and (Door or Placeable)) t.Trap.LastTriggeredBy = last;
    }

    /// <summary>A creature becomes the trap's creator and gives it its faction; anything else clears the creator.</summary>
    [NwnxFunc("Object", "SetTrapCreator")]
    static void SetTrapCreator(NwnxCall n)
    {
        var o = n.PopObject();
        var creator = n.PopObject();
        if (o is not ITrappable t) return;
        if (creator is Creature c)
        {
            t.Trap.Creator = c.Id;
            t.Trap.Faction = c.Faction;
        }
        else t.Trap.Creator = GameObject.InvalidId;
        if (t.IsTrapped) WorldObjects.TrapSystem.Publish(n.World, o);
    }

    /// <summary>Damage immunity percentage for one damage type: effects (vulnerability lowers it) and equipped items.</summary>
    [NwnxFunc("Object", "GetDamageImmunity")]
    static void GetDamageImmunity(NwnxCall n)
    {
        var o = n.PopObject();
        int type = n.PopInt();
        if (o == null) { n.Push(-1); return; }
        var k = EffectConstants.Of(n.World);
        int pct = 0;
        foreach (var e in EffectQueries.Active(o, k.DamageImmunityIncrease, null)) if ((e.Ints[0] & type) != 0) pct += e.Ints[1];
        foreach (var e in EffectQueries.Active(o, k.DamageImmunityDecrease, null)) if ((e.Ints[0] & type) != 0) pct -= e.Ints[1];
        pct += ItemPropertyRules.ItemDamageImmunity(n.World, o, type);
        n.Push(Math.Clamp(pct, -100, 100));
    }

    [NwnxFunc("Object", "GetHasVisualEffect")]
    static void GetHasVisualEffect(NwnxCall n)
    {
        var o = n.PopObject();
        int vfx = n.PopInt();
        var k = EffectConstants.Of(n.World);
        n.Push(o != null && o.Effects.Any(e => (e.Type == k.VisualEffect || e.Type == k.Beam) && e.Ints[0] == vfx));
    }

    /// <summary>Items in the object's inventory, those inside containers included, equipment not.</summary>
    [NwnxFunc("Object", "GetInventoryItemCount")]
    static void GetInventoryItemCount(NwnxCall n)
    {
        var o = n.PopObject();
        if (o == null) { n.Push(-1); return; }
        if (InventorySystem.InventoryList(o) == null) { n.Push(0); return; }
        n.World.Inventory.Load(o);
        static int Count(List<Item> items) => items.Where(i => !i.IsDestroyed).Sum(i => 1 + Count(i.Inventory));
        n.Push(Count(InventorySystem.InventoryList(o)!));
    }

    /// <summary>doortypes.2da VisibleModel of the door's appearance; generic doors (appearance 0) always have one (nwnx_object).</summary>
    [NwnxFunc("Object", "GetDoorHasVisibleModel")]
    static void GetDoorHasVisibleModel(NwnxCall n)
    {
        if (n.PopObject() is not Door d) { n.Push(0); return; }
        n.Push(n.World.Resources.TwoDA("doortypes").GetInt(d.Appearance, "VisibleModel", 1) != 0);
    }

    /// <summary>The engine's animation numbers for ANIMATION_* constants, measured with ActionPlayAnimation (nwnx_object).</summary>
    static readonly Dictionary<int, int> EngineAnimations = new()
    {
        [0] = 0, [1] = 52, [2] = 30, [3] = 32, [4] = 33, [5] = 48, [6] = 36, [7] = 47, [8] = 38, [9] = 39, [10] = 40,
        [11] = 41, [12] = 59, [13] = 60, [14] = 57, [15] = 58, [16] = 6, [17] = 8, [18] = 15, [19] = 16, [20] = 93,
        [100] = 53, [103] = 56, [105] = 35, [107] = 29, [108] = 28, [109] = 44, [112] = 71, [113] = 70,
    };
    const int AnimationReady = 1, AnimationPause = 0, AnimationWalk = 2, AnimationRun = 4, AnimationDead = 8;

    /// <summary>
    /// A creature's current animation in the engine's numbering: the one it is playing, walking or running, dead, or
    /// standing (ready while its AI idles at the lowest level, else paused). Other objects report 0.
    /// </summary>
    [NwnxFunc("Object", "GetCurrentAnimation")]
    static void GetCurrentAnimation(NwnxCall n)
    {
        var o = n.PopObject();
        if (o == null) { n.Push(-1); return; }
        if (o is not Creature c) { n.Push(0); return; }
        if (c.IsDead) { n.Push(AnimationDead); return; }
        if (c.Actions.FirstOrDefault() is AnimationAction a && EngineAnimations.TryGetValue(a.Animation, out int playing)) { n.Push(playing); return; }
        if (c.Speed > 0) { n.Push(c.Mode is OpenWinter.Net.MovementMode.Run or OpenWinter.Net.MovementMode.MouseRun ? AnimationRun : AnimationWalk); return; }
        n.Push(n.World.EffectiveAILevel(c) > 0 ? AnimationPause : AnimationReady);
    }

    // ---------------------------------------------------------------- areas of effect

    [NwnxFunc("Object", "GetAoEObjectDurationRemaining")]
    static void GetAoEDuration(NwnxCall n) =>
        n.Push(n.PopObject() is AreaOfEffect a && a.ExpiresAt > 0 ? (float)Math.Max(0, a.ExpiresAt - n.World.Time) : 0f);

    [NwnxFunc("Object", "GetAoEObjectRadius")]
    static void GetAoERadius(NwnxCall n) => n.Push(n.PopObject() is AreaOfEffect { Shape: 'C' } a ? a.Radius : 0f);

    [NwnxFunc("Object", "SetAoEObjectRadius")]
    static void SetAoERadius(NwnxCall n)
    {
        var o = n.PopObject();
        float radius = n.PopFloat();
        if (o is AreaOfEffect { Shape: 'C' } a && radius > 0)
        {
            a.Radius = radius;
            n.World.MarkChanged(a);
        }
    }

    // ---------------------------------------------------------------- spells

    /// <summary>The spell a defence check is about: the given one, else the caster's current spell.</summary>
    static int SpellFor(GameObject? caster, int spell) => spell >= 0 ? spell : caster?.LastSpell?.SpellId ?? -1;

    /// <summary>2 when the defender is immune to the spell (effects and items), else -1.</summary>
    [NwnxFunc("Object", "DoSpellImmunity")]
    static void DoSpellImmunity(NwnxCall n)
    {
        var defender = n.PopObject();
        var caster = n.PopObject();
        int spell = SpellFor(caster, n.PopInt());
        if (defender == null || spell < 0) { n.Push(-1); return; }
        int school = SpellTable.School(n.World, spell);
        int level = SpellDefense.LevelOf(n.World, caster?.LastSpell, spell);
        n.Push(SpellDefense.Immune(n.World, defender, caster, spell, school, level, feedback: true) ? 2 : -1);
    }

    /// <summary>2 for a globe (unlimited absorption), 3 for a mantle (which loses the levels), else -1.</summary>
    [NwnxFunc("Object", "DoSpellLevelAbsorption")]
    static void DoSpellLevelAbsorption(NwnxCall n)
    {
        var defender = n.PopObject();
        var caster = n.PopObject();
        int spell = SpellFor(caster, n.PopInt());
        int level = n.PopInt();
        int school = n.PopInt();
        if (defender == null || spell < 0) { n.Push(-1); return; }
        if (school < 0) school = SpellTable.School(n.World, spell);
        if (level < 0) level = SpellDefense.LevelOf(n.World, caster?.LastSpell, spell);
        if (SpellDefense.Absorb(n.World, defender, caster, spell, school, level, limited: false, removeLevels: false, feedback: true)) n.Push(2);
        else if (SpellDefense.Absorb(n.World, defender, caster, spell, school, level, limited: true, removeLevels: true, feedback: true)) n.Push(3);
        else n.Push(-1);
    }

    /// <summary>Removes every effect the object created, wherever it is, its own included, spell or not (nwnx_object).</summary>
    [NwnxFunc("Object", "ClearSpellEffectsOnOthers")]
    static void ClearSpellEffectsOnOthers(NwnxCall n)
    {
        if (n.PopObject() is not { } caster) return;
        var sys = n.World.System<EffectSystem>();
        foreach (var o in n.World.AllObjects.Where(x => !x.IsDestroyed && x.Effects.Count > 0).ToList())
            sys.RemoveWhere(o, e => e.Creator == caster.Id);
    }

    [NwnxFunc("Object", "GetLastSpellCastFeat")]
    static void LastSpellFeat(NwnxCall n) => n.Push(n.PopObject() is { } o ? o.LastSpell is { FeatId: >= 0 } s ? s.FeatId : 65535 : -1);

    [NwnxFunc("Object", "GetLastSpellCastSpontaneous")]
    static void LastSpellSpontaneous(NwnxCall n) => n.Push(n.PopObject() is Creature { LastSpell.Spontaneous: true });

    [NwnxFunc("Object", "GetLastSpellCastDomainLevel")]
    static void LastSpellDomainLevel(NwnxCall n) => n.Push(n.PopObject() is Creature { LastSpell: { } s } ? s.DomainLevel : 0);

    /// <summary>Whether the calling object's current spell was cast instantly (ActionCastSpell bInstantSpell).</summary>
    [NwnxFunc("Object", "GetLastSpellInstant")]
    static void LastSpellInstant(NwnxCall n) => n.Push(n.Thread != null && n.World.Get(n.Thread.Self)?.LastSpell?.Instant == true);

    /// <summary>
    /// One projectile override per creature: its spells (or one spell) fly by another path; type -1 removes it. With
    /// bPersist it travels in the creature's GFF, under the original's keys (nwnx_object).
    /// </summary>
    [NwnxFunc("Object", "OverrideSpellProjectileVFX")]
    static void OverrideProjectile(NwnxCall n)
    {
        var o = n.PopObject();
        int type = n.PopInt(), path = n.PopInt(), spell = n.PopInt();
        bool persist = n.PopInt() != 0;
        if (o is not Creature) return;
        if (type < 0)
        {
            foreach (var key in new[] { ProjectileType, ProjectilePath, ProjectileSpell }) NwnxPersistence.Delete(o, key);
            return;
        }
        NwnxPersistence.Set(o, ProjectileType, NwnxValue.Int(type), persist);
        NwnxPersistence.Set(o, ProjectilePath, NwnxValue.Int(path), persist);
        NwnxPersistence.Set(o, ProjectileSpell, NwnxValue.Int(spell), persist);
    }

    const string ProjectileType = "NWNX_Object!OSPVFX_TYPE", ProjectilePath = "NWNX_Object!OSPVFX_PATH", ProjectileSpell = "NWNX_Object!OSPVFX_SPELLID";

    /// <summary>The PROJECTILE_PATH_TYPE_* a creature's projectile for <paramref name="spell"/> takes instead, or -1.</summary>
    public static int ProjectilePathOverride(World world, GameObject caster, int spell)
    {
        if (caster.PluginValues == null && TemplateAccess.TemplateOf(caster)?.Has(NwnxPersistence.Field) != true) return -1;
        if (NwnxPersistence.Get(caster, ProjectileType) is not { Value: int type } || type != 7) return -1; // NWNX_OBJECT_SPELL_PROJECTILE_TYPE_USE_PATH
        int only = NwnxPersistence.Get(caster, ProjectileSpell) is { Value: int sp } ? sp : -1;
        int path = NwnxPersistence.Get(caster, ProjectilePath) is { Value: int p } ? p : -1;
        return only >= 0 && only != spell ? -1 : path;
    }

    [NwnxFunc("Object", "SetConversationPrivate")]
    static void SetConversationPrivate(NwnxCall n)
    {
        var o = n.PopObject();
        bool isPrivate = n.PopInt() != 0;
        if (o == null) return;
        if (isPrivate) n.State(o)[ConversationPrivateKey] = NwnxValue.Int(1);
        else n.State(o).Remove(ConversationPrivateKey);
    }

    const string ConversationPrivateKey = "object:conversationPrivate";

    /// <summary>Whether conversations an object starts are private by default (SetConversationPrivate).</summary>
    public static bool ConversationPrivate(World world, GameObject owner) =>
        NwnxRegistry.Of(world).TryGetObjectState(owner.Id, out var state) && state.ContainsKey(ConversationPrivateKey);

    // ---------------------------------------------------------------- UUIDs and export

    /// <summary>Gives the object this UUID; whoever had it gets a new one when next asked.</summary>
    [NwnxFunc("Object", "ForceAssignUUID")]
    static void ForceAssignUUID(NwnxCall n)
    {
        var o = n.PopObject();
        var uuid = n.PopString();
        if (o == null || uuid.Length == 0 || !PersistenceSystem.CanCarryUuid(o)) return;
        var sys = n.World.System<PersistenceSystem>();
        if (sys.ByUuid(uuid) is { } other && other != o) sys.RefreshUuid(other);
        sys.TryClaim(o, uuid);
    }

    /// <summary>
    /// Writes the object as a blueprint file (.uti, .utc, ...) into the server's "nwnx" folder (alias "NWNX", the only
    /// one accepted).
    /// </summary>
    [NwnxFunc("Object", "Export")]
    static void Export(NwnxCall n)
    {
        var o = n.PopObject();
        var name = n.PopString();
        var alias = n.PopString();
        if (o is not (Creature or Item or Placeable or Waypoint or Door or Store or Trigger)) return;
        // A resource name, into the NWNX folder only (the alias the server sets up); anything else is refused.
        if (name.Length is 0 or > 16 || !name.All(ch => ch is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_')) return;
        if (alias.Length > 0 && !alias.Equals("NWNX", StringComparison.OrdinalIgnoreCase)) return;
        // The file is only written; the original does not look resources up in that folder (nwnx_object).
        if (ObjectSerializer.ToBytes(n.World, o) is not { } data) return;
        string folder = Path.GetFullPath(Path.Combine(n.World.System<PersistenceSystem>().UserRoot, "nwnx"));
        string ext = ObjectSerializer.FileType(o).Trim().ToLowerInvariant();
        if (ext == "bic") ext = "utc";
        try
        {
            Directory.CreateDirectory(folder);
            string file = Path.GetFullPath(Path.Combine(folder, name.ToLowerInvariant() + "." + ext));
            if (!file.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return;
            File.WriteAllBytes(file, data);
        }
        catch (IOException e) { n.Registry.Log($"[nwnx] Object.Export: {e.Message}"); }
        catch (UnauthorizedAccessException e) { n.Registry.Log($"[nwnx] Object.Export: {e.Message}"); }
    }
}
