using System.Text.Json.Nodes;
using OpenWinter.Net.Nwn.Messages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateGuiMessages;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// The character sheet: what the player info and creature updates always carry (abilities, armour class, hit
/// points, classes, alignment), filled in with the sheet panel the server sends while it is open (saves, base
/// attack, skills, combat figures and experience).
/// </summary>
public sealed partial class NwnAbilities
{
    static readonly string[] AbilityNames = ["STR", "DEX", "CON", "INT", "WIS", "CHA"];

    /// <summary>The client polls an open sheet twice a second; a sheet not asked for this long has closed.</summary>
    const double SheetClosedAfter = 2;
    /// <summary>How long one panel stays the server's active one before the bridge switches to the other.</summary>
    const double PanelSwitchEvery = 3;

    bool _sheetOpen, _sheetDirty = true, _sheetFilled;
    double _sheetPolled, _panelSince;
    sbyte _panel = -1;
    int _sheetVersion;
    string _sheetJson = "";

    void ResetSheet()
    {
        _sheet = null;
        (_sheetOpen, _sheetDirty, _sheetFilled, _panel, _sheetJson) = (false, true, false, -1, "");
    }

    /// <summary>
    /// character {version}: the client's sheet, opening or polled while open. The server fills the sheet panel the
    /// client has active and only that one (measured: a saving throw bonus taken while the skills panel was active
    /// came only on switching back to the main panel, which then resent all its figures), so the bridge opens on the
    /// main panel (0) and, once its figures are in, switches between it and the skills panel (1) every few seconds
    /// while the client keeps polling. What the original sends when its sheet closes isn't known; the bridge sends
    /// nothing and starts again on the main panel when the sheet is next opened.
    /// </summary>
    void Sheet(JsonObject d)
    {
        if (_player == 0) return;
        double now = _clock();
        if (!_sheetOpen || now - _sheetPolled > SheetClosedAfter)
        {
            _sheetOpen = true;
            Panel(0, now);
        }
        else if (_sheetFilled && now - _panelSince >= PanelSwitchEvery) Panel((sbyte)(1 - _panel), now);
        _sheetPolled = now;
        // A poll quoting the current version gets nothing unless the sheet changed since.
        var version = d["version"] is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;
        SendSheet(force: version != _sheetVersion.ToString());
    }

    void Panel(sbyte panel, double now)
    {
        if (panel == _panel) return;
        (_panel, _panelSince) = (panel, now);
        send(new GuiCharacterSheetMessages.GuiCharacterSheetStatus { ActivePanel = panel, Creature = _player });
    }

    /// <summary>Takes in a sheet panel update: only the parts its flags name are sent.</summary>
    void CharacterSheet(GuiCharacterSheet s)
    {
        _sheet ??= new GuiCharacterSheet { ObjectId = s.ObjectId };
        var f = s.Flags;
        _sheet.Flags |= f;
        bool Has(CharacterSheetFlags flag) => (f & flag) != 0;
        if (Has(CharacterSheetFlags.Abilities)) _sheet.Abilities = s.Abilities;
        if (Has(CharacterSheetFlags.Fortsave)) _sheet.FortSave = s.FortSave;
        if (Has(CharacterSheetFlags.Willsave)) _sheet.WillSave = s.WillSave;
        if (Has(CharacterSheetFlags.Reflexsave)) _sheet.ReflexSave = s.ReflexSave;
        if (Has(CharacterSheetFlags.BaseFortsave)) _sheet.BaseFortSave = s.BaseFortSave;
        if (Has(CharacterSheetFlags.BaseWillsave)) _sheet.BaseWillSave = s.BaseWillSave;
        if (Has(CharacterSheetFlags.BaseReflexsave)) _sheet.BaseReflexSave = s.BaseReflexSave;
        if (Has(CharacterSheetFlags.Currentxp)) _sheet.Experience = s.Experience;
        if (Has(CharacterSheetFlags.Baseattack)) _sheet.BaseAttack = s.BaseAttack;
        if (Has(CharacterSheetFlags.CombatInformation)) _sheet.CombatInfo = s.CombatInfo;
        if (Has(CharacterSheetFlags.Ac)) _sheet.Ac = s.Ac;
        if (Has(CharacterSheetFlags.Hitpoints)) _sheet.HitPoints = s.HitPoints;
        if (Has(CharacterSheetFlags.Feats)) _sheet.Feats = s.Feats;
        // The main panel's figures are in: the skills panel next, at once, so a newly opened sheet fills quickly.
        if (Has(CharacterSheetFlags.Abilities) && !_sheetFilled && _sheetOpen)
        {
            _sheetFilled = true;
            Panel(1, _clock());
        }
        if (Has(CharacterSheetFlags.Skills) && s.Skills is { } skills)
            for (int i = 0; i < skills.Ranks.Count; i++)
                if (skills.Ranks[i] is { } r) _skillRanks[i] = (r.Item1, r.Item2);
    }

