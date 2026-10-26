using System.Text.Json.Nodes;
using OpenWinter.Net.Nwn.Messages;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>The spellbook: the "spellbook" notify, casting and preparing spells.</summary>
public sealed partial class NwnAbilities
{
    /// <summary>Metamagic feats (FEAT_*) and the METAMAGIC_* bit each one prepares.</summary>
    static readonly (int Meta, int Feat, string Name)[] MetaFeats =
    [
        (1, 11, "Empower"), (2, 12, "Extend"), (4, 25, "Maximize"), (8, 29, "Quicken"), (16, 33, "Silent"), (32, 37, "Still"),
    ];

    /// <summary>Spell levels a metamagic adds (METAMAGIC_* bit).</summary>
    static int MetaCost(int meta) => meta switch { 1 => 2, 2 => 1, 4 => 3, 8 => 4, 16 => 1, 32 => 1, _ => 0 };

    /// <summary>The spells.2da column that gives a class's spell levels (classes.2da SpellTableColumn).</summary>
    string SpellColumn(int cls) => data.TwoDA("classes", cls, "SpellTableColumn") is { Length: > 0 } c ? c : data.TwoDA("classes", cls, "Label");

    bool IsCaster(int cls) => Cell("classes", cls, "SpellCaster", 0) == 1;
    bool Memorizes(int cls) => Cell("classes", cls, "MemorizesSpells", 0) == 1;
    /// <summary>A class that prepares from its own spellbook (wizards) rather than its whole spell list.</summary>
    bool HasSpellbook(int cls) => Cell("classes", cls, "SpellbookRestricted", 0) == 1;

    int SpellLevel(int cls, int spell) => Cell("spells", spell, SpellColumn(cls));

    bool SpellExists(int spell) => spell >= 0 && data.TwoDA("spells", spell, "Label").Length > 0;

    /// <summary>The classes spells are cast with, by their place in the creature's class list.</summary>
    IEnumerable<(int Index, ClassEntry Class)> CasterClasses() =>
        _classes.Select((c, i) => (i, c)).Where(x => x.i < ClassSlots && IsCaster(x.c.Class));

    void SendSpellbook()
    {
        if (_player == 0) return;
        var used = new SortedSet<int>();
        var classes = new JsonArray();
        foreach (var (ci, cl) in CasterClasses())
        {
            bool memorizes = Memorizes(cl.Class);
            var levels = new JsonArray();
            for (int level = 0; level < SpellLevels; level++)
            {
                var entries = new JsonArray();
                var slots = new JsonArray();
                var available = new JsonArray();
                int max, left = 0;
                if (memorizes)
                {
                    max = _slots[ci, level];
                    var prepared = _prepared.Where(p => p.Key.Class == ci && p.Key.Level == level && SpellExists(p.Value.Spell)).ToList();
                    foreach (var g in prepared.GroupBy(p => (p.Value.Spell, p.Value.Meta)))
                    {
                        used.Add(g.Key.Spell);
                        entries.Add(new JsonObject { ["spell"] = g.Key.Spell, ["meta"] = g.Key.Meta, ["ready"] = g.Count(p => p.Value.Ready), ["total"] = g.Count() });
                    }
                    for (int i = 0; i < max; i++)
                    {
                        var p = _prepared.TryGetValue((ci, level, i), out var at) && SpellExists(at.Spell) ? at : new Prepared(-1, 0, false, false);
                        if (p.Ready) left++;
                        slots.Add(new JsonObject { ["index"] = i, ["spell"] = p.Spell, ["meta"] = p.Meta, ["ready"] = p.Ready, ["domain"] = p.Domain });
                    }
                    if (max > 0)
                        foreach (var s in Preparable(ci, cl, level).Concat(DomainSpells(cl, level)).Distinct())
                        {
                            available.Add(s);
                            used.Add(s);
                        }
                }
                else
                {
                    max = SpontaneousSlots(cl, level);
                    left = _usesLeft[ci, level];
                    foreach (var spell in _known[ci].Where(s => SpellExists(s) && SpellLevel(cl.Class, s) == level).Order())
                    {
                        used.Add(spell);
                        entries.Add(new JsonObject { ["spell"] = spell, ["meta"] = 0, ["ready"] = left, ["total"] = max });
                    }
                }
                if (entries.Count > 0 || slots.Count > 0)
                    levels.Add(new JsonObject { ["level"] = level, ["spells"] = entries, ["slots"] = slots, ["available"] = available, ["max"] = max, ["left"] = left });
            }
            classes.Add(new JsonObject
            {
                ["class"] = cl.Class,
                ["name"] = Tlk("classes", cl.Class, "Name"),
                ["icon"] = Lower(data.TwoDA("classes", cl.Class, "Icon")),
                ["level"] = cl.Level,
                ["casterLevel"] = cl.Level,
                ["ability"] = data.TwoDA("classes", cl.Class, "SpellcastingAbil").ToUpperInvariant(),
                ["memorizes"] = memorizes,
                ["spellbook"] = HasSpellbook(cl.Class),
                ["levels"] = levels,
            });
        }
        var abilities = new JsonArray();
        foreach (var g in _spellAbilities.Where(a => SpellExists(a.Spell)).GroupBy(a => a.Spell))
        {
            used.Add(g.Key);
            abilities.Add(new JsonObject { ["spell"] = g.Key, ["ready"] = g.Count(a => a.Ready), ["total"] = g.Count() });
        }
        foreach (var s in _formSpells.Where(SpellExists)) used.Add(s);
        var metamagic = new JsonArray();
        foreach (var m in MetaFeats.Where(m => _feats.ContainsKey(m.Feat)))
            metamagic.Add(new JsonObject { ["meta"] = m.Meta, ["name"] = m.Name, ["levels"] = MetaCost(m.Meta), ["icon"] = Lower(data.TwoDA("feat", m.Feat, "ICON")) });
        var info = new JsonArray();
        foreach (var spell in used) info.Add(SpellInfo(spell));
        Notify("spellbook", new JsonObject
        {
            ["classes"] = classes,
            ["abilities"] = abilities,
            ["polymorphed"] = _formSpells.Length > 0,
            ["formAbilities"] = new JsonArray(_formSpells.Select(s => (JsonNode)(SpellExists(s) ? s : -1)).ToArray()),
            ["metamagic"] = metamagic,
            ["info"] = info,
        });
    }

