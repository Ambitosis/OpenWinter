using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Scripting;

/// <summary>Public EVENT_SCRIPT_* identities and the corresponding object blueprint fields.</summary>
public static class EventScripts
{
    static readonly Dictionary<int, string> ModuleEvents = new()
    {
        [3000] = "Mod_OnHeartbeat", [3001] = "Mod_OnUsrDefined", [3002] = "Mod_OnModLoad", [3003] = "Mod_OnModStart",
        [3004] = "Mod_OnClientEntr", [3005] = "Mod_OnClientLeav", [3006] = "Mod_OnActvtItem", [3007] = "Mod_OnAcquirItem",
        [3008] = "Mod_OnUnAqreItem", [3009] = "Mod_OnPlrDeath", [3010] = "Mod_OnPlrDying", [3011] = "Mod_OnSpawnBtnDn",
        [3012] = "Mod_OnPlrRest", [3013] = "Mod_OnPlrLvlUp", [3014] = "Mod_OnCutsnAbort", [3015] = "Mod_OnPlrEqItm",
        [3016] = "Mod_OnPlrUnEqItm", [3017] = "Mod_OnPlrChat", [3018] = "Mod_OnPlrTarget", [3019] = "Mod_OnPlrGuiEvt",
        [3020] = "Mod_OnPlrTileAct", [3021] = "Mod_OnNuiEvent",
    };
    static readonly Dictionary<int, string> AreaEvents = new()
    {
        [4000] = "OnHeartbeat", [4001] = "OnUserDefined", [4002] = "OnEnter", [4003] = "OnExit",
    };
    static readonly Dictionary<int, string> AoeEvents = new()
    {
        [11000] = "OnHeartbeat", [11001] = "OnUserDefined", [11002] = "OnObjEnter", [11003] = "OnObjExit",
    };
    static readonly Dictionary<int, string> CreatureEvents = new()
    {
        [5000] = "ScriptHeartbeat", [5001] = "ScriptOnNotice", [5002] = "ScriptSpellAt", [5003] = "ScriptAttacked",
        [5004] = "ScriptDamaged", [5005] = "ScriptDisturbed", [5006] = "ScriptEndRound", [5007] = "ScriptDialogue",
        [5008] = "ScriptSpawn", [5009] = "ScriptRested", [5010] = "ScriptDeath", [5011] = "ScriptUserDefine",
        [5012] = "ScriptOnBlocked",
    };
    static readonly Dictionary<int, string> TriggerEvents = new()
    {
        [7000] = "ScriptHeartbeat", [7001] = "ScriptOnEnter", [7002] = "ScriptOnExit", [7003] = "ScriptUserDefine",
        [7004] = "OnTrapTriggered", [7005] = "OnDisarm", [7006] = "OnClick",
    };
    static readonly Dictionary<int, string> PlaceableEvents = new()
    {
        [9000] = "OnClosed", [9001] = "OnDamaged", [9002] = "OnDeath", [9003] = "OnDisarm", [9004] = "OnHeartbeat",
        [9005] = "OnInvDisturbed", [9006] = "OnLock", [9007] = "OnMeleeAttacked", [9008] = "OnOpen", [9009] = "OnSpellCastAt",
        [9010] = "OnTrapTriggered", [9011] = "OnUnlock", [9012] = "OnUsed", [9013] = "OnUserDefined", [9014] = "OnDialog",
        [9015] = "OnClick",
    };
    static readonly Dictionary<int, string> DoorEvents = new()
    {
        [10000] = "OnOpen", [10001] = "OnClosed", [10002] = "OnDamaged", [10003] = "OnDeath", [10004] = "OnDisarm",
        [10005] = "OnHeartbeat", [10006] = "OnLock", [10007] = "OnMeleeAttacked", [10008] = "OnSpellCastAt",
        [10009] = "OnTrapTriggered", [10010] = "OnUnlock", [10011] = "OnUserDefined", [10012] = "OnClick",
        [10013] = "OnDialog", [10014] = "OnFailToOpen",
    };
    static readonly Dictionary<int, string> EncounterEvents = new()
    {
        [13000] = "OnEntered", [13001] = "OnExit", [13002] = "OnHeartbeat", [13003] = "OnExhausted", [13004] = "OnUserDefined",
    };
    static readonly Dictionary<int, string> StoreEvents = new()
    {
        [14000] = "OnOpenStore", [14001] = "OnStoreClosed",
    };

    static Dictionary<int, string>? Table(GameObject o)
    {
        return o switch
        {
            Module => ModuleEvents,
            Area => AreaEvents,
            Creature => CreatureEvents,
            Trigger => TriggerEvents,
            Placeable => PlaceableEvents,
            Door => DoorEvents,
            Encounter => EncounterEvents,
            Store => StoreEvents,
            _ when o.Type == ObjectType.AreaOfEffect => AoeEvents,
            _ => null,
        };
    }

    public static string? Field(GameObject o, int handler) =>
        Table(o) is { } table && table.TryGetValue(handler, out var field) ? field : null;

    public static int Type(GameObject o, string field) =>
        Table(o)?.FirstOrDefault(pair => pair.Value == field).Key ?? 0;
}
