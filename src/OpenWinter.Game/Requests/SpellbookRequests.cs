using OpenWinter.Game.Objects;
using OpenWinter.Game.Progression;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Spells;

namespace OpenWinter.Game.Requests;

/// <summary>
/// The spellbook window and quickbar: what the player can cast, per class and spell level, with
/// uses left, plus each spell's name, icon, school, range, components and targeting so the client
/// can show and aim it. Classes that prepare spells also get their memorisation slots and the
/// spells they could prepare at each level ("spell-memorize" fills or clears a slot).
/// </summary>
public static class SpellbookRequests
{
    /// <summary>Feats that let a caster prepare a spell with a metamagic (METAMAGIC_* bit → FEAT_*).</summary>
    public static readonly (int Meta, int Feat, string Name)[] MetaFeats =
    {
        (NwRules.MetaEmpower, 11, "Empower"), (NwRules.MetaExtend, 12, "Extend"), (NwRules.MetaMaximize, 25, "Maximize"),
        (NwRules.MetaQuicken, 29, "Quicken"), (NwRules.MetaSilent, 33, "Silent"), (NwRules.MetaStill, 37, "Still"),
    };

    [ClientRequest("spellbook")]
    static void Spellbook(Request r) => SendBook(r);

    static void SendBook(Request r)
    {
        if (r.World.Output is not { } output) return;
        var book = Describe(r.World, r.Player);
        output.Notify("spellbook", r.Player, book, r.Player);
        r.World.System<AbilitiesFeed>().SpellbookSent(r.Player, book);
    }

    /// <summary>
    /// Prepares a spell in a memorisation slot ({class, level, slot, spell, meta}; spell -1 empties it).
    /// Like the original, a newly prepared spell is ready after the next rest. Answers with the spellbook.
    /// </summary>
    [ClientRequest("spell-memorize")]
    static void Memorize(Request r)
    {
        int cls = r.Int("class", -1), level = r.Int("level", -1), slot = r.Int("slot", -1), spell = r.Int("spell", -1), meta = r.Int("meta", 0);
        int index = CreatureRules.ClassIndex(r.Player, cls);
        // The player's spellbook: NWNX_SET_MEMORIZED_SPELL_SLOT, or NWNX_CLEAR_MEMORIZED_SPELL_SLOT for an emptied slot;
        // a skip leaves the slot as it is.
        var ev = spell < 0
            ? Nwnx.NwnxEvent.Begin(r.World, "NWNX_CLEAR_MEMORIZED_SPELL_SLOT", r.Player)?.Add("SPELL_MULTICLASS", index).Add("SPELL_LEVEL", level).Add("SPELL_SLOT", slot)
            : Nwnx.NwnxEvent.Begin(r.World, "NWNX_SET_MEMORIZED_SPELL_SLOT", r.Player)?.Add("SPELL_MULTICLASS", index).Add("SPELL_SLOT", slot)
                .Add("SPELL_ID", spell).Add("SPELL_DOMAIN", index >= 0 && r.Player.DomainSlots.Contains((index, level, slot)) ? level : 0)
                .Add("SPELL_METAMAGIC", meta).Add("SPELL_FROMCLIENT", true);
        if (ev?.Before() == true) { ev.Add("ACTION_RESULT", false).After(); SendBook(r); return; }
        var error = Memorize(r.World, r.Player, cls, level, slot, spell, meta);
        if (spell >= 0) ev?.Add("ACTION_RESULT", error == null);
        ev?.After();
        if (error != null) r.World.Output?.Notify("spellbook-error", r.Player, new { message = error }, r.Player);
        SendBook(r);
    }

