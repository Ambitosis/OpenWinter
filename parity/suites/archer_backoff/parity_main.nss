// Archer backing away: the server has no built-in back-off. A drow archer (short bow, one attack) with NO event
// scripts at all attacks a passive, immortal bandit with no event scripts either, from 1.5 m (6), 3 m and 6 m (4 each). Distance to the target and from the start are
// sampled. Then ActionMoveAwayFromObject on its own: an actor 2 m from a bystander, range 5 m and the default
// (40 m), with nothing else queued.
#include "parity_inc"

object Area() { return GetObjectByTag("PARITY_AREA"); }
int nCell = 0;

void Strip(object o)
{
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    AssignCommand(o, ClearAllActions(TRUE));
    SetAILevel(o, AI_LEVEL_NORMAL);
}

object Make(string sRes, float x, float y, float f, int nFaction)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, Location(Area(), Vector(x, y, 0.0), f));
    Strip(o);
    ChangeToStandardFaction(o, nFaction);
    SetImmortal(o, TRUE);
    return o;
}

void Equip(object o, string sRes, int nSlot, int nStack = 1)
{
    object it = CreateItemOnObject(sRes, o, nStack);
    SetIdentified(it, TRUE);
    AssignCommand(o, ActionEquipItem(it, nSlot));
}

void Sample(string g, string sAt)
{
    object m = GetModule();
    int n = GetLocalInt(m, g + ".count"), i;
    float moved = 0.0, apart = 0.0;
    for (i = 0; i < n; i++)
    {
        object a = GetLocalObject(m, g + IntToString(i));
        moved += GetDistanceBetweenLocations(GetLocation(a), GetLocalLocation(a, "L0"));
        apart += GetDistanceBetween(a, GetLocalObject(a, "T"));
    }
    int back = 0;
    for (i = 0; i < n; i++)
    {
        object a = GetLocalObject(m, g + IntToString(i));
        if (GetAttackTarget(GetLocalObject(a, "T")) == a) back++;
    }
    PLogI(g + ".fightsBack." + sAt, back > 0);
    // Once its target fights it, how far an archer gets varies a lot between nwserver runs (close.moved.27s 5.1-7.2 m
    // over five runs of six archers), so those readings get a wider tolerance.
    // 2.5 s after the shot ("3s") about half have taken their first step, so a mean over four varies by about 0.3 m.
    float tol = g == "close" && sAt != "1s" && sAt != "3s" ? 1.5 : sAt == "3s" ? 1.0 : 0.6;
    PLogF(g + ".moved." + sAt, moved / IntToFloat(n), tol);
    PLogF(g + ".apart." + sAt, apart / IntToFloat(n), tol);
}

// A trace of the first archer of a group: its distance from its target every 0.25 s ("RAW" lines, uncompared).
void Trace(object a, float t)
{
    if (t > 16.0) { WriteTimestampedLogEntry("RAW|" + GetLocalString(a, "G") + ".trace|" + GetLocalString(a, "TR")); return; }
    SetLocalString(a, "TR", GetLocalString(a, "TR") + FloatToString(GetDistanceBetween(a, GetLocalObject(a, "T")), 0, 2) + "@" + FloatToString(t, 0, 2) + " ");
    DelayCommand(0.25, Trace(a, t + 0.25));
}

void Archer(string g, float fDist)
{
    // 16 m apart: an archer backing away from a target that fights it can cover 6 m.
    float x = 8.0 + IntToFloat(nCell % 6) * 16.0, y = 8.0 + IntToFloat(nCell / 6) * 16.0;
    nCell++;
    object a = Make("nw_drowfight010", x, y, 0.0, STANDARD_FACTION_HOSTILE);
    object t = Make("nw_bandit001", x + fDist, y, 180.0, STANDARD_FACTION_COMMONER);
    SetBaseAttackBonus(1, a);
    object it = GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, a);
    if (GetIsObjectValid(it)) DestroyObject(it);
    it = GetItemInSlot(INVENTORY_SLOT_LEFTHAND, a);
    if (GetIsObjectValid(it)) DestroyObject(it);
    Equip(a, "nw_wbwsh001", INVENTORY_SLOT_RIGHTHAND);
    Equip(a, "nw_wamar001", INVENTORY_SLOT_ARROWS, 99);
    SetLocalObject(a, "T", t);
    SetLocalLocation(a, "L0", GetLocation(a));
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "ab_atk");
    object m = GetModule();
    int n = GetLocalInt(m, g + ".count");
    SetLocalObject(m, g + IntToString(n), a);
    SetLocalInt(m, g + ".count", n + 1);
    SetLocalString(a, "G", g);
    DelayCommand(2.0, AssignCommand(a, ActionAttack(t)));
    if (n == 0) DelayCommand(2.0, Trace(a, 2.0));
}

void Away(string g, float fRange, int bDefault)
{
    float x = 10.0, y = 60.0 + IntToFloat(GetLocalInt(GetModule(), "AWAYS")) * 20.0;
    SetLocalInt(GetModule(), "AWAYS", GetLocalInt(GetModule(), "AWAYS") + 1);
    object b = Make("nw_bandit001", x, y, 0.0, STANDARD_FACTION_COMMONER);
    object a = Make("nw_bandit001", x + 2.0, y, 0.0, STANDARD_FACTION_COMMONER);
    if (bDefault) DelayCommand(2.0, AssignCommand(a, ActionMoveAwayFromObject(b, TRUE)));
    else DelayCommand(2.0, AssignCommand(a, ActionMoveAwayFromObject(b, TRUE, fRange)));
    DelayCommand(3.0, PLogF(g + ".d1", GetDistanceBetween(a, b), 0.8));
    DelayCommand(6.0, PLogF(g + ".d4", GetDistanceBetween(a, b), 1.5));
    DelayCommand(6.0, PLogI(g + ".action4", GetCurrentAction(a)));
    DelayCommand(14.0, PLogF(g + ".d12", GetDistanceBetween(a, b), 1.5));
    DelayCommand(14.0, PLogI(g + ".action12", GetCurrentAction(a)));
}

void main()
{
    int i;
    for (i = 0; i < 6; i++) Archer("close", 1.5);
    for (i = 0; i < 4; i++)
    {
        Archer("near", 3.0);
        Archer("far", 6.0);
    }
    string s = "close near far ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        DelayCommand(3.0, Sample(g, "1s"));
        DelayCommand(5.0, Sample(g, "3s"));
        DelayCommand(9.0, Sample(g, "7s"));
        DelayCommand(15.0, Sample(g, "13s"));
        DelayCommand(21.0, Sample(g, "19s"));
        DelayCommand(29.0, Sample(g, "27s"));
        s = GetSubString(s, sp + 1, 1000);
    }
    Away("away5", 5.0, FALSE);
    Away("away40", 0.0, TRUE);
    DelayCommand(30.0, WriteTimestampedLogEntry("RAW|shots|" + GetLocalString(GetModule(), "SHOTS")));
    DelayCommand(30.0, PDone());
}