    /// <summary>
    /// Sends the sheet when it changed (or always, when <paramref name="force"/>), with a version polls can quote;
    /// it is built again only when something on it changed.
    /// </summary>
    void SendSheet(bool force)
    {
        if (_player == 0) return;
        if (!_sheetDirty)
        {
            if (force && _sheetJson.Length > 0) Notify("character-sheet", Versioned(_sheetJson));
            return;
        }
        _sheetDirty = false;
        var s = _sheet;
        var sa = s?.Abilities;
        int[] scores = sa != null ? [sa.StrStat, sa.DexStat, sa.ConStat, sa.IntStat, sa.WisStat, sa.ChaStat] : _abilityFinal;
        int[] bases = sa != null ? [sa.StrBase, sa.DexBase, sa.ConBase, sa.IntBase, sa.WisBase, sa.ChaBase] : _abilityBase;
        var abilities = new JsonArray();
        for (int i = 0; i < 6; i++)
            abilities.Add(new JsonObject
            {
                ["name"] = AbilityNames[i], ["base"] = bases[i], ["score"] = scores[i],
                ["mod"] = (int)Math.Floor((scores[i] - 10) / 2.0),
            });
        int level = _classes.Sum(c => c.Level);
        int xp = (int)(s?.Experience ?? 0);
        var classes = new JsonArray();
        foreach (var c in _classes)
            classes.Add(new JsonObject { ["id"] = c.Class, ["name"] = Tlk("classes", c.Class, "Name"), ["level"] = c.Level, ["icon"] = Lower(data.TwoDA("classes", c.Class, "Icon")) });
        var skills = new JsonArray();
        for (int i = 0, n = data.TwoDARowCount("skills"); i < n; i++)
        {
            string name = Tlk("skills", i, "Name");
            if (name.Length == 0 || name.StartsWith('{') || !SkillUsable(i) && !_skillRanks.ContainsKey(i)) continue;
            // The sheet sends each skill's total (ranks and modifiers), not the ranks bought.
            var (total, _) = _skillRanks.GetValueOrDefault(i);
            skills.Add(new JsonObject
            {
                ["id"] = i, ["name"] = name, ["ranks"] = total, ["total"] = total,
                ["icon"] = Lower(data.TwoDA("skills", i, "Icon")),
                ["ability"] = data.TwoDA("skills", i, "KeyAbility").ToUpperInvariant(),
                ["armourCheck"] = Cell("skills", i, "ArmorCheckPenalty", 0) != 0,
                ["untrained"] = Cell("skills", i, "Untrained", 1) != 0,
                ["description"] = Tlk("skills", i, "Description"),
            });
        }
        var feats = new JsonArray();
        foreach (var feat in _feats.Keys)
        {
            string name = Tlk("feat", feat, "FEAT");
            if (name.Length == 0) continue;
            feats.Add(new JsonObject
            {
                ["id"] = feat, ["name"] = name, ["icon"] = Lower(data.TwoDA("feat", feat, "ICON")),
                ["category"] = Cell("feat", feat, "TOOLSCATEGORIES", 6), ["description"] = Tlk("feat", feat, "DESCRIPTION"),
            });
        }
        var combat = s?.CombatInfo;
        int str = Math.Clamp(scores[0], 0, Math.Max(0, data.TwoDARowCount("encumbrance") - 1));
        string raceName = Tlk("racialtypes", _race, "Name");
        var sheet = new JsonObject
        {
            ["creature"] = _player,
            ["name"] = _name,
            ["portrait"] = _portrait,
            ["race"] = _subrace.Length > 0 ? _subrace : raceName,
            ["baseRace"] = raceName,
            ["subrace"] = _subrace,
            ["gender"] = _gender == 1 ? "Female" : "Male",
            ["deity"] = _deity,
            ["age"] = 0,
            ["description"] = "",
            ["alignment"] = Alignment(_lawChaos, _goodEvil),
            ["lawChaos"] = _lawChaos,
            ["goodEvil"] = _goodEvil,
            ["level"] = level,
            ["xp"] = xp,
            ["xpLevel"] = XpFor(level),
            ["xpNext"] = level < 40 ? XpFor(level + 1) : xp,
            ["classes"] = classes,
            ["abilities"] = abilities,
            ["hp"] = _maxHp > 0 ? _hp : s?.HitPoints?.CurrentHp ?? 0,
            ["maxHp"] = _maxHp > 0 ? _maxHp : s?.HitPoints?.MaxHp ?? 0,
            ["tempHp"] = _tempHp,
            ["ac"] = (int?)s?.Ac ?? _ac,
            ["acParts"] = new JsonObject { ["dex"] = (int?)sa?.DexAcMod ?? 0 },
            ["bab"] = (int?)s?.BaseAttack?.BaseAttack ?? 0,
            ["attacks"] = (int?)combat?.NumAttacks ?? 1,
            ["saves"] = new JsonObject { ["fortitude"] = (int?)s?.FortSave ?? 0, ["reflex"] = (int?)s?.ReflexSave ?? 0, ["will"] = (int?)s?.WillSave ?? 0 },
            ["baseSaves"] = new JsonObject { ["fortitude"] = (int?)s?.BaseFortSave ?? 0, ["reflex"] = (int?)s?.BaseReflexSave ?? 0, ["will"] = (int?)s?.BaseWillSave ?? 0 },
            ["spellResistance"] = (int?)combat?.SpellResistance ?? 0,
            ["arcaneFailure"] = (int?)combat?.ArcaneSpellFailure ?? 0,
            ["armourCheckPenalty"] = (int?)combat?.ArmorCheckPenalty ?? 0,
            ["weight"] = _weight,
            ["walkSpeed"] = _walkRate,
            ["runSpeed"] = _runRate,
            ["carryNormal"] = Cell("encumbrance", str, "Normal", 0),
            ["carryHeavy"] = Cell("encumbrance", str, "Heavy", 0),
            ["weapons"] = combat == null ? new JsonObject() : new JsonObject
            {
                ["main"] = Weapon(4, combat.OnHandAttackMod, combat.OnHandDamageMod, combat.OnHandCritRange, combat.OnHandCritMult, (combat.UnarmedDamageDice, combat.UnarmedDamageDie)),
                ["off"] = combat.OffHandEquipped
                    ? Weapon(5, combat.OffHandAttackMod ?? 0, combat.OffHandDamageMod ?? 0, combat.OffHandCritRange ?? 1, combat.OffHandCritMult ?? 2, null)
                    : null,
            },
            ["skills"] = new JsonArray(skills.OrderBy(k => (string?)k!["name"]).Select(k => k!.DeepClone()).ToArray()),
            ["feats"] = new JsonArray(feats.OrderBy(k => (string?)k!["name"]).Select(k => k!.DeepClone()).ToArray()),
        };
        var json = sheet.ToJsonString();
        if (json != _sheetJson) (_sheetJson, force, _sheetVersion) = (json, true, _sheetVersion + 1);
        if (force) Notify("character-sheet", Versioned(json));
    }

