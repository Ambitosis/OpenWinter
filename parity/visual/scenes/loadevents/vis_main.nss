// loadevents (docs/SAVEGAME.md): a single-player autosave with known state, for loading back in
// the original (tools/visual/menudrive.py: Load Game) to see which events the load runs and what comes back.
// - Module OnModuleLoad, OnClientEnter and OnModuleStart are set by script (the save keeps them); this script is the
//   area's OnEnter and logs every entry.
// - An NPC's action queue: ActionWait(20), a marker, a move, a marker (ActionDoCommand). Saved 5 s into the wait.
// - A personal reputation: SetIsTemporaryEnemy(pc, npc2, TRUE, 60.0), saved 5 s in.
// - A DelayCommand of 40 s, and a 1 s log loop.
// The autosave comes at 6 s; the run goes on to 60 s as the uninterrupted control.
#include "visual_inc"
#include "le_inc"

void Mark(string s) { LeNote("mark|" + s); }
void Tick(int n)
{
    object a = GetObjectByTag("LE_N"), b = GetObjectByTag("LE_R");
    vector v = GetPosition(a);
    LeNote("tick|" + IntToString(n) + "|act=" + IntToString(GetCurrentAction(a)) + "|pos=" + FloatToString(v.x, 0, 2) + ","
        + FloatToString(v.y, 0, 2) + "|enemy=" + IntToString(GetIsEnemy(GetFirstPC(), b)) + "|rep=" + IntToString(GetReputation(b, GetFirstPC())));
    if (n < 90) DelayCommand(1.0, Tick(n + 1));
}
void Save() { LeNote("autosave"); DoSinglePlayerAutoSave(); }

void main()
{
    object pc = GetEnteringObject();
    object m = GetModule();
    LeNote("ev_area_enter|" + GetName(pc) + "|started=" + IntToString(GetLocalInt(m, "started")));
    if (!GetIsPC(pc) || GetLocalInt(m, "started")) return;
    SetLocalInt(m, "started", 1);
    SetEventScript(m, EVENT_SCRIPT_MODULE_ON_MODULE_LOAD, "le_load");
    SetEventScript(m, EVENT_SCRIPT_MODULE_ON_CLIENT_ENTER, "le_enter");
    SetEventScript(m, EVENT_SCRIPT_MODULE_ON_MODULE_START, "le_start");
    SetPlotFlag(pc, TRUE);
    object a = GetArea(pc);
    object n = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(a, Vector(14.0, 14.0, 0.0), 0.0), FALSE, "LE_N");
    object r = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(a, Vector(26.0, 14.0, 0.0), 0.0), FALSE, "LE_R");
    int i;
    for (i = 5000; i <= 5012; i++) { SetEventScript(n, i, ""); SetEventScript(r, i, ""); }
    ChangeToStandardFaction(n, STANDARD_FACTION_MERCHANT);
    ChangeToStandardFaction(r, STANDARD_FACTION_MERCHANT);
    AssignCommand(n, ActionWait(20.0));
    AssignCommand(n, ActionDoCommand(Mark("after_wait")));
    AssignCommand(n, ActionMoveToLocation(Location(a, Vector(14.0, 26.0, 0.0), 0.0)));
    AssignCommand(n, ActionDoCommand(Mark("after_move")));
    SetIsTemporaryEnemy(pc, r, TRUE, 60.0);
    DelayCommand(40.0, Mark("delayed_40"));
    Tick(0);
    DelayCommand(6.0, Save());
    DelayCommand(62.0, VDone());
}