    JsonObject SpellInfo(int spell) => new()
    {
        ["spell"] = spell,
        ["name"] = Tlk("spells", spell, "Name", data.TwoDA("spells", spell, "Label")),
        ["icon"] = Lower(data.TwoDA("spells", spell, "IconResRef")),
        ["range"] = data.TwoDA("spells", spell, "Range"),
        ["school"] = SchoolName(data.TwoDA("spells", spell, "School")),
        ["components"] = data.TwoDA("spells", spell, "VS").ToUpperInvariant(),
        ["innate"] = Cell("spells", spell, "Innate"),
        ["metamagic"] = Hex(data.TwoDA("spells", spell, "MetaMagic")),
        ["targetType"] = SpellTargetType(spell),
        ["hostile"] = Cell("spells", spell, "HostileSetting", 0) == 1,
    };

    /// <summary>spells.2da TargetType bits: 1 self, 2 creature, 4 ground, 8 item, 16 door, 32 placeable, 64 trigger.</summary>
    int SpellTargetType(int spell) => Hex(data.TwoDA("spells", spell, "TargetType"));

    string SchoolName(string letter)
    {
        for (int i = 0, n = data.TwoDARowCount("spellschools"); i < n; i++)
            if (string.Equals(data.TwoDA("spellschools", i, "Letter"), letter, StringComparison.OrdinalIgnoreCase))
                return Tlk("spellschools", i, "StringRef", data.TwoDA("spellschools", i, "Label"));
        return "";
    }

    /// <summary>What a preparing class can put in a slot of a level: its spellbook (wizards) or its whole list.</summary>
    IEnumerable<int> Preparable(int ci, ClassEntry cl, int level)
    {
        if (HasSpellbook(cl.Class)) return _known[ci].Where(s => SpellExists(s) && SpellLevel(cl.Class, s) == level).Order();
        var column = SpellColumn(cl.Class);
        var list = new List<int>();
        for (int s = 0, n = data.TwoDARowCount("spells"); s < n; s++)
            if (int.TryParse(data.TwoDA("spells", s, column), out var l) && l == level) list.Add(s);
        return list;
    }

    IEnumerable<int> DomainSpells(ClassEntry cl, int level)
    {
        if (level < 1) yield break;
        foreach (var d in new[] { cl.Domain1, cl.Domain2 })
            if (d >= 0 && Cell("domains", d, $"Level_{level}") is var spell and >= 0 && SpellExists(spell)) yield return spell;
    }

