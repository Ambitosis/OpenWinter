using System.Text.Json.Nodes;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using static OpenWinter.Game.Persistence.CampaignDatabase;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>Campaign database, object serialization (json/templates), UUIDs and character export.</summary>
static class PersistenceFunctions
{
    static PersistenceSystem Sys(Call c) => c.World.System<PersistenceSystem>();

    // ---- Campaign variables -------------------------------------------------------------
    // Set*(string sCampaignName, string sVarName, <value>, object oPlayer=OBJECT_INVALID)

    static object? Set(Call c, int type, Action<SqlStatement, int> bind)
    {
        try { CampaignDatabase.Set(c.World, c.S(0), c.S(1), c.Obj(3), type, bind); }
        catch (Exception e) when (e is IOException or InvalidOperationException) { c.Host.Log($"[campaign] {e.Message}"); }
        return null;
    }

    static T Get<T>(Call c, int type, Func<CampaignValue, T> read, T fallback)
    {
        try { return CampaignDatabase.Get(c.World, c.S(0), c.S(1), c.Obj(2), type, read, fallback); }
        catch (Exception e) when (e is IOException or InvalidOperationException) { c.Host.Log($"[campaign] {e.Message}"); return fallback; }
    }

    [NwFunc("SetCampaignInt")] static object? SetInt(Call c) => Set(c, TypeInt, (q, i) => q.BindInt(i, c.I(2)));
    [NwFunc("SetCampaignFloat")] static object? SetFloat(Call c) => Set(c, TypeFloat, (q, i) => q.BindDouble(i, c.F(2)));
    [NwFunc("SetCampaignString")] static object? SetString(Call c) => Set(c, TypeString, (q, i) => q.BindText(i, c.S(2)));
    [NwFunc("SetCampaignVector")] static object? SetVector(Call c) => Set(c, TypeVector, (q, i) => q.BindText(i, VectorText(c.V(2))));
    [NwFunc("SetCampaignLocation")] static object? SetLocation(Call c) => Set(c, TypeLocation, (q, i) => q.BindText(i, LocationText(c.World, c.L(2))));
    [NwFunc("SetCampaignJson")] static object? SetJson(Call c) => Set(c, TypeJson, (q, i) => q.BindText(i, JsonFunctions.Dump(c.J(2))));

    [NwFunc("GetCampaignInt")] static object? GetInt(Call c) => Get(c, TypeInt, v => unchecked((int)v.Int()), 0);
    [NwFunc("GetCampaignFloat")] static object? GetFloat(Call c) => Get(c, TypeFloat, v => (float)v.Double(), 0f);
    [NwFunc("GetCampaignString")] static object? GetString(Call c) => Get(c, TypeString, v => v.Text(), "");
    [NwFunc("GetCampaignVector")] static object? GetVector(Call c) => Get(c, TypeVector, v => ParseVector(v.Text()), System.Numerics.Vector3.Zero);
    [NwFunc("GetCampaignLocation")] static object? GetLocation(Call c) => Get(c, TypeLocation, v => ParseLocation(c.World, v.Text()), Location.Invalid);
    [NwFunc("GetCampaignJson")] static object? GetJson(Call c) => Get(c, TypeJson, v => JsonFunctions.Parse(v.Text()), JsonValue.Null);

    [NwFunc("DeleteCampaignVariable")]
    static object? Delete(Call c)
    {
        try { CampaignDatabase.Delete(c.World, c.S(0), c.S(1), c.Obj(2)); }
        catch (Exception e) when (e is IOException or InvalidOperationException) { c.Host.Log($"[campaign] {e.Message}"); }
        return null;
    }

    [NwFunc("DestroyCampaignDatabase")] static object? DestroyDatabase(Call c) { Sys(c).DestroyCampaign(c.S(0)); return null; }

    // int StoreCampaignObject(string sCampaignName, string sVarName, object oObject, object oPlayer=OBJECT_INVALID, int bSaveObjectState=FALSE)
    [NwFunc("StoreCampaignObject")]
    static object? StoreObject(Call c)
    {
        // Every serializable type but areas; stored zstd-compressed in a "CPDB" buffer like the original's
        // (parity suite persist_objects: vartype 'O', compressed 1).
        if (c.Obj(2) is not { } o || !ObjectSerializer.CanStore(o) || ObjectSerializer.ToBytes(c.World, o, c.I(4) != 0) is not { } bytes) return 0;
        try
        {
            var packed = CompressedBuffer.Wrap("CPDB", bytes);
            CampaignDatabase.Set(c.World, c.S(0), c.S(1), c.Obj(3), TypeObject, (q, i) => q.BindBlob(i, packed), compressed: true);
            return PersistenceSystem.CampaignKey(c.S(0)) != null ? 1 : 0;
        }
        catch (Exception e) when (e is IOException or InvalidOperationException) { c.Host.Log($"[campaign] {e.Message}"); return 0; }
    }

    // object RetrieveCampaignObject(string sCampaignName, string sVarName, location locLocation, object oOwner = OBJECT_INVALID, object oPlayer=OBJECT_INVALID, int bLoadObjectState=FALSE)
    [NwFunc("RetrieveCampaignObject")]
    static object? RetrieveObject(Call c)
    {
        byte[]? bytes;
        try { bytes = CampaignDatabase.Get(c.World, c.S(0), c.S(1), c.Obj(4), TypeObject, v => v.Blob(), null); }
        catch (Exception e) when (e is IOException or InvalidOperationException) { c.Host.Log($"[campaign] {e.Message}"); return GameObject.InvalidId; }
        if (bytes == null) return GameObject.InvalidId;
        return ObjectSerializer.FromBytes(c.World, bytes, c.L(2), c.Obj(3), c.I(5) != 0)?.Id ?? GameObject.InvalidId;
    }

