using System.Text.Json.Nodes;
using OpenWinter.Net.Nwn.Messages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAppearanceMessages;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>Feats, modes, skills and items: the "abilities" and "action-mode" notifies and the requests that use them.</summary>
public sealed partial class NwnAbilities
{
    const int TargetSelf = 1, TargetCreature = 2, TargetDoor = 16, TargetPlaceable = 32, TargetTrigger = 64, TargetItem = 8;

    // ACTION_MODE_* (nwscript.nss).
    const int ModeDetect = 0, ModeStealth = 1, ModeParry = 2, ModeCounterspell = 5, ModeDefensiveCast = 10;

    // SKILL_* (skills.2da rows) the radial uses on something, or that are the stealth and detect modes.
    const int SkillAnimalEmpathy = 0, SkillDisableTrap = 2, SkillHeal = 4, SkillHide = 5, SkillListen = 6, SkillLore = 7,
        SkillMoveSilently = 8, SkillOpenLock = 9, SkillParry = 10, SkillPickPocket = 13, SkillSearch = 14, SkillSetTrap = 15,
        SkillSpot = 17, SkillTaunt = 18;

    // ITEM_PROPERTY_*: cast spell, thieves' tools, trap, healer's kit.
    const int PropCastSpell = 15, PropThievesTools = 55, PropTrap = 70, PropHealersKit = 80;

    /// <summary>Modes the player can toggle: the feat or skill they need (-1 none), whether a caster needs it, and a fallback name.</summary>
    static readonly (int Mode, int Feat, int Skill, bool Caster, string Name)[] Modes =
    [
        (ModeDetect, -1, -1, false, "Detect Mode"),
        (ModeStealth, -1, SkillHide, false, "Stealth Mode"),
        (ModeParry, -1, SkillParry, false, "Parry"),
        (3, 28, -1, false, "Power Attack"),
        (4, 19, -1, false, "Improved Power Attack"),
        (6, 204, -1, false, "Flurry of Blows"),
        (7, 30, -1, false, "Rapid Shot"),
        (8, 389, -1, false, "Expertise"),
        (9, 390, -1, false, "Improved Expertise"),
        (ModeDefensiveCast, -1, -1, true, "Defensive Casting"),
        (11, 425, -1, false, "Dirty Fighting"),
    ];

    static readonly int[] CombatModes = [2, 3, 4, 5, 6, 7, 8, 9, 10, 11];

    /// <summary>Feats that are special attacks on a target (knockdown, disarm, called shot, sap, stunning fist, smites, quivering palm).</summary>
    static readonly HashSet<int> AttackFeats = [23, 17, 9, 16, 5, 31, 39, 301, 472, 296];

    /// <summary>Skills used on something: the targets they take and whether using them is hostile.</summary>
    static readonly SortedDictionary<int, (int Targets, bool Hostile)> ActiveSkills = new()
    {
        [SkillAnimalEmpathy] = (TargetCreature, false),
        [SkillDisableTrap] = (TargetDoor | TargetPlaceable | TargetTrigger, false),
        [SkillHeal] = (TargetSelf | TargetCreature, false),
        [SkillOpenLock] = (TargetDoor | TargetPlaceable, false),
        [SkillPickPocket] = (TargetCreature, true),
        [SkillSetTrap] = (TargetSelf | TargetDoor | TargetPlaceable, false),
        [SkillTaunt] = (TargetCreature, true),
        [SkillLore] = (TargetItem, false),
    };

    // -- modes --------------------------------------------------------------

    readonly HashSet<int> _modesOn = [];

    bool ModeOn(int mode) => _modesOn.Contains(mode);

    /// <summary>
    /// The player state's bit for each ACTION_MODE_*: detect 0x1, stealth 0x2, parry 0x4, power attack 0x8, flurry
    /// of blows 0x20, expertise 0x2000, improved expertise 0x4000 and defensive casting 0x8000 as measured;
    /// improved power attack and rapid shot are taken to sit between them, dirty fighting after defensive casting.
    /// </summary>
    static readonly Dictionary<int, uint> ModeBits = new()
    {
        [ModeDetect] = 0x1, [ModeStealth] = 0x2, [ModeParry] = 0x4, [3] = 0x8, [4] = 0x10, [6] = 0x20, [7] = 0x40,
        [8] = 0x2000, [9] = 0x4000, [ModeDefensiveCast] = 0x8000, [11] = 0x10000,
    };

