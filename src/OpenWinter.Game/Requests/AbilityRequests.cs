using System.Numerics;
using OpenWinter.Game.Combat;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Skills;
using OpenWinter.Game.Society;
using OpenWinter.Game.Spells;

namespace OpenWinter.Game.Requests;

/// <summary>
/// The action bar and the radial menu: what the player can use right now (active feats with uses left,
/// action modes and whether they are on, active skills, items with activatable properties), and the
/// requests that use them (<c>use-feat</c>, <c>use-skill</c>, <c>use-item</c>, <c>attack</c>). Everything
/// the client sends is checked here: the player must own the item, have the feat or skill, and the target
/// must be in their area.
/// <para>
/// Target types are spells.2da TargetType bits: 1 self, 2 creature, 4 ground, 8 item, 16 door, 32 placeable, 64 trigger.
/// </para>
/// </summary>
public static class AbilityRequests
{
    public const int TargetSelf = 1, TargetCreature = 2, TargetGround = 4, TargetItem = 8, TargetDoor = 16, TargetPlaceable = 32, TargetTrigger = 64;

    // ACTION_MODE_* (nwscript.nss).
    public const int ModeDetect = 0, ModeStealth = 1, ModeParry = 2, ModePowerAttack = 3, ModeImprovedPowerAttack = 4,
        ModeCounterspell = 5, ModeFlurry = 6, ModeRapidShot = 7, ModeExpertise = 8, ModeImprovedExpertise = 9,
        ModeDefensiveCast = 10, ModeDirtyFighting = 11;

    /// <summary>Combat modes: turning one on turns the others off.</summary>
    static readonly int[] CombatModes = { ModeParry, ModePowerAttack, ModeImprovedPowerAttack, ModeCounterspell, ModeFlurry,
        ModeRapidShot, ModeExpertise, ModeImprovedExpertise, ModeDefensiveCast, ModeDirtyFighting };

    /// <summary>The modes a player can toggle, what they need (a feat, a skill, spellcasting) and a fallback name.</summary>
    static readonly (int Mode, int Feat, int Skill, bool Caster, string Name)[] Modes =
    {
        (ModeDetect, -1, -1, false, "Detect Mode"),
        (ModeStealth, -1, SkillIds.Hide, false, "Stealth Mode"),
        (ModeParry, -1, SkillIds.Parry, false, "Parry"),
        (ModePowerAttack, CombatK.FeatPowerAttack, -1, false, "Power Attack"),
        (ModeImprovedPowerAttack, CombatK.FeatImprovedPowerAttack, -1, false, "Improved Power Attack"),
        (ModeFlurry, CombatK.FeatFlurryOfBlows, -1, false, "Flurry of Blows"),
        (ModeRapidShot, CombatK.FeatRapidShot, -1, false, "Rapid Shot"),
        (ModeExpertise, CombatK.FeatExpertise, -1, false, "Expertise"),
        (ModeImprovedExpertise, CombatK.FeatImprovedExpertise, -1, false, "Improved Expertise"),
        (ModeDefensiveCast, -1, -1, true, "Defensive Casting"),
        (ModeDirtyFighting, CombatK.FeatDirtyFighting, -1, false, "Dirty Fighting"),
    };

    /// <summary>Feats that turn a mode on and off instead of being used.</summary>
    static readonly Dictionary<int, int> ModeFeats = Modes.Where(m => m.Feat >= 0).ToDictionary(m => m.Feat, m => m.Mode);

    /// <summary>
    /// Skills used on something (the engine keys their behaviour by skills.2da row): the targets they take and
    /// whether using them is hostile. Hide/Search are the stealth and detect toggles (listed as modes).
    /// </summary>
    static readonly Dictionary<int, (int Targets, bool Hostile)> ActiveSkills = new()
    {
        [SkillIds.AnimalEmpathy] = (TargetCreature, false),
        [SkillIds.DisableTrap] = (TargetDoor | TargetPlaceable | TargetTrigger, false),
        [SkillIds.Heal] = (TargetSelf | TargetCreature, false),
        [SkillIds.OpenLock] = (TargetDoor | TargetPlaceable, false),
        [SkillIds.PickPocket] = (TargetCreature, true),
        [SkillIds.SetTrap] = (TargetSelf | TargetDoor | TargetPlaceable, false),
        [SkillIds.Taunt] = (TargetCreature, true),
        [SkillIds.Lore] = (TargetItem, false),
    };

    static readonly int[] TrapSubSkills = { 0, SkillIds.SubSkillExamineTrap, SkillIds.SubSkillFlagTrap, SkillIds.SubSkillRecoverTrap };

    // ------------------------------------------------------------------ requests

