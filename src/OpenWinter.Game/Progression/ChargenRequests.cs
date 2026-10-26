using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using OpenWinter.Game.Rules;
using OpenWinter.Net;

namespace OpenWinter.Game.Progression;

/// <summary>
/// Character creation requests, sent from the character selection screen before the player has a
/// creature: "chargen-data", "chargen-portraits", "chargen-describe", "chargen-validate" (options and
/// errors for a partial build, optionally completed from the class package) and "chargen-create"
/// (builds the BIC, saves it in the vault and enters the game). Answers are notifies of the same
/// names ("chargen-validate" answers "chargen-plan", "chargen-create" answers "chargen-result").
/// </summary>
public static class ChargenRequests
{
    /// <summary>Handles a request from a session that hasn't chosen a character yet.</summary>
    public static void HandlePreLogin(GameServer server, PlayerSession s, string kind, string json)
    {
        JsonElement data;
        try { data = json.Length == 0 ? default : JsonDocument.Parse(json).RootElement; }
        catch (JsonException) { return; }
        var w = server.World;
        void Reply(string k, object payload) =>
            s.Send(new NotifyMessage { Kind = k, Subject = GameObject.InvalidId, Json = JsonSerializer.Serialize(payload) }.Write());
        try
        {
            switch (kind)
            {
                case "chargen-data": Reply("chargen-data", ChargenData.Static(w)); break;
                case "chargen-portraits": Reply("chargen-portraits", ChargenData.Portraits(w, Int(data, "race"), Int(data, "gender"))); break;
                case "chargen-describe": Reply("chargen-describe", ChargenData.Describe(w, Str(data, "kind"), Int(data, "id"))); break;
                case "chargen-validate": Reply("chargen-plan", Validate(w, data)); break;
                case "chargen-create": Create(server, s, data, Reply); break;
                case "chargen-associate": Reply("chargen-associate", AssociateSheet(w, data)); break;
            }
        }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or FormatException or IndexOutOfRangeException or ArgumentException)
        {
            w.Scripts?.Log($"[chargen] {kind} from {s.PlayerName}: {e.Message}");
        }
    }

    /// <summary>The chargen-plan payload: plan for the first level plus identity errors (name, abilities, portrait, ...).</summary>
    public static object Validate(World w, JsonElement data)
    {
        var n = NewCharacter.FromJson(data.ValueKind == JsonValueKind.Object && data.TryGetProperty("character", out var ch) ? ch : data);
        LevelChoice? auto = null;
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("auto", out var a) && a.ValueKind == JsonValueKind.True
            && n.Race >= 0 && ProgressionRules.ClassExists(w, n.Level.Class))
        {
            auto = PackageBuilder.Complete(w, Blank(w, n), n.Level);
            n.Level = auto;
        }
        var result = CharacterFactory.Validate(w, n);
        var identity = CharacterFactory.IdentityErrors(w, n);
        bool sheet = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("sheet", out var sh) && sh.ValueKind == JsonValueKind.True;
        return new
        {
            plan = ChargenData.Plan(w, result.Plan, auto),
            identity = identity.Select(e => new { code = e.Code, message = e.Message }).ToArray(),
            pointsSpent = ProgressionRules.PointBuyTotal(w, n.Abilities),
            pointsBudget = n.Race >= 0 ? ProgressionRules.PointBuyBudget(w, n.Race) : 0,
            sheet = sheet ? PreviewSheet(w, n) : null,
        };
    }

    /// <summary>
    /// The character record of a build in progress (the classic creation screen shows it beside the steps): the
    /// level-0 creature, with the first level applied once a class is chosen (whatever is still missing).
    /// </summary>
    public static object? PreviewSheet(World w, NewCharacter n)
    {
        if (n.Race < 0 || n.Race >= w.Resources.TwoDA("racialtypes").RowCount) return null;
        try
        {
            var c = Blank(w, n);
            if (ProgressionRules.ClassExists(w, n.Level.Class))
            {
                var plan = ProgressionRules.Plan(w, c, n.Level, player: true);
                LevelApplier.Apply(w, c, n.Level, plan, ProgressionRules.HitDie(w, n.Level.Class));
            }
            c.CurrentHitPoints = CreatureRules.MaxHitPoints(w, c);
            return SheetWithSpells(w, c);
        }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException or ArgumentException or NullReferenceException)
        {
            w.Scripts?.Log($"[chargen] preview sheet: {e.Message}");
            return null;
        }
    }

    /// <summary>The "character-sheet" payload plus the known spells (the record's spells tab).</summary>
    static object SheetWithSpells(World w, Creature c) => new
    {
        sheet = Requests.CharacterRequests.Sheet(w, c),
        spells = c.Classes.SelectMany(cc => Enumerable.Range(0, 10).SelectMany(l => cc.KnownSpells[l].Select(sp => new { level = l, id = sp })))
            .Select(x => new { x.id, x.level, name = ProgressionRules.SpellName(w, x.id), icon = w.Resources.TwoDA("spells").Get(x.id, "IconResRef").ToLowerInvariant() })
            // By name across the levels, as the record's Spells tab lists them (c41).
            .OrderBy(x => x.name).ToArray(),
    };

    /// <summary>
    /// A familiar's or animal companion's record at a master's level ({ table: "hen_familiar" | "hen_companion", id,
    /// level }): its blueprint (BASERESREF + the two-digit level, as summoning resolves it, else level 01, else the
    /// bare resref), not created in the world.
    /// </summary>
    public static object AssociateSheet(World w, JsonElement data)
    {
        string table = Str(data, "table") == "hen_companion" ? "hen_companion" : "hen_familiar";
        int id = Int(data, "id"), level = Math.Clamp(Int(data, "level"), 1, Rules.CreatureRules.MaxLevel);
        var t = w.Resources.TwoDA(table);
        string baseRef = id >= 0 && id < t.RowCount ? t.Get(id, "BASERESREF").ToLowerInvariant() : "";
        object? sheet = null;
        if (baseRef.Length > 0)
        {
            foreach (var resref in new[] { baseRef + level.ToString("00"), baseRef + "01", baseRef })
            {
                if (w.Resources.ReadGff(resref, OpenWinter.Aurora.Resources.ResType.Utc) is not { } utc) continue;
                try
                {
                    var c = w.Creatures.FromTemplate(utc);
                    CreatureRules.EnsureInit(w, c);
                    c.CurrentHitPoints = CreatureRules.MaxHitPoints(w, c);
                    sheet = AssociateRecord(w, c);
                }
                catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException or ArgumentException or NullReferenceException)
                {
                    w.Scripts?.Log($"[chargen] associate sheet {resref}: {e.Message}");
                }
                break;
            }
        }
        return new { table, id, level, record = sheet };
    }

    /// <summary>
    /// The record the original shows for a familiar or companion blueprint (level-up's familiar step: bat,
    /// panther, hell hound, imp, fire and ice mephits, pixie and raven at a master's level 2; c37 at level 1): its name,
    /// race, alignment, classes, abilities and hit points, but unarmed (Main Weapon: Unarmed, its creature weapons and
    /// hide not counted), at medium size (the tiny bat's 1-3 unarmed damage, no size bonus), AC 9 and every save -1
    /// whatever its Dexterity, Constitution, Wisdom and classes (as a creature with 8 in each and no class saves), and
    /// spell resistance 0. Attack: base attack plus Strength, or Dexterity with Weapon Finesse (bat +3 = 1 + 2,
    /// fire mephit +2 = 2 + 0); damage 1d3 plus Strength.
    /// </summary>
    public static object AssociateRecord(World w, Creature c)
    {
        c.Equipment.Clear();
        c.Size = 3;
        var record = System.Text.Json.JsonSerializer.SerializeToNode(SheetWithSpells(w, c))!;
        if (record["sheet"] is System.Text.Json.Nodes.JsonObject sheet)
        {
            sheet["ac"] = 9;
            sheet["acParts"] = new System.Text.Json.Nodes.JsonObject
            {
                ["armour"] = 0, ["shield"] = 0, ["dex"] = -1, ["size"] = 0, ["natural"] = 0, ["deflection"] = 0, ["dodge"] = 0, ["penalty"] = 0,
            };
            sheet["saves"] = new System.Text.Json.Nodes.JsonObject { ["fortitude"] = -1, ["reflex"] = -1, ["will"] = -1 };
            sheet["baseSaves"] = new System.Text.Json.Nodes.JsonObject { ["fortitude"] = 0, ["reflex"] = 0, ["will"] = 0 };
            sheet["spellResistance"] = 0;
        }
        return record;
    }

    /// <summary>A level-0 creature with the new character's race, abilities and alignment (not in the world).</summary>
    public static Creature Blank(World w, NewCharacter n) => w.Creatures.FromTemplate(CharacterFactory.BlankTemplate(w, n));

    static void Create(GameServer server, PlayerSession s, JsonElement data, Action<string, object> reply)
    {
        var w = server.World;
        var opts = w.System<ProgressionSystem>();
        if (s.Creature != null) return;
        if (!opts.AllowCharacterCreation || !server.Options.CharacterSelection)
        {
            reply("chargen-result", new { ok = false, errors = new[] { new { code = "server", message = "This server does not allow new characters." } } });
            return;
        }
        var n = NewCharacter.FromJson(data.ValueKind == JsonValueKind.Object && data.TryGetProperty("character", out var ch) ? ch : data);
        var result = CharacterFactory.Build(w, n);
        if (!result.Ok)
        {
            reply("chargen-result", new { ok = false, errors = result.Errors.Select(e => new { code = e.Code, message = e.Message }).ToArray() });
            return;
        }
        string file = "";
        var bic = result.Bic!;
        // Re-read the written bytes: the player enters exactly as the saved character loads.
        var creature = w.Creatures.FromTemplate(Gff.Read(Gff.Write(bic, "BIC ")));
        try
        {
            if (server.Options.Vault == VaultMode.Local) file = CharacterFactory.SaveToLocalVault(w, bic);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"[server] saving {s.PlayerName}'s new character failed: {e.Message}");
        }
        reply("chargen-result", new { ok = true, file = Path.GetFileNameWithoutExtension(file), name = n.FullName });
        server.EnterWithNewCharacter(s, creature);
        if (server.Options.Vault == VaultMode.Server)
        {
            try { file = Persistence.ServerVault.Export(w, creature); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Console.WriteLine($"[server] saving {s.PlayerName}'s new character failed: {e.Message}"); }
        }
        Console.WriteLine($"[server] {s.PlayerName} created {n.FullName} ({ProgressionRules.ClassName(w, n.Level.Class)}){(file.Length > 0 ? " -> " + file : "")}");
    }

    // The same data in game (level-up window).
    [ClientRequest("chargen-data")]
    static void DataInGame(Request r) => r.World.Output?.Notify("chargen-data", r.Player, ChargenData.Static(r.World), r.Player);

    [ClientRequest("chargen-associate")]
    static void AssociateInGame(Request r) => r.World.Output?.Notify("chargen-associate", r.Player, AssociateSheet(r.World, r.Data), r.Player);

    [ClientRequest("chargen-describe")]
    static void DescribeInGame(Request r) => r.World.Output?.Notify("chargen-describe", r.Player, ChargenData.Describe(r.World, r.Str("kind"), r.Int("id")), r.Player);

    static int Int(JsonElement e, string name) => LevelChoice.Int(e, name, -1);
    static string Str(JsonElement e, string name) => LevelChoice.Str(e, name);
}