    JsonNode Versioned(string json)
    {
        var sheet = JsonNode.Parse(json)!;
        sheet["version"] = _sheetVersion.ToString();
        return sheet;
    }

    /// <summary>
    /// A weapon line from the sheet's combat figures: the attack bonus, the weapon's dice (baseitems.2da, or the
    /// unarmed dice) and what is added to them, and the threat range (the sheet sends how many rolls threaten).
    /// </summary>
    JsonObject Weapon(int slot, int attack, int damage, int critRange, int critMult, (int Dice, int Die)? unarmed)
    {
        var weapon = _equipped.FirstOrDefault(kv => kv.Value == slot).Key;
        var (dice, die) = _items.TryGetValue(weapon, out var item) && Cell("baseitems", (int)item.BaseItemId, "NumDice", 0) is var n and > 0
            ? (n, Cell("baseitems", (int)item.BaseItemId, "DieToRoll", 0))
            : unarmed ?? (0, 0);
        return new JsonObject
        {
            ["name"] = item != null ? ItemName(item) : "", ["attack"] = attack,
            ["min"] = Math.Max(1, dice + damage), ["max"] = Math.Max(1, dice * die + damage), ["diceMin"] = dice, ["diceMax"] = dice * die, ["flat"] = damage,
            ["bonuses"] = new JsonArray(), ["threat"] = 21 - Math.Max(1, critRange), ["mult"] = critMult,
        };
    }

    int XpFor(int level) => level <= 1 ? 0 : Cell("exptable", level - 1, "XP", 0);

    static string Alignment(int lawChaos, int goodEvil)
    {
        string lc = lawChaos >= 70 ? "Lawful" : lawChaos <= 30 ? "Chaotic" : "Neutral";
        string ge = goodEvil >= 70 ? "Good" : goodEvil <= 30 ? "Evil" : "Neutral";
        return lc == "Neutral" && ge == "Neutral" ? "True Neutral" : $"{lc} {ge}";
    }
}