    /// <summary>
    /// What the player can use: {target?} adds facts about that object for its radial menu. The player is then
    /// kept up to date: an "abilities" notify follows whenever uses, modes or items change.
    /// </summary>
    [ClientRequest("abilities")]
    static void Abilities(Request r)
    {
        var feed = r.World.System<AbilitiesFeed>();
        feed.Subscribe(r.Player);
        var target = r.World.Get(r.Id("target"));
        r.World.Output?.Notify("abilities", r.Player, Describe(r.World, r.Player, target), r.Player);
        feed.Sent(r.Player, Describe(r.World, r.Player));
    }

    /// <summary>
    /// Cancel polymorph (the radial menu): ends the player's polymorph unless it was applied locked
    /// (EffectPolymorph's nLocked). It ends like a polymorph running out: the change-back visual plays at once, the
    /// effect (and its bonus hit points) stays listed and the player changes back half a second later (visual
    /// scene polymorphpc: 0.5 s after the wedge the form and its 35 hit points were still there, at 2.5 s gone).
    /// </summary>
    [ClientRequest("cancel-polymorph")]
    static void CancelPolymorph(Request r)
    {
        if (CancellablePolymorph(r.World, r.Player) is { } effect) r.World.System<EffectSystem>().CancelPolymorph(r.Player, effect);
    }

    /// <summary>The polymorph effect the player could cancel now: there is one, it isn't ending and it isn't locked.</summary>
    static Scripting.Effect? CancellablePolymorph(World w, Creature c)
    {
        var k = Scripting.EffectConstants.Of(w);
        if (c.PolymorphForm is not { Ending: false } form) return null;
        var effect = c.Effects.FirstOrDefault(e => e.UniqueId == form.EffectId && e.Type == k["EFFECT_TYPE_POLYMORPH"]);
        return effect == null || effect.Ints[1] != 0 ? null : effect;
    }

    /// <summary>Use a feat: {feat, sub (sub-radial spell or called-shot part), target (object id) or pos [x,y,z]}. Mode feats toggle their mode.</summary>
    [ClientRequest("use-feat")]
    static void UseFeat(Request r)
    {
        var w = r.World;
        var pc = r.Player;
        int feat = r.Int("feat", -1);
        if (pc.IsDead || !pc.Commandable || pc.Area == null) return;
        if (ModeFeats.TryGetValue(feat, out int mode))
        {
            SetMode(w, pc, mode, !pc.ActionModes.Contains(mode));
            return;
        }
        if (!IsActiveFeat(w, feat) || !CreatureRules.OwnsFeat(w, pc, feat)) return;
        if (!CreatureRules.HasFeat(w, pc, feat))
        {
            w.Output?.SendMessage(pc, $"You have no uses of {FeatName(w, feat)} left.");
            return;
        }
        int sub = r.Int("sub", 0);
        if (sub != 0 && !SubOptions(w, feat).Any(s => s.Id == sub)) return; // only the feat's own sub-radial choices
        if (!Aim(r, out var target, out var location)) return;
        ActionQueues.EnqueuePlayerTask(w, pc, new Scripting.Functions.UseFeatAction { Feat = feat, SubFeat = sub, Target = target, TargetLocation = location });
        w.System<AbilitiesFeed>().Soon(pc);
    }

    /// <summary>Use a skill: {skill, sub (disable trap: 100 flag, 101 recover, 102 examine), target, item (healer's kit, tools, trap kit)}.</summary>
    [ClientRequest("use-skill")]
    static void UseSkill(Request r)
    {
        var w = r.World;
        var pc = r.Player;
        int skill = r.Int("skill", -1);
        if (pc.IsDead || !pc.Commandable || pc.Area == null) return;
        // Hide/Move Silently and Search/Spot/Listen are the stealth and detect toggles.
        if (skill is SkillIds.Hide or SkillIds.MoveSilently) { SetMode(w, pc, ModeStealth, !pc.StealthMode); return; }
        if (skill is SkillIds.Search or SkillIds.Spot or SkillIds.Listen) { SetMode(w, pc, ModeDetect, !pc.DetectMode); return; }
        if (!ActiveSkills.TryGetValue(skill, out var info) || !CreatureRules.HasSkill(w, pc, skill)) return;
        int sub = r.Int("sub", 0);
        if (skill == SkillIds.DisableTrap ? !TrapSubSkills.Contains(sub) : sub != 0) return;
        var item = r.Obj<Item>("item");
        if (item != null && !r.Owns(item)) return;
        var target = w.Get(r.Id("target"));
        if (target == null && (info.Targets & TargetSelf) != 0) target = pc;
        if (target == null) return;
        if (target is Item own)
        {
            // Lore on a carried item identifies it on the spot.
            if (skill == SkillIds.Lore && r.Owns(own)) w.System<SkillSystem>().TryIdentify(pc, own);
            return;
        }
        if (target.Area != pc.Area || (target == pc && (info.Targets & TargetSelf) == 0) || !Accepts(info.Targets, target, pc)) return;
        if (skill == SkillIds.SetTrap)
        {
            item ??= FirstWith(w, pc, SkillIds.IpTrap);
            if (item == null) { w.Output?.SendMessage(pc, "You need a trap kit to set a trap."); return; }
        }
        if (skill == SkillIds.Heal) item ??= FirstWith(w, pc, SkillIds.IpHealersKit);
        if (skill == SkillIds.OpenLock) item ??= FirstWith(w, pc, SkillIds.IpThievesTools);
        ActionQueues.EnqueuePlayerTask(w, pc, new UseSkillAction
        {
            Skill = skill, SubSkill = sub, ItemId = item?.Id ?? GameObject.InvalidId, Target = target.Id, Range = UseSkillAction.RangeFor(skill),
        });
    }