    /// <summary>The modes on in the player state; each change is announced as an "action-mode" notify.</summary>
    void State(uint state)
    {
        bool first = !_stateKnown;
        (_state, _stateKnown) = (state, true);
        foreach (var (m, bit) in ModeBits)
        {
            bool on = (state & bit) != 0;
            if (on == _modesOn.Contains(m)) continue;
            if (on) _modesOn.Add(m); else _modesOn.Remove(m);
            if (!first || on)
                Notify("action-mode", new JsonObject { ["creature"] = _player, ["mode"] = m, ["on"] = on, ["name"] = ModeName(m), ["feedback"] = "" });
        }
    }

    void ToggleMode(int mode)
    {
        if (mode > (int)InputMessages.ToggleMode.DefensiveStance) return;
        send(new InputMessages.InputToggleMode { Mode = (InputMessages.ToggleMode)mode });
    }

    string ModeName(int mode)
    {
        var m = Modes.FirstOrDefault(x => x.Mode == mode);
        if (m.Name == null) return mode == ModeCounterspell ? "Counterspell" : "";
        if (m.Feat >= 0) return FeatName(m.Feat);
        if (mode == ModeParry) return Tlk("skills", SkillParry, "Name", m.Name);
        return m.Name;
    }

    bool SkillUsable(int skill) => skill is >= 0 and < 32 && (_skillsUsable & (1u << skill)) != 0;

    bool Qualifies((int Mode, int Feat, int Skill, bool Caster, string Name) m) =>
        (m.Feat < 0 || _feats.ContainsKey(m.Feat)) && (m.Skill < 0 || SkillUsable(m.Skill))
        && (!m.Caster || _classes.Any(c => IsCaster(c.Class)));

    // -- the "abilities" notify ---------------------------------------------

    /// <summary>The "abilities" notify; with a target, what the radial menu needs to know about it.</summary>
    void SendAbilities(uint target = GameObjUpdateMessages.InvalidObjectId)
    {
        if (_player == 0 || !_abilitiesWanted) return;
        Notify("abilities", new JsonObject
        {
            ["feats"] = FeatList(), ["modes"] = ModeList(), ["skills"] = SkillList(), ["items"] = ItemList(),
            ["target"] = target == GameObjUpdateMessages.InvalidObjectId ? null : new JsonObject
            {
                ["id"] = target,
                ["self"] = target == _player,
                ["injured"] = target == _player && _hp < _maxHp,
                ["canCancelPolymorph"] = target == _player && _formSpells.Length > 0,
            },
        });
    }

    string FeatName(int feat) => Tlk("feat", feat, "FEAT", data.TwoDA("feat", feat, "LABEL"));

    bool IsActiveFeat(int feat) => AttackFeats.Contains(feat) || Cell("feat", feat, "SPELLID") >= 0;

    JsonArray FeatList()
    {
        var list = new JsonArray();
        var modeFeats = Modes.Where(m => m.Feat >= 0).Select(m => m.Feat).ToHashSet();
        // A successor (Barbarian Rage 2..7) is shown as its first feat, which counts the uses.
        var successors = _feats.Keys.Select(f => Cell("feat", f, "SUCCESSOR")).Where(f => f >= 0).ToHashSet();
        foreach (var (feat, uses) in _feats)
        {
            if (successors.Contains(feat) || modeFeats.Contains(feat) || !IsActiveFeat(feat)) continue;
            int spell = Cell("feat", feat, "SPELLID");
            int master = Cell("feat", feat, "MASTERFEAT");
            var subs = new JsonArray();
            if (feat == 5)
            {
                subs.Add(new JsonObject { ["id"] = 1, ["name"] = "Leg", ["icon"] = "", ["targetType"] = TargetCreature });
                subs.Add(new JsonObject { ["id"] = 2, ["name"] = "Arm", ["icon"] = "", ["targetType"] = TargetCreature });
            }
            else if (spell >= 0)
                for (int i = 1; i <= 5; i++)
                    if (Cell("spells", spell, "SubRadSpell" + i) is var sub and > 0 && SpellExists(sub))
                        subs.Add(new JsonObject
                        {
                            ["id"] = sub, ["name"] = Tlk("spells", sub, "Name", data.TwoDA("spells", sub, "Label")),
                            ["icon"] = Lower(data.TwoDA("spells", sub, "IconResRef")), ["targetType"] = SpellTargetType(sub),
                        });
            list.Add(new JsonObject
            {
                ["feat"] = feat,
                ["name"] = FeatName(feat),
                ["icon"] = Lower(data.TwoDA("feat", feat, "ICON")),
                ["uses"] = uses ?? -1,
                ["max"] = uses == null ? -1 : _featMax.GetValueOrDefault(feat),
                ["targetType"] = AttackFeats.Contains(feat) ? TargetCreature
                    : Cell("feat", feat, "TARGETSELF", 0) == 1 ? TargetSelf : spell >= 0 ? SpellTargetType(spell) : TargetSelf,
                ["hostile"] = Cell("feat", feat, "HostileFeat", 0) == 1 || spell >= 0 && Cell("spells", spell, "HostileSetting", 0) == 1,
                ["spell"] = spell,
                ["master"] = master,
                ["masterName"] = master >= 0 ? Tlk("masterfeats", master, "STRREF") : "",
                ["masterIcon"] = master >= 0 ? Lower(data.TwoDA("masterfeats", master, "ICON")) : "",
                ["subs"] = subs,
            });
        }
        return list;
    }

