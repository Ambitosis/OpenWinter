using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Content;

static partial class Visual
{
    // Declarative fixture deltas, never installed assets. Explicit arrays replace
    // their template lists; class levels do not implicitly grant feats or spells.
    internal static GffStruct CreatureFixture(LocalConfig cfg, JsonElement spec)
    {
        var sandbox = Path.Combine(Parity.Repo(cfg), "out", "visual", "resource-user");
        Directory.CreateDirectory(sandbox);
        var resources = ResMan.ForClient(new GamePaths(cfg.GameRoot, sandbox, cfg.Language), [], "");
        string template = spec.GetProperty("template").GetString()!;
        // "templateModule": "Prelude" reads the template from an installed official module (data/nwm or data/mod)
        // instead of the base game, so a fixture can be a campaign creature exactly as authored.
        byte[]? bytes;
        if (spec.TryGetProperty("templateModule", out var templateModule))
        {
            string module = templateModule.GetString()!;
            var file = new[] { "nwm", "mod" }.Select(d => Path.Combine(cfg.GameRoot, "data", d, module + "." + d)).FirstOrDefault(File.Exists)
                ?? throw new FileNotFoundException($"official module {module} not found under data/nwm or data/mod");
            using var erf = new ErfSource(file);
            bytes = erf.Read(ResKey.Of(template, ResType.FromExtension("utc")));
        }
        else bytes = resources.Read(template, ResType.FromExtension("utc"));
        if (bytes == null) throw new InvalidDataException($"Missing installed creature template: {template}");
        var actor = Gff.Read(bytes);
        // Event scripts are cleared unless "keepScripts": true (a campaign creature with its stock AI).
        if (!(spec.TryGetProperty("keepScripts", out var keep) && keep.GetBoolean()))
            foreach (var field in actor.Fields.Where(x => x.Label.StartsWith("Script") && x.Type == GffType.ResRef))
                field.Value = "";
        // Preserve the existing exact-HP label fixture contract.
        if (spec.TryGetProperty("hitPoints", out var hpSpec))
        {
            int hp = Bounded(hpSpec, 1, short.MaxValue, "hitPoints");
            actor.SetShort("HitPoints", hp).SetShort("MaxHitPoints", hp).SetShort("CurrentHitPoints", hp)
                .SetByte("Con", 10).SetList("FeatList", []);
        }
        if (spec.TryGetProperty("classLevels", out var classes))
        {
            var list = new List<GffStruct>();
            var seen = new HashSet<int>();
            foreach (var entry in classes.EnumerateArray())
            {
                int id = Bounded(entry.GetProperty("class"), 0, 255, "class");
                int level = Bounded(entry.GetProperty("level"), 1, 255, "level");
                if (!seen.Add(id)) throw new InvalidDataException($"Duplicate fixture class: {id}");
                var cls = new GffStruct(2).SetInt("Class", id).SetShort("ClassLevel", level);
                if (entry.TryGetProperty("domains", out var domains))
                {
                    var selected = domains.EnumerateArray().Select(x => Bounded(x, 0, 255, "domain")).ToArray();
                    if (selected.Length != 2 || selected[0] == selected[1])
                        throw new InvalidDataException("Fixture domains must contain two different domain IDs");
                    cls.SetByte("Domain1", selected[0]).SetByte("Domain2", selected[1]);
                }
                // Raw optional fields support loader-presence/invalid-value controls;
                // unlike legal `domains`, null deliberately leaves that GFF field absent.
                if (entry.TryGetProperty("domainFields", out var domainFields))
                {
                    var fields = domainFields.EnumerateArray().ToArray();
                    if (fields.Length != 2) throw new InvalidDataException("Fixture domainFields requires two entries");
                    for (int i = 0; i < fields.Length; i++)
                        if (fields[i].ValueKind != JsonValueKind.Null)
                            cls.SetByte($"Domain{i + 1}", Bounded(fields[i], 0, 255, "domain field"));
                }
                if (entry.TryGetProperty("knownSpells", out var known))
                    foreach (var levelList in known.EnumerateObject())
                    {
                        int spellLevel = SpellLevel(levelList.Name);
                        cls.SetList($"KnownList{spellLevel}", levelList.Value.EnumerateArray()
                            .Select(x => new GffStruct(3).SetWord("Spell", Bounded(x, 0, ushort.MaxValue, "known spell"))).ToList());
                    }
                if (entry.TryGetProperty("memorizedSpells", out var memorized))
                    foreach (var levelList in memorized.EnumerateObject())
                    {
                        int spellLevel = SpellLevel(levelList.Name);
                        cls.SetList($"MemorizedList{spellLevel}", levelList.Value.EnumerateArray().Select(x =>
                            new GffStruct(3).SetWord("Spell", Bounded(x.GetProperty("spell"), 0, ushort.MaxValue, "memorized spell"))
                                .SetByte("Ready", !x.TryGetProperty("ready", out var ready) || ready.GetBoolean() ? 1 : 0)
                                .SetByte("SpellMetaMagic", x.TryGetProperty("metamagic", out var meta) ? Bounded(meta, 0, 255, "metamagic") : 0)).ToList());
                    }
                list.Add(cls);
            }
            if (list.Count == 0) throw new InvalidDataException("classLevels must contain a class");
            actor.SetList("ClassList", list);
        }
        if (spec.TryGetProperty("skills", out var skills))
        {
            var list = actor.GetList("SkillList");
            foreach (var skill in skills.EnumerateObject())
            {
                if (!int.TryParse(skill.Name, out int id) || id < 0 || id > 255)
                    throw new InvalidDataException($"Invalid fixture skill: {skill.Name}");
                while (list.Count <= id) list.Add(new GffStruct(0).SetByte("Rank", 0));
                list[id].SetByte("Rank", Bounded(skill.Value, 0, 255, "skill rank"));
            }
            actor.SetList("SkillList", list);
        }
        if (spec.TryGetProperty("feats", out var feats))
            actor.SetList("FeatList", feats.EnumerateArray().Select(x => Bounded(x, 0, ushort.MaxValue, "feat"))
                .Distinct().Select(id => new GffStruct(1).SetWord("Feat", id)).ToList());
        if (spec.TryGetProperty("equipment", out var equipment))
        {
            var list = new List<GffStruct>();
            var seen = new HashSet<int>();
            foreach (var entry in equipment.EnumerateArray())
            {
                int slot = Bounded(entry.GetProperty("slot"), 0, 17, "equipment slot");
                if (!seen.Add(slot)) throw new InvalidDataException($"Duplicate fixture equipment slot: {slot}");
                list.Add(ItemEntry(resources, entry, 1u << slot, "EquippedRes", null));
            }
            actor.SetList("Equip_ItemList", list);
        }
        // "items": [{ "resref", "dropable": 0|1, "pickpocketable": 0|1, "embed": true }] replaces the inventory
        // (ItemList). Flags left out are left out of the entry, as the toolset leaves them.
        if (spec.TryGetProperty("items", out var items))
            actor.SetList("ItemList", items.EnumerateArray().Select((entry, i) =>
                ItemEntry(resources, entry, (uint)i, "InventoryRes", i)).ToList());
        // "fields": { "DecayTime": { "dword": 0 }, "IsDestroyable": { "byte": 0 }, "Lootable": 1 } sets top-level fields
        // (typed objects add fields the template lacks with the GFF type the original reads).
        if (spec.TryGetProperty("fields", out var fieldSet)) Parity.Apply(actor, fieldSet);
        if (spec.TryGetProperty("abilities", out var abilities))
            foreach (var ability in abilities.EnumerateObject())
            {
                string label = ability.Name switch
                {
                    "strength" => "Str", "dexterity" => "Dex", "constitution" => "Con",
                    "intelligence" => "Int", "wisdom" => "Wis", "charisma" => "Cha",
                    _ => throw new InvalidDataException($"Unknown fixture ability: {ability.Name}"),
                };
                actor.SetByte(label, Bounded(ability.Value, 1, 255, ability.Name));
            }
        // "specAbilities": [{ "spell": 90, "level": 6, "ready": true }] replaces the special ability list.
        if (spec.TryGetProperty("specAbilities", out var specAbilities))
            actor.SetList("SpecAbilityList", specAbilities.EnumerateArray().Select(x => new GffStruct(4)
                .SetWord("Spell", Bounded(x.GetProperty("spell"), 0, ushort.MaxValue, "special ability"))
                .SetByte("SpellFlags", !x.TryGetProperty("ready", out var ready) || ready.GetBoolean() ? 1 : 0)
                .SetByte("SpellCasterLevel", x.TryGetProperty("level", out var lvl) ? Bounded(lvl, 0, 255, "caster level") : 1)).ToList());
        // Event scripts a fixture keeps ("scripts": { "ScriptSpawn": "my_spawn" }) and its tag.
        if (spec.TryGetProperty("scripts", out var scripts))
            foreach (var script in scripts.EnumerateObject()) actor.SetResRef(script.Name, script.Value.GetString() ?? "");
        if (spec.TryGetProperty("tag", out var tagSpec)) actor.SetString("Tag", tagSpec.GetString() ?? "");
        if (spec.TryGetProperty("challengeRating", out var cr))
            actor.SetFloat("ChallengeRating", cr.GetSingle());
        foreach (var (key, prefix) in new[] { ("familiar", "Familiar"), ("companion", "Companion") })
            if (spec.TryGetProperty(key, out var associate))
            {
                actor.SetInt(prefix + "Type", Bounded(associate.GetProperty("type"), 0, 255, key + " type"));
                if (associate.TryGetProperty("name", out var name)) actor.SetString(prefix + "Name", name.GetString() ?? "");
            }
        return actor;
    }