    /// <summary>
    /// Use one of a carried item's properties: {item, prop (index in the item's properties), target or pos}.
    /// Healer's kits, thieves' tools and trap kits walk to the target and use their skill.
    /// </summary>
    [ClientRequest("use-item")]
    static void UseItem(Request r)
    {
        var w = r.World;
        var pc = r.Player;
        if (pc.IsDead || !pc.Commandable || pc.Area == null) return;
        if (r.Obj<Item>("item") is not { } item || !r.Owns(item) || item.IsDestroyed) return;
        int index = r.Int("prop", -1);
        if (index < 0 || index >= item.Properties.Count) return;
        var prop = item.Properties[index];
        if (!prop.Valid || !prop.Useable || !IsActivatable(prop)) return;
        var skills = w.System<SkillSystem>();
        if (!CanActivateFromUi(skills, pc, item)) { w.Output?.SendMessage(pc, "You cannot use that item."); return; }
        if (prop.Type == ItemRules.PropCastSpell && !w.Inventory.CanUseItemProperty(item, prop))
        {
            w.Output?.SendMessage(pc, "That item has no uses left.");
            return;
        }
        // Kits and tools are skill uses: walk over, animate, roll.
        int skill = prop.Type switch
        {
            SkillIds.IpHealersKit => SkillIds.Heal,
            SkillIds.IpThievesTools => SkillIds.OpenLock,
            SkillIds.IpTrap => SkillIds.SetTrap,
            _ => -1,
        };
        if (skill >= 0)
        {
            // A trap kit aimed at the ground sets the trap there.
            if (skill == SkillIds.SetTrap && w.Get(r.Id("target")) == null && r.Element("pos") != null)
            {
                ActionQueues.EnqueuePlayerTask(w, pc, new UseSkillAction
                {
                    Skill = skill, ItemId = item.Id, Target = pc.Id, Spot = new Location(pc.Area.Id, r.Vec("pos"), 0), Range = UseSkillAction.RangeFor(skill),
                });
                return;
            }
            var on = w.Get(r.Id("target")) ?? pc;
            if (on.Area != pc.Area || !Accepts(ActiveSkills[skill].Targets, on, pc) || on == pc && (ActiveSkills[skill].Targets & TargetSelf) == 0) return;
            ActionQueues.EnqueuePlayerTask(w, pc, new UseSkillAction { Skill = skill, ItemId = item.Id, Target = on.Id, Range = UseSkillAction.RangeFor(skill) });
            return;
        }
        uint target;
        Location location;
        if (ItemRules.IsPotion(item.BaseItem)) { target = pc.Id; location = Location.Invalid; } // potions are drunk, never thrown
        else if (!Aim(r, out target, out location)) return;
        // Every UI entry point submits a task to the same player queue.
        ActionQueues.EnqueuePlayerTask(w, pc, new UseItemAction { ItemId = item.Id, Property = prop, Target = target, TargetLocation = location });
        w.System<AbilitiesFeed>().Soon(pc);
    }

    /// <summary>
    /// Name and icon of one of the player's items (an item dragged from the inventory onto the action bar):
    /// {item} → "item-info" {item, name, icon, stack, equippable, equipped}.
    /// </summary>
    [ClientRequest("item-info")]
    static void ItemInfo(Request r)
    {
        if (r.Obj<Item>("item") is not { } item || !r.Owns(item) || item.IsDestroyed) return;
        var inv = r.World.Inventory;
        r.World.Output?.Notify("item-info", r.Player, new
        {
            item = item.Id, name = item.Name.Resolve(r.World.Resources.Talk), icon = inv.Icon(item, r.Player), stack = item.StackSize,
            equippable = inv.Rules.EquipableSlots(item.BaseItem) != 0, equipped = r.Player.Equipment.ContainsValue(item),
            slot = Inventory.InventorySystem.SlotOf(r.Player, item),
            equipSlots = inv.Rules.EquipableSlots(item.BaseItem),
        }, r.Player);
    }

