using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Progression;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// Character creation on an NWN server, and the creation data the level-up window shares: the races, classes, skills,
/// feats, spells, portraits and descriptions on offer and the checks of a build in progress come from the rules, as
/// nwmain works them out itself. A finished character goes to the server whole, as a character file for its vault,
/// as nwmain's New button sends it; the server then logs the player in with it or says why it can't.
/// </summary>
public sealed class NwnCharacterCreation(NwnRules rules, Action<NwnMessage> send)
{
    string _pendingName = "";

    /// <summary>The creation requests; true when the request was one.</summary>
    public bool FromClient(string kind, string json)
    {
        if (!kind.StartsWith("chargen-", StringComparison.Ordinal)) return false;
        if (rules.World is not { } w) return true;
        JsonElement data;
        try { data = json.Length == 0 ? default : JsonDocument.Parse(json).RootElement; }
        catch (JsonException) { return true; }
        try
        {
            switch (kind)
            {
                case "chargen-data": rules.Notify(kind, ChargenData.Static(w)); break;
                case "chargen-portraits": rules.Notify(kind, ChargenData.Portraits(w, Int(data, "race"), Int(data, "gender"))); break;
                case "chargen-describe": rules.Notify(kind, ChargenData.Describe(w, Str(data, "kind"), Int(data, "id"))); break;
                case "chargen-associate": rules.Notify(kind, ChargenRequests.AssociateSheet(w, data)); break;
                case "chargen-validate": rules.Notify("chargen-plan", ChargenRequests.Validate(w, data)); break;
                case "chargen-create": Create(w, data); break;
            }
        }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or FormatException or IndexOutOfRangeException or ArgumentException)
        {
            if (kind == "chargen-create") Refused(e.Message);
        }
        return true;
    }

    void Create(OpenWinter.Game.World w, JsonElement data)
    {
        var n = NewCharacter.FromJson(data.ValueKind == JsonValueKind.Object && data.TryGetProperty("character", out var ch) ? ch : data);
        var result = CharacterFactory.Build(w, n);
        if (!result.Ok)
        {
            rules.Notify("chargen-result", new { ok = false, errors = result.Errors.Select(e => new { code = e.Code, message = e.Message }).ToArray() });
            return;
        }
        var file = Compact(result.Bic!);
        if (file.Length > MaxFileBytes)
        {
            rules.Notify("chargen-result", new { ok = false, errors = new[] { new { code = "size", message = "This character is too large to send to the server." } } });
            return;
        }
        _pendingName = n.FullName;
        send(new LoginMessages.LoginNewServerCharacter { CharacterData = file });
    }

    /// <summary>
    /// The largest character file the server takes: a message from the client of about 4 KiB or more stops a server
    /// running WSNetLayer (measured: a 4040-byte file goes through, a 4083-byte one stops it).
    /// </summary>
    public const int MaxFileBytes = 4040;

    /// <summary>Fields left out of the file when zero or empty: the server reads them as that when absent.</summary>
    static readonly string[] Defaulted =
    [
        "Description", "Conversation", "Subrace", "Deity", "MClassLevUpIn", "SkillPoints", "Experience",
        "RefSaveThrow", "WillSaveThrow", "FortSaveThrow", "Color_Skin", "Color_Hair", "Color_Tattoo1", "Color_Tattoo2",
        "Tail_New", "Wings_New", "BodyPart_Neck", "BodyPart_Torso", "BodyPart_Belt", "BodyPart_Pelvis",
        "BodyPart_LShoul", "BodyPart_LBicep", "BodyPart_LFArm", "BodyPart_LHand", "BodyPart_LThigh", "BodyPart_LShin",
        "BodyPart_LFoot", "BodyPart_RShoul", "BodyPart_RBicep", "BodyPart_RFArm", "BodyPart_RHand", "BodyPart_RThigh",
        "BodyPart_RShin", "ArmorPart_RFoot",
    ];

    /// <summary>
    /// The character file, made small enough to send where it can be: the file as the original writes it, else without
    /// the fields that are zero or empty and the unranked skills' ranks (none at the end of a skill list), else also
    /// without prepared spells. The server's own copy of the character has every field again, and logs in with enforce
    /// legal characters on (measured).
    /// </summary>
    public static byte[] Compact(GffStruct bic)
    {
        var file = Gff.Write(bic, "BIC ");
        if (file.Length <= MaxFileBytes) return file;
        foreach (var name in Defaulted)
            if (bic.Fields.FirstOrDefault(f => f.Label == name) is { } field && IsEmpty(field.Value)) bic.Remove(name);
        Trim(bic.GetList("SkillList"));
        foreach (var level in bic.GetList("LvlStatList")) Trim(level.GetList("SkillList"));
        file = Gff.Write(bic, "BIC ");
        if (file.Length <= MaxFileBytes) return file;
        // Then prepared spells: the character prepares them in the game instead.
        foreach (var cls in bic.GetList("ClassList"))
            for (int level = 0; level < 10; level++) cls.Remove($"MemorizedList{level}");
        return Gff.Write(bic, "BIC ");
    }

    static bool IsEmpty(object? value) => value switch
    {
        byte b => b == 0,
        sbyte b => b == 0,
        ushort u => u == 0,
        short h => h == 0,
        uint u => u == 0,
        int i => i == 0,
        string t => t.Length == 0,
        LocString l => l.FirstString.Length == 0,
        _ => false,
    };

    /// <summary>
    /// A skill list without the unranked skills at its end. The unranked ones before keep their rank field: without
    /// it the server finds the ranks invalid (measured).
    /// </summary>
    static void Trim(List<GffStruct> skills)
    {
        while (skills.Count > 0 && skills[^1].GetInt("Rank") == 0) skills.RemoveAt(skills.Count - 1);
    }

    /// <summary>The server's answer to a new character; true when the message was one.</summary>
    public bool ToClient(NwnMessage message)
    {
        if (_pendingName.Length == 0) return false;
        switch (message)
        {
            case LoginMessages.LoginCharacterFail fail:
                Refused($"The server refused the character ({fail.Reason}).");
                return true;
            case LoginMessages.LoginDeny:
                Refused("The server refused the character.");
                return true;
            case ModuleMessages.ModuleInfo:
                // Accepted: the module loads for the new character.
                rules.Notify("chargen-result", new { ok = true, file = "", name = _pendingName });
                _pendingName = "";
                return false;
        }
        return false;
    }

    void Refused(string why)
    {
        _pendingName = "";
        rules.Notify("chargen-result", new { ok = false, errors = new[] { new { code = "server", message = why } } });
    }

    static int Int(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.TryGetInt32(out int i) ? i : -1;
    static string Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