    /// <summary>
    /// A spontaneous caster's casts per day at a level: the class's spell gain table, plus bonus spells from its
    /// casting ability (one more at each level up to the modifier, and another every four levels beyond it).
    /// </summary>
    int SpontaneousSlots(ClassEntry cl, int level)
    {
        var table = data.TwoDA("classes", cl.Class, "SpellGainTable");
        if (table.Length == 0 || !int.TryParse(data.TwoDA(table, cl.Level - 1, $"SpellLevel{level}"), out var n)) return 0;
        int ability = Array.IndexOf(["STR", "DEX", "CON", "INT", "WIS", "CHA"], data.TwoDA("classes", cl.Class, "SpellcastingAbil").ToUpperInvariant());
        int mod = ability < 0 ? 0 : (int)Math.Floor((_abilityFinal[ability] - 10) / 2.0);
        if (level > 0 && n > 0 && mod >= level) n += (mod - level) / 4 + 1;
        return n;
    }

    /// <summary>
    /// cast-spell {spell, class (-1 any), meta (255 any), target or pos, spontaneous}: casts with the class (by its
    /// place in the class list) and the prepared copy the original's quickbar would, a domain copy only when no other
    /// is ready.
    /// </summary>
    void CastSpell(JsonObject d)
    {
        int spell = Int(d, "spell", -1);
        if (!SpellExists(spell)) return;
        int cls = Int(d, "class", -1), meta = Int(d, "meta", 255);
        bool spontaneous = Bool(d, "spontaneous");
        var casters = CasterClasses().Where(c => cls < 0 || c.Class.Class == cls).ToList();
        if (casters.Count == 0) return;
        // A ready copy in a preparing class, or a known spell of a spontaneous one.
        (int Index, int Meta, int Domain)? pick = null;
        foreach (var (ci, cl) in casters)
        {
            if (spontaneous) { pick = (ci, meta == 255 ? 0 : meta, 0); break; }
            if (Memorizes(cl.Class))
            {
                var copy = _prepared.Where(p => p.Key.Class == ci && p.Value.Spell == spell && p.Value.Ready && (meta == 255 || p.Value.Meta == meta))
                    .OrderBy(p => p.Value.Domain).ThenBy(p => p.Key.Level).Select(p => (KeyValuePair<(int, int, int), Prepared>?)p).FirstOrDefault();
                if (copy is { } c) { pick = (ci, c.Value.Meta, c.Value.Domain ? c.Key.Item2 : 0); break; }
            }
            else if (_known[ci].Contains(spell)) { pick = (ci, meta == 255 ? 0 : meta, 0); break; }
        }
        // Nothing ready: the server answers with its own feedback, as it would to the original.
        pick ??= (casters[0].Index, meta == 255 ? 0 : meta, 0);
        var m = new InputMessages.InputCastSpell
        {
            SpellId = (uint)spell, MultiClass = (byte)pick.Value.Index, DomainLevel = (byte)pick.Value.Domain, MetaType = (byte)pick.Value.Meta,
            Flags = spontaneous ? InputMessages.CastSpellFlags.Spontaneous : 0,
            // The spell's spells.2da TargetType: the server refuses a cast that names any other.
            SpellTargetType = (ushort)SpellTargetType(spell),
        };
        Aim(d, m);
        send(m);
    }

    /// <summary>A cast's target: an object, a point in the area, or the caster.</summary>
    void Aim(JsonObject d, InputMessages.InputCastSpell m)
    {
        var target = Id(d, "target");
        if (target != GameObjUpdateMessages.InvalidObjectId)
        {
            m.Flags |= InputMessages.CastSpellFlags.HasTarget;
            m.Target = target;
        }
        else if (Pos(d) is { } pos)
        {
            m.Flags |= InputMessages.CastSpellFlags.HasPosition;
            m.Position = pos;
        }
        else
        {
            m.Flags |= InputMessages.CastSpellFlags.HasTarget;
            m.Target = _player;
        }
    }

    /// <summary>spell-memorize {class, level, slot, spell (-1 empties it), meta}.</summary>
    void Memorize(JsonObject d)
    {
        int cls = Int(d, "class", -1), level = Int(d, "level", -1), slot = Int(d, "slot", -1), spell = Int(d, "spell", -1);
        int ci = _classes.FindIndex(c => c.Class == cls);
        if (ci < 0 || ci >= ClassSlots || level is < 0 or >= SpellLevels || slot < 0) return;
        if (spell < 0)
        {
            send(new InputMessages.InputUnMemorizeSpell { MultiClass = (byte)ci, SpellLevel = (byte)level, SpellSlot = (byte)slot });
            return;
        }
        bool domain = _prepared.TryGetValue((ci, level, slot), out var at) && at.Domain;
        send(new InputMessages.InputMemorizeSpell
        {
            MultiClass = (byte)ci, SpellId = (uint)spell, SpellSlot = (byte)slot, DomainLevel = (byte)(domain ? level : 0), MetaType = (byte)Int(d, "meta"),
        });
    }
}