    /// <summary>Attack an object in the player's area: {target}.</summary>
    [ClientRequest("attack")]
    static void Attack(Request r)
    {
        if (r.World.Get(r.Id("target")) is not { } t || t == r.Player || t.Area != r.Player.Area || r.Player.IsDead || !r.Player.Commandable) return;
        if (t is not (Creature or Door or Placeable)) return;
        if (r.World.RefusesPvp(r.Player, t)) return; // e.g. her own summon (associateradial)
        ActionQueues.Clear(r.World, r.Player);
        // On the action queue, so the player's next order (or a script's ClearAllActions) ends the fight.
        r.Player.Actions.Add(new AttackAction { Target = t.Id });
        r.World.System<CombatSystem>().Attack(r.Player, t);
    }

    /// <summary>
    /// Turns an action mode on or off for a player (the radial's and action bar's mode toggles): only modes the
    /// creature qualifies for; combat modes exclude each other. Clients are told with an "action-mode" notify.
    /// </summary>
    public static bool SetMode(World w, Creature c, int mode, bool on)
    {
        CreatureRules.EnsureInit(w, c);
        if (mode == ModeDetect && CreatureRules.HasFeat(w, c, SkillIds.FeatKeenSense, ignoreUses: true))
            return false;
        if (mode is ModeStealth or ModeDetect) return w.System<SkillSystem>().SetActionMode(c, mode, on);
        var def = Modes.FirstOrDefault(m => m.Mode == mode && m.Name != null);
        if (def.Name == null) return false;
        if (on && !Qualifies(w, c, def)) return false;
        if (on == c.ActionModes.Contains(mode)) return true;
        ApplyCombatMode(w, c, mode, on);
        return true;
    }

    /// <summary>Shared mutation/feedback after request validation, or for the script setter.</summary>
    internal static void ApplyCombatMode(World w, Creature c, int mode, bool on)
    {
        var cancelled = on && CombatModes.Contains(mode)
            ? CombatModes.Where(m => m != mode && c.ActionModes.Remove(m)).ToArray() : [];
        bool changed = on ? c.ActionModes.Add(mode) : c.ActionModes.Remove(mode);
        w.MarkChanged(c);
        // Native announces Defensive Casting first when it replaces another combat mode.
        if (changed && on && mode == ModeDefensiveCast) NotifyMode(w, c, mode, true);
        foreach (int old in cancelled) NotifyMode(w, c, old, false);
        if (changed && (!on || mode != ModeDefensiveCast)) NotifyMode(w, c, mode, on);
        w.System<AbilitiesFeed>().Soon(c);
    }

    /// <summary>Baseline mode feedback, including script changes and mutually exclusive modes being cancelled.</summary>
    public static void NotifyMode(World w, Creature c, int mode, bool on)
    {
        int strref = mode switch
        {
            ModeDetect => on ? 58275 : 58276,
            ModeStealth => on ? 58277 : 58278,
            ModeParry => on ? 58279 : 58280,
            ModePowerAttack => on ? 58281 : 58282,
            ModeImprovedPowerAttack => on ? 58283 : 58284,
            ModeFlurry => on ? 63811 : 63812,
            ModeRapidShot => on ? 63809 : 63810,
            ModeExpertise => on ? 1758 : 1759,
            ModeImprovedExpertise => on ? 1760 : 1761,
            ModeDefensiveCast => on ? 1766 : 1767,
            ModeDirtyFighting => on ? 3873 : 3876,
            _ => -1,
        };
        w.Output?.Notify("action-mode", c, new
        {
            creature = c.Id, mode, on, name = ModeName(w, mode),
            feedback = strref < 0 ? "" : w.Resources.TlkString((uint)strref),
        });
    }

    static string ModeName(World w, int mode)
    {
        var m = Modes.FirstOrDefault(m => m.Mode == mode);
        if (m.Name == null) return "";
        if (m.Feat >= 0) return FeatName(w, m.Feat);
        if (mode == ModeParry) return Tlk(w, w.Resources.TwoDA("skills").GetInt(SkillIds.Parry, "Name", -1), m.Name);
        return m.Name;
    }

    // ------------------------------------------------------------------ what the player can use

    /// <summary>The "abilities" notify: feats, modes, skills and usable items, with names, icons and target types.</summary>
    public static object Describe(World w, Creature c, GameObject? target = null)
    {
        CreatureRules.EnsureInit(w, c);
        return new
        {
            feats = Feats(w, c),
            modes = ModeList(w, c),
            skills = Skills(w, c),
            items = Items(w, c),
            target = target != null && (GameServer.Perceives(w, c, target) || target is Item i && w.Inventory.Owner(i) == c) ? DescribeTarget(w, c, target) : null,
        };
    }

