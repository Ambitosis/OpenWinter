using System.Security.Cryptography;
using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Content;

/// <summary>Opt-in, disposable character controls for real-player native probes.</summary>
static class VisualCharacter
{
    public static void Write(LocalConfig cfg, JsonElement scene, JsonElement? variant, string module)
    {
        var spec = variant is { } v && (v.TryGetProperty("removeFeats", out _) || v.TryGetProperty("characterFixture", out _)) ? v : scene;
        bool fixture = spec.TryGetProperty("characterFixture", out var profile);
        bool remove = spec.TryGetProperty("removeFeats", out var removeFeats);
        string output = Path.ChangeExtension(module, ".bic");
        string metadata = Path.ChangeExtension(module, ".character.json");
        if (!fixture && !remove)
        {
            if (File.Exists(output)) File.Delete(output);
            if (File.Exists(metadata)) File.Delete(metadata);
            return;
        }

        string character = scene.TryGetProperty("character", out var c) ? c.GetString()! : "aluviandarkstar";
        ResRef(character);
        string source = new[] { Path.Combine(cfg.GameRoot, "data", "lcv"), Path.Combine(cfg.UserRoot, "localvault") }
            .Select(d => Path.Combine(d, character + ".bic")).First(File.Exists);
        byte[] original = File.ReadAllBytes(source);
        var pc = Gff.Read(original);
        if (remove)
        {
            var removed = removeFeats.EnumerateArray().Select(f => f.GetInt32()).ToHashSet();
            pc.GetList("FeatList").RemoveAll(f => removed.Contains((int)f.GetInt("Feat")));
        }
        var itemChanges = new List<object>();
        string expectedName = "";
        if (fixture)
        {
            // Both filename and display name sort before installed pregenerated characters.
            // The scene must still assert native identity/class/item gates: selection is not assumed.
            string name = profile.GetProperty("name").GetString() ?? "";
            if (string.IsNullOrWhiteSpace(name) || name.Length > 80 || name.Any(c => char.IsControl(c) || c == '|'))
                throw new InvalidDataException("Character fixture name must be 1..80 printable characters without '|'.");
            expectedName = "000 " + name;
            pc.SetLocString("FirstName", new LocString(expectedName))
                .SetLocString("LastName", new LocString(""));
            // "addFeats": [945] appends feats the character lacks (feats no item property grants, such as Scribe Scroll).
            if (profile.TryGetProperty("addFeats", out var addFeats))
            {
                var feats = pc.GetList("FeatList");
                foreach (var feat in addFeats.EnumerateArray().Select(f => f.GetInt32()))
                {
                    if (feat < 0 || feat > ushort.MaxValue) throw new InvalidDataException("Character addFeats requires feat IDs.");
                    if (!feats.Any(f => f.GetInt("Feat") == feat)) feats.Add(new GffStruct(1).SetWord("Feat", feat));
                }
                pc.SetList("FeatList", feats);
            }
            if (profile.TryGetProperty("skillRanks", out var ranks))
                foreach (var rank in ranks.EnumerateObject())
                {
                    var skills = pc.GetList("SkillList");
                    if (!int.TryParse(rank.Name, out int id) || id < 0 || id >= skills.Count || !rank.Value.TryGetByte(out byte value))
                        throw new InvalidDataException("Character skillRanks requires existing skill IDs and byte ranks.");
                    skills[id].SetByte("Rank", value);
                }
            var resources = new ResMan(new GamePaths(cfg.GameRoot, cfg.UserRoot, cfg.Language));
            resources.AddBaseGame(includeUserOverride: false);
            var items = pc.GetList("ItemList");
            if (profile.TryGetProperty("replaceItems", out var replacements))
                foreach (var replacement in replacements.EnumerateArray())
                {
                    int x = Coordinate(replacement, "x"), y = Coordinate(replacement, "y");
                    var matches = items.Where(i => i.GetInt("Repos_PosX", -1) == x && i.GetInt("Repos_PosY", -1) == y).ToList();
                    if (matches.Count != 1) throw new InvalidDataException($"Character replacement at {x},{y} requires exactly one existing item.");
                    string resref = replacement.GetProperty("resref").GetString()!;
                    ResRef(resref);
                    var item = Gff.Read(resources.Read(resref, ResType.Uti) ?? throw new InvalidDataException($"Missing installed item {resref}."));
                    item.StructType = matches[0].StructType;
                    // Preserve the installed BIC labels exactly: native expects Repos_Posy (lowercase y).
                    // A different spelling silently causes native inventory repacking and wrong hotbar targets.
                    item.SetWord(matches[0].Field("Repos_PosX")!.Label, x)
                        .SetWord(matches[0].Field("Repos_PosY")!.Label, y);
                    if (replacement.TryGetProperty("stack", out var count))
                    {
                        int stack = count.GetInt32();
                        if (stack < 1 || stack > ushort.MaxValue) throw new InvalidDataException("Character item stack must be 1..65535.");
                        item.SetWord("StackSize", stack);
                    }
                    int index = items.IndexOf(matches[0]);
                    itemChanges.Add(new
                    {
                        index, x, y, sourceBase = matches[0].GetInt("BaseItem"),
                        sourceTag = matches[0].GetString("Tag"), resref, targetBase = item.GetInt("BaseItem"),
                        targetTag = item.GetString("Tag"), stack = item.GetInt("StackSize"),
                        properties = item.GetList("PropertiesList").Select((p, slot) => new
                        {
                            slot, type = p.GetInt("PropertyName"), subtype = p.GetInt("Subtype"), cost = p.GetInt("CostValue")
                        }).ToArray()
                    });
                    items[index] = item;
                }
            pc.SetList("ItemList", items);
            if (profile.TryGetProperty("quickbarSpells", out var spellSlots))
            {
                var slots = pc.GetList("QBList");
                while (slots.Count < 36) slots.Add(new GffStruct(0).SetByte("QBObjectType", 0));
                foreach (var binding in spellSlots.EnumerateArray())
                {
                    int slot = binding.GetProperty("slot").GetInt32();
                    int spell = binding.GetProperty("spell").GetInt32();
                    int cls = binding.GetProperty("classIndex").GetInt32();
                    if (slot is < 0 or >= 36 || spell < 0 || cls < 0 || cls >= pc.GetList("ClassList").Count)
                        throw new InvalidDataException("Invalid authored quickbar spell binding.");
                    slots[slot] = new GffStruct(0).SetByte("QBObjectType", 2).SetInt("QBINTParam1", spell)
                        .SetByte("QBMultiClass", (byte)cls).SetByte("QBMetaType", 0).SetByte("QBDomainLevel", 0);
                }
                pc.SetList("QBList", slots);
            }
            // "quickbarItems": [{"slot": 7, "x": 8, "y": 0, "property": 0}] binds a slot to the backpack item at x,y
            // (its cast-spell property by index; 255 or no "property" binds the item itself).
            if (profile.TryGetProperty("quickbarItems", out var itemSlots))
            {
                var slots = pc.GetList("QBList");
                while (slots.Count < 36) slots.Add(new GffStruct(0).SetByte("QBObjectType", 0));
                foreach (var binding in itemSlots.EnumerateArray())
                {
                    int slot = binding.GetProperty("slot").GetInt32();
                    int x = Coordinate(binding, "x"), y = Coordinate(binding, "y");
                    int property = binding.TryGetProperty("property", out var p) ? p.GetInt32() : 255;
                    if (slot is < 0 or >= 36 || property is < 0 or > 255
                        || items.Count(i => i.GetInt("Repos_PosX", -1) == x && i.GetInt("Repos_PosY", -1) == y) != 1)
                        throw new InvalidDataException("Invalid authored quickbar item binding.");
                    slots[slot] = new GffStruct(0).SetByte("QBObjectType", 1).SetDWord("QBItemInvSlot", uint.MaxValue)
                        .SetByte("QBItemReposX", x).SetByte("QBItemReposY", y)
                        .SetByte("QBContReposX", 255).SetByte("QBContReposY", 255)
                        .SetByte("QBCastPropIndex", property).SetByte("QBCastSubPropIdx", 255);
                }
                pc.SetList("QBList", slots);
            }
        }
        byte[] data = Gff.Write(pc, "BIC ");
        File.WriteAllBytes(output, data);
        if (fixture)
            File.WriteAllText(metadata, JsonSerializer.Serialize(new
            {
                vaultResRef = "000_fixture", expectedIdentity = "VISUAL|note|character-fixture|" + expectedName + "|pc=1", source, sourceSha256 = Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant(),
                generatedSha256 = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant(), profile, itemChanges,
                quickbarItems = pc.GetList("QBList").Select((q, slot) => new { q, slot })
                    .Where(x => x.q.GetInt("QBObjectType") == 1).Select(x => new
                    {
                        x.slot, x = x.q.GetInt("QBItemReposX", -1), y = x.q.GetInt("QBItemReposY", -1),
                        property = x.q.GetInt("QBCastPropIndex", -1), equipped = x.q.GetInt("QBItemInvSlot", -1)
                    }).ToArray(),
            }, new JsonSerializerOptions { WriteIndented = true }));
        else if (File.Exists(metadata)) File.Delete(metadata);
    }

    static int Coordinate(JsonElement spec, string key)
    {
        int value = spec.GetProperty(key).GetInt32();
        return value is >= 0 and < 255 ? value : throw new InvalidDataException($"Character item {key} must be 0..254.");
    }

    static void ResRef(string value)
    {
        if (value.Length is < 1 or > 32 || value.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            throw new InvalidDataException("Character/item resource names must be ASCII letters, digits or underscores.");
    }
}
