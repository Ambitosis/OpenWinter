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
                        slots.Add(new JsonObject
                        {
                            ["index"] = i, ["spell"] = p.Spell, ["meta"] = p.Meta, ["ready"] = p.Ready, ["domain"] = _domainSlots.Contains((ci, level, i)),
                        });
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
            ["polymorphed"] = _polymorphed || _formSpells.Length > 0,
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
        if (_preparable.TryGetValue((cl.Class, level), out var cached)) return cached;
        var column = SpellColumn(cl.Class);
        var list = new List<int>();
        for (int s = 0, n = data.TwoDARowCount("spells"); s < n; s++)
            if (int.TryParse(data.TwoDA("spells", s, column), out var l) && l == level) list.Add(s);
        return _preparable[(cl.Class, level)] = list;
    }

    /// <summary>A class's whole spell list at a level, read once from spells.2da.</summary>
    readonly Dictionary<(int Class, int Level), List<int>> _preparable = [];

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
    /// cast-spell {spell, class (-1 any, 255 a special ability), meta (255 any), target or pos, spontaneous, form}:
    /// casts with the class (by its place in the class list) and the prepared copy the original's quickbar would, a
    /// domain copy only when no other is ready. A cure or inflict spell with no ready copy is converted
    /// spontaneously by a class that can (classes.2da CanCastSpontaneously). Special abilities and a polymorph
    /// form's abilities are cast as special abilities.
    /// </summary>
    void CastSpell(JsonObject d)
    {
        int spell = Int(d, "spell", -1);
        if (!SpellExists(spell)) return;
        int cls = Int(d, "class", -1), meta = Int(d, "meta", 255);
        if (Bool(d, "form"))
        {
            CastFormAbility(d, spell);
            return;
        }
        if (cls == 255)
        {
            CastAbility(d, spell);
            return;
        }
        bool spontaneous = Bool(d, "spontaneous");
        var casters = CasterClasses().Where(c => cls < 0 || c.Class.Class == cls).ToList();
        if (casters.Count == 0) return;
        var pick = spontaneous ? null : ReadyCopy(casters, spell, meta);
        bool Converts((int Index, ClassEntry Class) c) => Cell("classes", c.Class.Class, "CanCastSpontaneously", 0) == 1;
        if (pick == null && !spontaneous && Cell("spells", spell, "SpontaneouslyCast", 0) == 1 && casters.Any(Converts))
            (pick, spontaneous) = ((casters.First(Converts).Index, 0, 0), true);
        // A spontaneous cast goes to a class that can convert; with nothing ready, to a class whose list has the
        // spell, and the server answers with its own feedback as it would to the original.
        var fallback = casters.Where(c => spontaneous ? Converts(c) : SpellLevel(c.Class.Class, spell) >= 0).Select(c => (int?)c.Index).FirstOrDefault();
        pick ??= (fallback ?? casters[0].Index, meta == 255 ? 0 : meta, 0);
        var m = new InputMessages.InputCastSpell
        {
            SpellId = (uint)spell, MultiClass = (byte)pick.Value.Index, DomainLevel = (byte)pick.Value.Domain, MetaType = (byte)pick.Value.Meta,
            Flags = spontaneous ? InputMessages.CastSpellFlags.Spontaneous : 0,
            // The spell's spells.2da TargetType: the server refuses a cast that names any other.
            SpellTargetType = (ushort)SpellTargetType(spell),
        };
        var (flags, target, pos) = Aim(d);
        (m.Flags, m.Target, m.Position) = (m.Flags | flags, target, pos);
        send(m);
    }

    /// <summary>A ready copy of a spell: a prepared one (not a domain copy while another is ready) or a known spell with uses left.</summary>
    (int Index, int Meta, int Domain)? ReadyCopy(List<(int Index, ClassEntry Class)> casters, int spell, int meta)
    {
        foreach (var (ci, cl) in casters)
        {
            if (Memorizes(cl.Class))
            {
                var copy = _prepared.Where(p => p.Key.Class == ci && p.Value.Spell == spell && p.Value.Ready && (meta == 255 || p.Value.Meta == meta))
                    .OrderBy(p => p.Value.Domain).ThenBy(p => p.Key.Level).Select(p => (KeyValuePair<(int Class, int Level, int Slot), Prepared>?)p).FirstOrDefault();
                if (copy is { } c) return (ci, c.Value.Meta, c.Value.Domain ? c.Key.Level : 0);
            }
            else if (_known[ci].Contains(spell) && SpellLevel(cl.Class, spell) is var level and >= 0 && _usesLeft[ci, level] > 0)
                return (ci, meta == 255 ? 0 : meta, 0);
        }
        return null;
    }

    /// <summary>A special ability, at the caster level it was given (measured: the server casts it as asked).</summary>
    void CastAbility(JsonObject d, int spell)
    {
        var ability = _spellAbilities.Where(a => a.Spell == spell).OrderByDescending(a => a.Ready).FirstOrDefault();
        var (flags, target, pos) = Aim(d);
        send(new InputMessages.InputCastSpellLikeAbility
        {
            SpellId = (uint)spell, CasterLevel = (byte)ability.CasterLevel, Flags = flags, Target = target, Position = pos,
        });
    }

    /// <summary>
    /// A polymorph form's ability (polymorph.2da SPELL1-3): an ordinary cast with class 255 (measured: the server
    /// cast a mind flayer's ability given so, and not as a special ability).
    /// </summary>
    void CastFormAbility(JsonObject d, int spell)
    {
        if (!_formSpells.Contains(spell)) return;
        var (flags, target, pos) = Aim(d);
        send(new InputMessages.InputCastSpell
        {
            SpellId = (uint)spell, MultiClass = FormClass, Flags = flags, Target = target, Position = pos,
            SpellTargetType = (ushort)SpellTargetType(spell),
        });
    }

    /// <summary>The class a polymorph form's abilities are cast with.</summary>
    const byte FormClass = 255;

    /// <summary>A cast's target: an object, a point in the area, or the caster.</summary>
    (InputMessages.CastSpellFlags Flags, uint? Target, (float, float, float)? Position) Aim(JsonObject d)
    {
        var target = Id(d, "target");
        if (target != GameObjUpdateMessages.InvalidObjectId) return (InputMessages.CastSpellFlags.HasTarget, target, null);
        if (Pos(d) is { } pos) return (InputMessages.CastSpellFlags.HasPosition, null, pos);
        return (InputMessages.CastSpellFlags.HasTarget, _player, null);
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
        bool domain = _domainSlots.Contains((ci, level, slot));
        send(new InputMessages.InputMemorizeSpell
        {
            MultiClass = (byte)ci, SpellId = (uint)spell, SpellSlot = (byte)slot, DomainLevel = (byte)(domain ? level : 0), MetaType = (byte)Int(d, "meta"),
        });
    }
}