    static List<object> Feats(World w, Creature c)
    {
        var feats = w.Resources.TwoDA("feat");
        var owned = CreatureRules.AllFeats(w, c).ToHashSet();
        // A successor (Barbarian Rage 2..7, Bard Song 2..20) is shown as its first feat, which counts its uses.
        var successors = owned.Select(f => feats.GetInt(f, "SUCCESSOR", -1)).Where(f => f >= 0).ToHashSet();
        var list = new List<object>();
        foreach (var feat in owned.Order())
        {
            if (successors.Contains(feat) || ModeFeats.ContainsKey(feat) || !IsActiveFeat(w, feat)) continue;
            int max = CreatureRules.MaxFeatUses(w, c, feat);
            int left = CreatureRules.RemainingFeatUses(w, c, feat);
            int spell = feats.GetInt(feat, "SPELLID", -1);
            int master = feats.GetInt(feat, "MASTERFEAT", -1);
            var masters = w.Resources.TwoDA("masterfeats");
            list.Add(new
            {
                feat,
                name = FeatName(w, feat),
                icon = Lower(feats.Get(feat, "ICON")),
                uses = max < 0 ? -1 : left,
                max,
                targetType = FeatTargetType(w, feat),
                hostile = feats.GetInt(feat, "HostileFeat") == 1 || (spell >= 0 && SpellTable.Hostile(w, spell)),
                spell,
                master,
                masterName = master >= 0 ? Tlk(w, masters.GetInt(master, "STRREF", -1)) : "",
                masterIcon = master >= 0 ? Lower(masters.Get(master, "ICON")) : "",
                subs = SubOptions(w, feat).Select(s => new { id = s.Id, name = s.Name, icon = s.Icon, targetType = s.TargetType }).ToArray(),
            });
        }
        return list;
    }

    static List<object> ModeList(World w, Creature c)
    {
        var feats = w.Resources.TwoDA("feat");
        var skills = w.Resources.TwoDA("skills");
        var list = new List<object>();
        foreach (var m in Modes)
        {
            if (!Qualifies(w, c, m)) continue;
            string name = ModeName(w, m.Mode), icon = "";
            if (m.Feat >= 0) icon = Lower(feats.Get(m.Feat, "ICON"));
            // Stealth mode shows Move Silently's icon (isk_movsilent), as the original's radial and quickbar do (visual
            // scene rulesfollowpc), not Hide's.
            else if (m.Mode is ModeParry or ModeStealth or ModeDetect)
                icon = Lower(skills.Get(m.Mode == ModeParry ? SkillIds.Parry : m.Mode == ModeStealth ? SkillIds.MoveSilently : SkillIds.Search, "Icon"));
            bool on = m.Mode switch { ModeStealth => c.StealthMode, ModeDetect => c.DetectMode, _ => c.ActionModes.Contains(m.Mode) };
            list.Add(new { mode = m.Mode, name, icon, on, feat = m.Feat, combat = CombatModes.Contains(m.Mode) });
        }
        return list;
    }

    static List<object> Skills(World w, Creature c)
    {
        var skills = w.Resources.TwoDA("skills");
        var list = new List<object>();
        foreach (var (skill, info) in ActiveSkills.OrderBy(kv => kv.Key))
        {
            if (skill >= skills.RowCount || !CreatureRules.HasSkill(w, c, skill)) continue;
            list.Add(new
            {
                skill,
                name = Tlk(w, skills.GetInt(skill, "Name", -1), skills.Get(skill, "Label")),
                icon = Lower(skills.Get(skill, "Icon")),
                rank = CreatureRules.SkillRank(w, c, skill),
                targetType = info.Targets,
                hostile = info.Hostile,
                subs = skill == SkillIds.DisableTrap
                    ? new object[]
                    {
                        new { id = 0, name = "Disarm Trap", icon = "ir_dis_trap" }, new { id = SkillIds.SubSkillExamineTrap, name = "Examine Trap", icon = "ir_examine" },
                        new { id = SkillIds.SubSkillFlagTrap, name = "Flag Trap", icon = "ir_flag_trap" }, new { id = SkillIds.SubSkillRecoverTrap, name = "Recover Trap", icon = "ir_rec_trap" },
                    }
                    : Array.Empty<object>(),
            });
        }
        return list;
    }