    JsonArray ModeList()
    {
        var list = new JsonArray();
        foreach (var m in Modes.Where(Qualifies))
        {
            // Stealth shows Move Silently's icon and detect Search's, as the original's radial does.
            string icon = m.Feat >= 0 ? Lower(data.TwoDA("feat", m.Feat, "ICON"))
                : m.Mode is ModeParry or ModeStealth or ModeDetect
                    ? Lower(data.TwoDA("skills", m.Mode == ModeParry ? SkillParry : m.Mode == ModeStealth ? SkillMoveSilently : SkillSearch, "Icon"))
                    : "";
            list.Add(new JsonObject
            {
                ["mode"] = m.Mode, ["name"] = ModeName(m.Mode), ["icon"] = icon, ["on"] = ModeOn(m.Mode), ["feat"] = m.Feat,
                ["combat"] = CombatModes.Contains(m.Mode),
            });
        }
        return list;
    }

    JsonArray SkillList()
    {
        var list = new JsonArray();
        foreach (var (skill, info) in ActiveSkills)
        {
            if (!SkillUsable(skill)) continue;
            var subs = new JsonArray();
            if (skill == SkillDisableTrap)
            {
                subs.Add(new JsonObject { ["id"] = 0, ["name"] = "Disarm Trap", ["icon"] = "ir_dis_trap" });
                subs.Add(new JsonObject { ["id"] = 102, ["name"] = "Examine Trap", ["icon"] = "ir_examine" });
                subs.Add(new JsonObject { ["id"] = 100, ["name"] = "Flag Trap", ["icon"] = "ir_flag_trap" });
                subs.Add(new JsonObject { ["id"] = 101, ["name"] = "Recover Trap", ["icon"] = "ir_rec_trap" });
            }
            list.Add(new JsonObject
            {
                ["skill"] = skill,
                ["name"] = Tlk("skills", skill, "Name", data.TwoDA("skills", skill, "Label")),
                ["icon"] = Lower(data.TwoDA("skills", skill, "Icon")),
                ["rank"] = _skillRanks.TryGetValue(skill, out var r) ? r.Ranks : 0,
                ["targetType"] = info.Targets,
                ["hostile"] = info.Hostile,
                ["subs"] = subs,
            });
        }
        return list;
    }

    static bool Activatable(ItemPropertyEntry p) => p.Name is PropCastSpell or PropThievesTools or PropTrap or PropHealersKit;

    /// <summary>A potion (baseitems.2da: potions and the enchanted potion) is drunk, never aimed.</summary>
    static bool IsPotion(uint baseItem) => baseItem is 49 or 104;