    /// <summary>Fills (or with spell -1 clears) a memorisation slot; returns why it can't, or null.</summary>
    public static string? Memorize(World w, Creature c, int cls, int level, int slot, int spell, int meta)
    {
        CreatureRules.EnsureInit(w, c);
        int ci = CreatureRules.ClassIndex(c, cls);
        if (ci < 0 || !CreatureRules.MemorizesSpells(w, cls)) return "That class doesn't prepare spells.";
        if (level < 0 || level > 9 || !CreatureRules.EnsureSlot(w, c, cls, level, slot)) return "There is no such spell slot.";
        var list = c.Classes[ci].Memorized[level];
        if (spell < 0)
        {
            list[slot] = (-1, 0, false);
            return null;
        }
        if (!SpellTable.Exists(w, spell)) return "Unknown spell.";
        if (meta != 0 && !MetaFeats.Any(m => m.Meta == meta && CreatureRules.HasFeat(w, c, m.Feat, ignoreUses: true)))
            return "You don't have that metamagic feat.";
        if (meta != 0 && (MetaMagicAllowed(w, spell) & meta) == 0) return "That metamagic can't be applied to this spell.";
        bool domain = c.DomainSlots.Contains((ci, level, slot));
        int baseLevel = level - CreatureRules.MetaMagicCost(w, meta);
        if (baseLevel < 0 || !Preparable(w, c, ci, baseLevel, domain).Contains(spell)) return "You can't prepare that spell in this slot.";
        list[slot] = (spell, meta, false);
        return null;
    }

    /// <summary>Spells a class could prepare at a spell level: its spellbook (wizards), its class list, or a domain slot's domain spells.</summary>
    public static IReadOnlyList<int> Preparable(World w, Creature c, int classIndex, int level, bool domainSlot = false)
    {
        var cc = c.Classes[classIndex];
        if (domainSlot) return DomainSpells(w, cc, level).ToList();
        IEnumerable<int> spells = ProgressionRules.HasSpellbook(w, cc.Class) ? cc.KnownSpells[level] : ProgressionRules.ClassSpells(w, cc.Class, level, cc.School);
        return spells.Where(s => SpellTable.Exists(w, s)).Distinct().ToList();
    }

    static IEnumerable<int> DomainSpells(World w, CreatureClass cc, int level)
    {
        if (level < 1) yield break;
        var domains = w.Resources.TwoDA("domains");
        foreach (var d in new[] { cc.Domain1, cc.Domain2 })
            if (d >= 0 && domains.Get(d, $"Level_{level}") is { Length: > 0 } v && int.TryParse(v, out int spell) && SpellTable.Exists(w, spell))
                yield return spell;
    }