    static List<object> Items(World w, Creature c)
    {
        var inv = w.Inventory;
        var tlk = w.Resources.Talk;
        var ipSpells = w.Resources.TwoDA("iprp_spells");
        var skills = w.System<SkillSystem>();
        var list = new List<object>();
        foreach (var item in inv.AllItems(c))
        {
            if (item.IsDestroyed) continue;
            List<object>? props = null;
            for (int i = 0; i < item.Properties.Count; i++)
            {
                var p = item.Properties[i];
                if (!p.Valid || !p.Useable || !IsActivatable(p)) continue;
                int spell = p.Type == ItemRules.PropCastSpell ? ipSpells.GetInt(p.SubType, "SpellIndex", -1) : -1;
                string name = p.Type switch
                {
                    ItemRules.PropCastSpell => item.Identified ? Tlk(w, ipSpells.GetInt(p.SubType, "Name", -1), "Use") : "Use",
                    SkillIds.IpHealersKit => "Heal",
                    SkillIds.IpThievesTools => "Open Lock",
                    _ => "Set Trap",
                };
                (props ??= new()).Add(new
                {
                    index = i,
                    key = QuickbarItemState.PowerKey(p),
                    name,
                    spell,
                    icon = spell >= 0 ? Lower(ipSpells.Get(p.SubType, "Icon")) : "",
                    targetType = p.Type switch
                    {
                        ItemRules.PropCastSpell when ItemRules.IsPotion(item.BaseItem) || spell >= 0 && SpellTable.IsPersonal(w, spell) => TargetSelf,
                        ItemRules.PropCastSpell => spell >= 0 ? SpellTargetType(w, spell) : TargetSelf,
                        _ => ActiveSkills[p.Type switch { SkillIds.IpHealersKit => SkillIds.Heal, SkillIds.IpThievesTools => SkillIds.OpenLock, _ => SkillIds.SetTrap }].Targets,
                    },
                    hostile = spell >= 0 && SpellTable.Hostile(w, spell),
                    uses = UsesLeft(item, p),
                    ready = p.Type != ItemRules.PropCastSpell || inv.CanUseItemProperty(item, p),
                });
            }
            if (props == null) continue;
            list.Add(new
            {
                item = item.Id,
                name = item.Name.Resolve(tlk),
                icon = inv.Icon(item, c),
                key = QuickbarItemState.Key(item),
                stack = item.StackSize,
                charges = item.Charges,
                identified = item.Identified,
                equipped = c.Equipment.ContainsValue(item),
                slot = Inventory.InventorySystem.SlotOf(c, item),
                usable = CanActivateFromUi(skills, c, item),
                props,
            });
        }
        return list;
    }

    /// <summary>What the radial menu needs to know about an object to offer the right actions.</summary>
    public static object DescribeTarget(World w, Creature pc, GameObject o)
    {
        var trap = o as ITrappable;
        var lockable = o as ILockable;
        bool trapKnown = trap is { IsTrapped: true } && (trap.Trap.DetectedBy.Contains(pc.Id) || trap.Trap.Flagged);
        int rep = o is Creature ? w.Factions.ReputationOf(o, pc) : 50;
        var cr = o as Creature;
        var groundItem = o as Item;
        int equipmentSlots = groundItem == null ? 0 : w.Inventory.Rules.EquipableSlots(groundItem.BaseItem);
        return new
        {
            id = o.Id,
            self = o == pc,
            portrait = Portraits.ResRef(w, o),
            itemIcon = groundItem == null ? Array.Empty<string>() : w.Inventory.Icon(groundItem, pc),
            itemBase = groundItem?.BaseItem ?? -1,
            itemStack = groundItem?.StackSize ?? 0,
            itemWidth = groundItem == null ? 0 : w.Inventory.Footprint(groundItem).W,
            itemHeight = groundItem == null ? 0 : w.Inventory.Footprint(groundItem).H,
            equipGround = groundItem != null && equipmentSlots != 0,
            equipOffhand = groundItem != null && (equipmentSlots & (EquipSlot.RightHand | EquipSlot.LeftHand)) == (EquipSlot.RightHand | EquipSlot.LeftHand)
                && !w.Inventory.Rules.IsTwoHanded(groundItem.BaseItem, pc.Size),
            locked = lockable?.IsLocked == true,
            lockable = lockable?.Lock.Lockable == true,
            keyRequired = lockable?.KeyRequiredFlag == true,
            trapped = trapKnown,
            trapRecoverable = trapKnown && trap!.Trap.Recoverable,
            trapDisarmable = trapKnown && trap!.Trap.Disarmable,
            hasInventory = o is Placeable { HasInventory: true },
            dead = cr?.IsDead == true,
            hostile = cr != null && rep <= FactionTable.HostileTo,
            injured = cr != null && cr.CurrentHitPoints < cr.MaxHitPoints,
            animal = cr != null && cr.Race is SkillIds.RaceAnimal or SkillIds.RaceBeast or SkillIds.RaceMagicalBeast,
            pc = cr?.IsPC == true,
            associate = cr != null && cr != pc && cr.Master != GameObject.InvalidId && AssociateService.CommandableBy(w, pc, cr),
            // ASSOCIATE_TYPE_* of the player's own associate: its radial's NW ring differs by type (docs/ASSOCIATE_COMMANDS.md).
            associateType = cr != null && cr != pc && cr.Master != GameObject.InvalidId && AssociateService.CommandableBy(w, pc, cr) ? cr.AssociateType : AssociateService.None,
            // Another player: the party ring's entries (docs/PARTY.md).
            party = cr is { IsPC: true } other && other != pc ? PartyService.Of(w).Facts(pc, other) : null,
            plot = o.Plot,
            // The player's own radial menu offers Cancel Polymorph.
            // The player's own radial and her familiar's NW ring offer Possess Familiar (associatesubmenus).
            canPossessFamiliar = AssociateService.Get(w, pc, AssociateService.Familiar, 1) is { } familiar && (o == pc || o == familiar)
                && Players.FamiliarPossession.CanPossess(w, pc, familiar),
            familiar = AssociateService.Get(w, pc, AssociateService.Familiar, 1)?.Id ?? GameObject.InvalidId,
            canUnpossessFamiliar = o == pc && pc.Player?.FamiliarMaster != null,
            canCancelPolymorph = o == pc && CancellablePolymorph(w, pc) != null,
        };
    }