    /// <summary>
    /// One equipment or inventory entry: a blueprint reference (<paramref name="resLabel"/>, as blueprints carry them)
    /// or, with "embed", the whole item as instances placed in an area carry it; "dropable"/"pickpocketable" only when given.
    /// </summary>
    static GffStruct ItemEntry(ResMan resources, JsonElement entry, uint structType, string resLabel, int? gridX)
    {
        string item = entry.GetProperty("resref").GetString()!;
        var uti = resources.ReadGff(item, ResType.FromExtension("uti"))
            ?? throw new InvalidDataException($"Missing installed item template: {item}");
        GffStruct result;
        if (entry.TryGetProperty("embed", out var embed) && embed.GetBoolean())
        {
            result = uti.DeepClone();
            result.StructType = structType;
            result.SetResRef("TemplateResRef", item);
        }
        else result = new GffStruct(structType).SetResRef(resLabel, item);
        if (gridX is int x) result.SetWord("Repos_PosX", x * 2).SetWord("Repos_Posy", 0);
        if (entry.TryGetProperty("dropable", out var dropable)) result.SetByte("Dropable", dropable.GetInt32());
        if (entry.TryGetProperty("pickpocketable", out var pick)) result.SetByte("Pickpocketable", pick.GetInt32());
        return result;
    }