    static int MetaMagicAllowed(World w, int spell)
    {
        var raw = SpellTable.Get(w, spell, "MetaMagic");
        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) raw = raw[2..];
        return raw.Length > 0 && int.TryParse(raw, System.Globalization.NumberStyles.HexNumber, null, out int v) ? v : 0;
    }

    public static object Describe(World w, Creature c)
    {
        CreatureRules.EnsureInit(w, c);
        var res = w.Resources;
        var spells = SpellTable.Table(w);
        var classTable = res.TwoDA("classes");
        string Name(int spell) => spells.GetInt(spell, "Name", -1) is var s and >= 0 ? res.TlkString(unchecked((uint)s)) : spells.Get(spell, "Label");
        var used = new HashSet<int>();
        var classes = new List<object>();
        for (int ci = 0; ci < c.Classes.Count; ci++)
        {
            var cl = c.Classes[ci];
            if (!CreatureRules.IsSpellCaster(w, cl.Class)) continue;
            bool memorizes = CreatureRules.MemorizesSpells(w, cl.Class);
            var levels = new List<object>();
            for (int level = 0; level < 10; level++)
            {
                var entries = new List<object>();
                var slots = new List<object>();
                var available = new List<int>();
                int max, left;
                if (memorizes)
                {
                    max = CreatureRules.MemorizedSlotCount(w, c, cl.Class, level);
                    var mem = cl.Memorized[level];
                    // Memorised slots, grouped by spell and metamagic.
                    // Empty slots hold no spell (65535 in character files) and aren't listed.
                    foreach (var g in mem.Where(m => SpellTable.Exists(w, m.Spell)).GroupBy(m => (m.Spell, m.Meta)))
                    {
                        used.Add(g.Key.Spell);
                        entries.Add(new { spell = g.Key.Spell, meta = g.Key.Meta, ready = g.Count(m => m.Ready), total = g.Count() });
                    }
                    // Every slot in order, empty ones included, for preparing spells.
                    left = 0;
                    for (int i = 0; i < max; i++)
                    {
                        var m = i < mem.Count ? mem[i] : (Spell: -1, Meta: 0, Ready: false);
                        bool exists = SpellTable.Exists(w, m.Spell);
                        if (exists && m.Ready) left++;
                        slots.Add(new { index = i, spell = exists ? m.Spell : -1, meta = exists ? m.Meta : 0, ready = exists && m.Ready, domain = c.DomainSlots.Contains((ci, level, i)) });
                    }
                    if (max > 0)
                    {
                        available.AddRange(Preparable(w, c, ci, level));
                        foreach (var s in DomainSpells(w, cl, level)) if (!available.Contains(s)) available.Add(s);
                    }
                    foreach (var s in available) used.Add(s);
                }
                else
                {
                    max = CreatureRules.SpellSlots(w, c, cl.Class, level);
                    left = CreatureRules.SlotsLeft(w, c, ci, level);
                    foreach (var spell in cl.KnownSpells[level].Where(s => SpellTable.Exists(w, s)))
                    {
                        used.Add(spell);
                        entries.Add(new { spell, meta = 0, ready = left, total = max });
                    }
                }
                if (entries.Count > 0 || slots.Count > 0)
                    levels.Add(new { level, spells = entries, slots, available, max, left });
            }
            classes.Add(new
            {
                @class = cl.Class,
                name = res.TlkString(unchecked((uint)classTable.GetInt(cl.Class, "Name", 0))),
                icon = classTable.Get(cl.Class, "Icon").ToLowerInvariant(),
                level = cl.Level,
                casterLevel = CreatureRules.CasterLevel(w, c, cl.Class),
                ability = classTable.Get(cl.Class, "SpellcastingAbil").ToUpperInvariant(),
                memorizes,
                spellbook = ProgressionRules.HasSpellbook(w, cl.Class),
                levels,
            });
        }
        var formAbilities = EffectSystem.FormAbilities(w, c).Select(s => SpellTable.Exists(w, s) ? s : -1).ToArray();
        foreach (int s in formAbilities) if (s >= 0) used.Add(s);
        var abilities = c.SpellAbilities.Where(a => SpellTable.Exists(w, a.Spell)).GroupBy(a => a.Spell)
            .Select(g => new { spell = g.Key, ready = g.Count(a => a.Ready), total = g.Count() }).ToArray();
        foreach (var ability in abilities) used.Add(ability.spell);
        var schools = res.TwoDA("spellschools");
        string SchoolName(string letter)
        {
            for (int i = 0; i < schools.RowCount; i++)
                if (string.Equals(schools.Get(i, "Letter"), letter, StringComparison.OrdinalIgnoreCase))
                    return schools.GetInt(i, "StringRef", -1) is var s and >= 0 ? res.TlkString(unchecked((uint)s)) : schools.Get(i, "Label");
            return "";
        }
        var featTable = res.TwoDA("feat");
        return new
        {
            classes,
            abilities,
            // Polymorphed, the original greys out the character's spells on the quickbar (they stay there,
            // with their counts) and refuses them; items and feats stay usable (visual scene polymorphpc).
            polymorphed = c.PolymorphForm != null,
            // The form's SPELL1-3 (-1 for an empty column), for the radial's Cast Spell wedge.
            formAbilities,
            // Metamagic feats the character has, for preparing spells with them.
            metamagic = MetaFeats.Where(m => CreatureRules.HasFeat(w, c, m.Feat, ignoreUses: true))
                .Select(m => new { meta = m.Meta, name = m.Name, levels = CreatureRules.MetaMagicCost(w, m.Meta), icon = featTable.Get(m.Feat, "ICON").ToLowerInvariant() }).ToArray(),
            info = used.Select(spell => new
            {
                spell,
                name = Name(spell),
                icon = spells.Get(spell, "IconResRef").ToLowerInvariant(),
                range = spells.Get(spell, "Range"),
                school = SchoolName(spells.Get(spell, "School")),
                components = spells.Get(spell, "VS").ToUpperInvariant(),
                innate = spells.GetInt(spell, "Innate", -1),
                metamagic = MetaMagicAllowed(w, spell),
                // Bit flags from spells.2da TargetType: 1 self, 2 creature, 4 area/ground, 8 item, 16 door, 32 placeable, 64 trigger.
                targetType = Convert.ToInt32(spells.Get(spell, "TargetType") is { Length: > 2 } hex && hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : "0", 16),
                hostile = SpellTable.Hostile(w, spell),
            }).ToArray(),
        };
    }
}