    // ------------------------------------------------------------------ rules

    /// <summary>A feat used from the radial: it casts a spell (feat.2da SPELLID) or is an attack feat (knockdown, smite, ...).</summary>
    public static bool IsActiveFeat(World w, int feat)
    {
        if (feat < 0) return false;
        if (CombatSystem.IsAttackFeat(feat)) return true;
        var feats = w.Resources.TwoDA("feat");
        return feat < feats.RowCount && feats.GetInt(feat, "SPELLID", -1) >= 0;
    }

    static bool Qualifies(World w, Creature c, (int Mode, int Feat, int Skill, bool Caster, string Name) m)
    {
        if (m.Feat >= 0 && !CreatureRules.OwnsFeat(w, c, m.Feat)) return false;
        if (m.Skill >= 0 && !CreatureRules.HasSkill(w, c, m.Skill)) return false;
        if (m.Caster && !c.Classes.Any(cl => cl.Level > 0 && CreatureRules.IsSpellCaster(w, cl.Class))) return false;
        return true;
    }

    // Activation and published usability use the same equipment and actual-property rules.
    static bool CanActivateFromUi(SkillSystem skills, Creature c, Item item) =>
        skills.CanActivateSpellItem(c, item);

    /// <summary>Cast-spell properties and the skill items (healer's kits, thieves' tools, trap kits).</summary>
    internal static bool IsActivatable(Scripting.ItemProperty p) =>
        p.Type is ItemRules.PropCastSpell or SkillIds.IpHealersKit or SkillIds.IpThievesTools or SkillIds.IpTrap;

    /// <summary>Activations left: uses today, whole uses of the charges, the stack for single-use items; -1 unlimited.</summary>
    static int UsesLeft(Item item, Scripting.ItemProperty p)
    {
        if (p.Type != ItemRules.PropCastSpell) return p.Type == SkillIds.IpHealersKit || p.Type == SkillIds.IpTrap || p.Type == SkillIds.IpThievesTools ? item.StackSize : -1;
        int perDay = InventorySystem.MaxUsesPerDay(p);
        if (perDay > 0) return Math.Min(p.UsesPerDay, perDay);
        if (p.CostTable == 3 && p.CostTableValue == 1) return item.StackSize; // single use
        if (p.CostTable == 3 && p.CostTableValue is >= 2 and <= 6) return item.Charges / (7 - p.CostTableValue);
        return -1;
    }

    /// <summary>The choices a feat offers on its sub-ring: its spell's SubRadSpell1..5 (wild shapes), or called shot's leg and arm.</summary>
    public static List<(int Id, string Name, string Icon, int TargetType)> SubOptions(World w, int feat)
    {
        var list = new List<(int, string, string, int)>();
        if (feat == CombatK.FeatCalledShot)
        {
            list.Add((CombatK.SpecialCalledShotLeg, "Leg", "", TargetCreature));
            list.Add((CombatK.SpecialCalledShotArm, "Arm", "", TargetCreature));
            return list;
        }
        var spells = SpellTable.Table(w);
        int spell = w.Resources.TwoDA("feat").GetInt(feat, "SPELLID", -1);
        if (spell < 0) return list;
        for (int i = 1; i <= 5; i++)
        {
            int sub = spells.GetInt(spell, "SubRadSpell" + i, -1);
            if (sub <= 0 || !SpellTable.Exists(w, sub)) continue;
            list.Add((sub, Tlk(w, spells.GetInt(sub, "Name", -1), spells.Get(sub, "Label")), Lower(spells.Get(sub, "IconResRef")), SpellTargetType(w, sub)));
        }
        return list;
    }

    static int FeatTargetType(World w, int feat)
    {
        var feats = w.Resources.TwoDA("feat");
        if (CombatSystem.IsAttackFeat(feat)) return TargetCreature;
        if (feats.GetInt(feat, "TARGETSELF") == 1) return TargetSelf;
        int spell = feats.GetInt(feat, "SPELLID", -1);
        return spell >= 0 ? SpellTargetType(w, spell) : TargetSelf;
    }