    /// <summary>
    /// The blueprints one fixture file makes: itself, or with "challengeRatings": [..] one copy per value named
    /// &lt;resref&gt;0, &lt;resref&gt;1, ... (the kill XP scenes vary only the victim's challenge rating).
    /// </summary>
    internal static IEnumerable<(string ResRef, GffStruct Gff)> CreatureFixtures(LocalConfig cfg, string resref, JsonElement spec)
    {
        if (!spec.TryGetProperty("challengeRatings", out var list))
        {
            yield return (resref, CreatureFixture(cfg, spec));
            yield break;
        }
        int i = 0;
        foreach (var cr in list.EnumerateArray())
        {
            string name = resref + i++;
            if (name.Length > 16) throw new InvalidDataException($"Fixture resref too long: {name}");
            yield return (name, CreatureFixture(cfg, spec).SetFloat("ChallengeRating", cr.GetSingle()));
        }
    }

    static int SpellLevel(string text) => int.TryParse(text, out int level) && level is >= 0 and <= 9 ? level
        : throw new InvalidDataException($"Fixture spell level must be in 0..9: {text}");

    static int Bounded(JsonElement value, int min, int max, string name)
    {
        int number = value.GetInt32();
        return number >= min && number <= max ? number
            : throw new InvalidDataException($"Fixture {name} must be in {min}..{max}");
    }
}
