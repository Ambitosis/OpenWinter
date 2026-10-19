// Shared helpers for the condition suites (cond_*): stripped actors, neighbours that count who attacks them,
// and per-actor sampling of the current action and position.
#include "parity_inc"

object CArea() { return GetObjectByTag("PARITY_AREA"); }

// A creature with no event scripts (unless bStock), AI normal, in a standard faction.
object CSpawn(float x, float y, string g, int nFaction, int bStock = FALSE, string sResRef = "nw_bandit001", string sTag = "")
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(CArea(), Vector(x, y, 0.0), 0.0), FALSE, sTag);
    int ev;
    if (!bStock) for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    SetAILevel(o, AI_LEVEL_NORMAL);
    ChangeToStandardFaction(o, nFaction);
    SetLocalString(o, "G", g);
    SetLocalFloat(o, "X0", x);
    SetLocalFloat(o, "Y0", y);
    return o;
}

// A neighbour that stands still, can't die, counts the attacks it takes (by attacker group) and heals.
object CNeighbour(float x, float y, string g, string sRole, int nFaction)
{
    object o = CSpawn(x, y, g, nFaction);
    SetLocalString(o, "R", sRole);
    SetImmortal(o, TRUE);
    SetEventScript(o, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "pc_cnatk");
    SetEventScript(o, EVENT_SCRIPT_CREATURE_ON_DAMAGED, "pc_cndmg");
    return o;
}

float CDist0(object o)
{
    vector v = GetPosition(o);
    float dx = v.x - GetLocalFloat(o, "X0"), dy = v.y - GetLocalFloat(o, "Y0");
    return sqrt(dx * dx + dy * dy);
}

void CAdd(string k, int n = 1)
{
    object m = GetModule();
    SetLocalInt(m, k, GetLocalInt(m, k) + n);
}

// One sample of an actor: its current action (as a bucket) and how far it is from its start.
void CSample(object o, string g)
{
    if (!GetIsObjectValid(o)) return;
    int a = GetCurrentAction(o);
    string b = a == ACTION_RANDOMWALK ? "walk" : a == ACTION_ATTACKOBJECT ? "attack" : a == ACTION_MOVETOPOINT ? "move"
        : a == ACTION_INVALID ? "none" : "other";
    CAdd(g + ".n");
    CAdd(g + "." + b);
    float d = CDist0(o);
    object m = GetModule();
    if (d > GetLocalFloat(m, g + ".maxd")) SetLocalFloat(m, g + ".maxd", d);
    SetLocalFloat(m, g + ".sumd", GetLocalFloat(m, g + ".sumd") + d);
    object t = GetAttackTarget(o);
    if (GetIsObjectValid(t)) CAdd(g + ".target." + GetLocalString(t, "R"));
}

void CRate(string g, string b, float fTol)
{
    object m = GetModule();
    int n = GetLocalInt(m, g + ".n");
    PLogF(g + "." + b, n > 0 ? IntToFloat(GetLocalInt(m, g + "." + b)) / IntToFloat(n) : 0.0, fTol);
}