    // ---- Json / templates -------------------------------------------------------------

    // json ObjectToJson(object oObject, int bSaveObjectState = FALSE)
    [NwFunc("ObjectToJson")]
    static object? ObjectToJson(Call c)
    {
        var o = c.Obj(0);
        if (o == null) return new JsonValue(null) { Error = "invalid object" };
        if (ObjectSerializer.Serialize(c.World, o, c.I(1) != 0) is not { } gff)
            return new JsonValue(null) { Error = $"object type {o.Type} cannot be serialized" };
        return new JsonValue(GffJson.ToJson(gff, ObjectSerializer.FileType(o)));
    }

    // object JsonToObject(json jObject, location locLocation, object oOwner = OBJECT_INVALID, int bLoadObjectState = FALSE)
    [NwFunc("JsonToObject")]
    static object? JsonToObject(Call c)
    {
        try
        {
            var gff = GffJson.FromJson(c.J(0).Node, out var type);
            return ObjectSerializer.Deserialize(c.World, gff, type, c.L(1), c.Obj(2), c.I(3) != 0)?.Id ?? GameObject.InvalidId;
        }
        catch (Exception e) when (e is FormatException or InvalidOperationException or OverflowException)
        {
            c.Host.Log($"[json] JsonToObject: {e.Message}");
            return GameObject.InvalidId;
        }
    }

    static readonly HashSet<ushort> TemplateTypes = new()
    {
        ResType.Are, ResType.Git, ResType.Gic, ResType.Utc, ResType.Uti, ResType.Utt, ResType.Utp, ResType.Utd, ResType.Utw,
        ResType.Ute, ResType.Utm, ResType.Dlg, ResType.Uts, ResType.Ifo, ResType.Fac, ResType.Itp, ResType.Gui, ResType.Gff, ResType.Bic,
    };

    // json TemplateToJson(string sResRef, int nResType)
    [NwFunc("TemplateToJson")]
    static object? TemplateToJson(Call c)
    {
        ushort type = (ushort)c.I(1);
        // RESTYPE_CAF of a module area reads as null in the original too (persist_objects tmpl.caf); ObjectToJson makes CAF.
        if (!TemplateTypes.Contains(type)) return new JsonValue(null) { Error = "unsupported resource type" };
        var bytes = c.World.Resources.Read(c.S(0), type);
        if (bytes == null) return new JsonValue(null) { Error = $"template not found: {c.S(0)}.{ResType.Extension(type)}" };
        try
        {
            var root = Gff.Read(bytes, out var fileType);
            return new JsonValue(GffJson.ToJson(root, fileType));
        }
        catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException)
        {
            return new JsonValue(null) { Error = $"not a valid GFF resource: {e.Message}" };
        }
    }

    // int JsonToTemplate(json jTemplateSpec, string sResRef, int nResType)
    [NwFunc("JsonToTemplate")]
    static object? JsonToTemplate(Call c)
    {
        ushort type = (ushort)c.I(2);
        if (!TemplateTypes.Contains(type) || c.S(1).Length == 0) return 0;
        var sys = Sys(c);
        if (c.J(0).Node == null) { sys.SetTemplate(c.S(1), type, null); return 0; }
        try
        {
            var root = GffJson.FromJson(c.J(0).Node, out var fileType);
            if (fileType.Trim() is "GFF" or "") fileType = GffJson.FileTypeFor(type);
            sys.SetTemplate(c.S(1), type, Gff.Write(root, fileType));
            return 1;
        }
        catch (Exception e) when (e is FormatException or InvalidOperationException or OverflowException)
        {
            sys.SetTemplate(c.S(1), type, null);
            c.Host.Log($"[json] JsonToTemplate {c.S(1)}: {e.Message}");
            return 0;
        }
    }

    // ---- UUIDs ------------------------------------------------------------------------

    [NwFunc("GetObjectUUID")] static object? GetUuid(Call c) => c.Obj(0) is { } o ? Sys(c).GetUuid(o) : "";
    [NwFunc("ForceRefreshObjectUUID")] static object? RefreshUuid(Call c) { if (c.Obj(0) is { } o) Sys(c).RefreshUuid(o); return null; }
    [NwFunc("GetObjectByUUID")] static object? ByUuid(Call c) => Sys(c).ByUuid(c.S(0))?.Id ?? GameObject.InvalidId;

    // ---- Characters and saving ----------------------------------------------------------

    static bool Export(Call c, Creature pc)
    {
        try { ServerVault.Export(c.World, pc); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { c.Host.Log($"[vault] export of {pc} failed: {e.Message}"); return false; }
    }

    [NwFunc("ExportSingleCharacter")]
    static object? ExportSingle(Call c)
    {
        if (c.Obj<Creature>(0) is { IsPC: true } pc)
        {
            // On success the player reads TLK 10393 "Your character was exported successfully." (visual scene polymorphpc2,
            // single player). ExportAllCharacters: not measured.
            if (Export(c, pc)) c.World.Output?.SendMessage(pc, c.World.Resources.TlkString(10393));
        }
        return null;
    }

    [NwFunc("ExportAllCharacters")]
    static object? ExportAll(Call c)
    {
        foreach (var pc in Players.FamiliarPossession.Players(c.World).ToList()) Export(c, pc);
        return null;
    }

    // Single player autosaves after the current tick; on a server (save games off) it does nothing, like the original.
    [NwFunc("DoSinglePlayerAutoSave")] static object? AutoSave(Call c) { SaveGames.SaveGameRequests.RequestAutoSave(c.World); return null; }
}