    /// <summary>spells.2da TargetType ("0x09"): which kinds of thing the spell can be aimed at.</summary>
    public static int SpellTargetType(World w, int spell)
    {
        var raw = SpellTable.Table(w).Get(spell, "TargetType");
        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) raw = raw[2..];
        return int.TryParse(raw, System.Globalization.NumberStyles.HexNumber, null, out int v) ? v : TargetSelf;
    }

    /// <summary>Does an object match target-type bits (the player counts as self, other creatures as creatures)?</summary>
    static bool Accepts(int types, GameObject o, Creature pc) => o switch
    {
        _ when o == pc => (types & (TargetSelf | TargetCreature)) != 0,
        Creature => (types & TargetCreature) != 0,
        Door => (types & TargetDoor) != 0,
        Placeable => (types & TargetPlaceable) != 0,
        Trigger => (types & TargetTrigger) != 0,
        Item => (types & TargetItem) != 0,
        _ => false,
    };

    /// <summary>The request's target: an object in the player's area (or a carried item), a point in the area, or the player.</summary>
    static bool Aim(Request r, out uint target, out Location location)
    {
        target = GameObject.InvalidId;
        location = Location.Invalid;
        var o = r.World.Get(r.Id("target"));
        if (o != null)
        {
            bool reachable = o is Item item ? item.Area == r.Player.Area && item.Area != null || r.Owns(item) : o.Area == r.Player.Area;
            if (!reachable) return false;
            target = o.Id;
            return true;
        }
        if (r.Element("pos") != null)
        {
            location = new Location(r.Player.Area!.Id, r.Vec("pos"), 0);
            return true;
        }
        target = r.Player.Id;
        return true;
    }

    static Item? FirstWith(World w, Creature c, int propertyType) =>
        w.Inventory.AllItems(c).FirstOrDefault(i => !i.IsDestroyed && i.Properties.Any(p => p.Valid && p.Type == propertyType));

    static string FeatName(World w, int feat)
    {
        var feats = w.Resources.TwoDA("feat");
        return Tlk(w, feats.GetInt(feat, "FEAT", -1), feats.Get(feat, "LABEL"));
    }

    static string Tlk(World w, int strref, string fallback = "")
    {
        if (strref < 0) return fallback;
        var s = w.Resources.TlkString(unchecked((uint)strref));
        return s.Length > 0 ? s : fallback;
    }

    static string Lower(string s) => s.ToLowerInvariant();
}

/// <summary>
/// Keeps players who asked for their abilities (the action bar, the radial menu) up to date: once a second,
/// sends a fresh "abilities" notify when feat uses, modes, skills or usable items changed, and a "spellbook"
/// notify when spell uses changed (so the bar's spell counts follow casting and resting).
/// </summary>
public sealed class AbilitiesFeed : IWorldSystem
{
    const float Interval = 1f;
    readonly World _world;
    readonly Dictionary<uint, string> _sent = new();
    readonly Dictionary<uint, string> _spells = new();
    float _timer;

    public AbilitiesFeed(World world) { _world = world; }

    public int Order => 101;

    public void Subscribe(Creature pc) => _sent.TryAdd(pc.Id, "");

    /// <summary>Records what the player was just sent, so the next check only sends changes.</summary>
    public void Sent(Creature pc, object data) => _sent[pc.Id] = System.Text.Json.JsonSerializer.Serialize(data);

    /// <summary>Keep the delivered book as the baseline, including changes before the first periodic poll.</summary>
    public void SpellbookSent(Creature pc, object book)
    {
        Subscribe(pc);
        _spells[pc.Id] = System.Text.Json.JsonSerializer.Serialize(book);
    }

    /// <summary>Check again shortly (after a use or a mode change).</summary>
    public void Soon(Creature pc) { if (_sent.ContainsKey(pc.Id)) _timer = Math.Min(_timer, 0.3f); }

    public void Step(float dt)
    {
        _timer -= dt;
        if (_timer > 0 || _sent.Count == 0) return;
        _timer = Interval;
        if (_world.Output == null) return;
        foreach (var id in _sent.Keys.ToList())
        {
            if (_world.Get<Creature>(id) is not { IsPC: true } pc || pc.IsDestroyed) { _sent.Remove(id); _spells.Remove(id); continue; }
            var data = AbilityRequests.Describe(_world, pc);
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            if (_sent[id] != json)
            {
                _sent[id] = json;
                _world.Output.Notify("abilities", pc, data, pc);
            }
            // Explicit spellbook replies record their delivered payload; an abilities-only subscriber
            // still waits for its first book request before relying on this baseline.
            var book = SpellbookRequests.Describe(_world, pc);
            var bookJson = System.Text.Json.JsonSerializer.Serialize(book);
            if (_spells.TryGetValue(id, out var lastBook) && lastBook != bookJson) _world.Output.Notify("spellbook", pc, book, pc);
            _spells[id] = bookJson;
        }
    }
}