    JsonArray ItemList()
    {
        var list = new JsonArray();
        foreach (var (id, item) in _items)
        {
            var props = new JsonArray();
            int active = 0;
            for (int i = 0; i < item.Properties.Count; i++)
            {
                var p = item.Properties[i];
                if (!Activatable(p)) continue;
                int uses = active < item.UsesLeft.Uses.Count && item.UsesLeft.Uses[active] is { } u ? u : -1;
                active++;
                int spell = p.Name == PropCastSpell ? Cell("iprp_spells", p.Subtype, "SpellIndex") : -1;
                string name = p.Name switch
                {
                    PropCastSpell => item.Identified ? Tlk("iprp_spells", p.Subtype, "Name", "Use") : "Use",
                    PropHealersKit => "Heal",
                    PropThievesTools => "Open Lock",
                    _ => "Set Trap",
                };
                props.Add(new JsonObject
                {
                    ["index"] = i,
                    ["key"] = $"{p.Name}:{p.Subtype}:{p.CostTableValue}:{p.Param1}",
                    ["name"] = name,
                    ["spell"] = spell,
                    ["icon"] = spell >= 0 ? Lower(data.TwoDA("iprp_spells", p.Subtype, "Icon")) : "",
                    ["targetType"] = p.Name switch
                    {
                        PropCastSpell when IsPotion(item.BaseItemId) || spell < 0 => TargetSelf,
                        PropCastSpell => SpellTargetType(spell),
                        PropHealersKit => ActiveSkills[SkillHeal].Targets,
                        PropThievesTools => ActiveSkills[SkillOpenLock].Targets,
                        _ => ActiveSkills[SkillSetTrap].Targets,
                    },
                    ["hostile"] = spell >= 0 && Cell("spells", spell, "HostileSetting", 0) == 1,
                    ["uses"] = uses,
                    ["ready"] = uses != 0,
                });
            }
            if (props.Count == 0) continue;
            list.Add(new JsonObject
            {
                ["item"] = id,
                ["name"] = ItemName(item),
                ["icon"] = new JsonArray(),
                ["key"] = $"{item.BaseItemId}:{ItemName(item)}",
                ["stack"] = item.StackSize,
                ["charges"] = 0,
                ["identified"] = item.Identified,
                ["equipped"] = _equipped.ContainsKey(id),
                ["slot"] = _equipped.TryGetValue(id, out var slot) ? slot : -1,
                ["usable"] = item.Useable,
                ["props"] = props,
            });
        }
        return list;
    }

    // -- using them ---------------------------------------------------------

    /// <summary>use-feat {feat, sub, target or pos}: mode feats toggle their mode.</summary>
    void UseFeat(JsonObject d)
    {
        int feat = Int(d, "feat", -1);
        if (feat < 0) return;
        if (Modes.FirstOrDefault(m => m.Feat == feat) is { Name: not null } mode)
        {
            ToggleMode(mode.Mode);
            return;
        }
        var target = Id(d, "target");
        var pos = Pos(d);
        if (target == GameObjUpdateMessages.InvalidObjectId && pos == null) target = _player;
        send(new InputMessages.InputUseFeat
        {
            Feat = (ushort)feat, SubFeat = (ushort)Int(d, "sub"), Target = target,
            Position = target == GameObjUpdateMessages.InvalidObjectId ? pos : null,
        });
    }

    /// <summary>use-skill {skill, sub, target, item}: Hide and Move Silently are the stealth mode, Search, Spot and Listen detect.</summary>
    void UseSkill(JsonObject d)
    {
        int skill = Int(d, "skill", -1);
        if (skill is SkillHide or SkillMoveSilently) { ToggleMode(ModeStealth); return; }
        if (skill is SkillSearch or SkillSpot or SkillListen) { ToggleMode(ModeDetect); return; }
        if (skill < 0) return;
        // A healer's kit, thieves' tools or trap kit is used through the item.
        if (Id(d, "item") is var item && item != GameObjUpdateMessages.InvalidObjectId && _items.ContainsKey(item))
        {
            UseItem(new JsonObject { ["item"] = item, ["target"] = d["target"]?.DeepClone() }, firstProperty: true);
            return;
        }
        var target = Id(d, "target");
        if (target == GameObjUpdateMessages.InvalidObjectId) target = _player;
        send(new InputMessages.InputUseSkill { Skill = (byte)skill, SubSkill = (byte)Int(d, "sub"), Target = target, Position = Pos(d) ?? (0, 0, 0) });
    }

    /// <summary>
    /// use-item {item, prop (its index in the item's properties), target or pos}; item-use {item, target} uses the
    /// first property. The server is given the property's place among the item's usable ones.
    /// </summary>
    void UseItem(JsonObject d, bool firstProperty)
    {
        var id = Id(d, "item");
        if (!_items.TryGetValue(id, out var item)) return;
        int prop = firstProperty ? item.Properties.FindIndex(Activatable) : Int(d, "prop", -1);
        if (prop < 0 || prop >= item.Properties.Count || !Activatable(item.Properties[prop])) return;
        int active = item.Properties.Take(prop).Count(Activatable);
        var target = Id(d, "target");
        var pos = Pos(d);
        if (IsPotion(item.BaseItemId) || target == GameObjUpdateMessages.InvalidObjectId && pos == null) (target, pos) = (_player, null);
        send(new InputMessages.InputUseItem
        {
            Item = id, ActivePropertyIndex = (byte)active,
            Target = target == GameObjUpdateMessages.InvalidObjectId ? null : target,
            Position = target == GameObjUpdateMessages.InvalidObjectId ? pos : null,
        });
    }
}
