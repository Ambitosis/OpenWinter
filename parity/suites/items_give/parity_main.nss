// ActionGiveItem, ActionTakeItem, ActionPutDownItem and ActionPickUpItem: range, other areas, containers,
// what the action queue does around them, and when the item moves. Tests run one after another, each with
// its own creatures (spawned 1.5 s before acting). "Where" is the item's possessor, the actor's current
// action and whether the actor moved; Track polls until the item moves and logs when and how far apart.
#include "parity_itemev"

const float SLOT = 6.0;

object Npc(string sTag, location l)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", l, FALSE, sTag);
    int i;
    for (i = 5000; i <= 5012; i++) SetEventScript(o, i, "");
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

location At(object oArea, float x, float y) { return Location(oArea, Vector(x, y, 0.0), 0.0); }

string Where(object it)
{
    if (!GetIsObjectValid(it)) return "gone";
    object p = GetItemPossessor(it);
    if (GetIsObjectValid(p)) return PEvName(p);
    return GetIsObjectValid(GetArea(it)) ? "ground" : "nowhere";
}

void Sample(string k, object actor, object it, location lStart)
{
    float d = GetDistanceBetweenLocations(lStart, GetLocation(actor));
    PLog(k, Where(it) + " action=" + IntToString(GetCurrentAction(actor)) + " moved=" + IntToString(d > 0.2 || d < 0.0));
}

// Polls every 0.1 s for 5 s: when the item leaves sFrom, logs the time and the distance between actor and other.
void Track(string k, object actor, object other, object it, string sFrom, int n)
{
    if (Where(it) != sFrom)
    {
        float d = GetArea(actor) == GetArea(other) ? GetDistanceBetween(actor, other) : -1.0;
        PLogF(k + ".when", IntToFloat(n) / 10.0, 0.35);
        PLogF(k + ".dist", d, 0.6);
        return;
    }
    if (n >= 50) { PLog(k + ".when", "never"); return; }
    DelayCommand(0.1, Track(k, actor, other, it, sFrom, n + 1));
}

void Clear() { PEvFlush(); }

void Watch(string k, object actor, object other, object it)
{
    location l = GetLocation(actor);
    Sample(k + ".t0", actor, it, l);
    DelayCommand(0.05, Sample(k + ".t005", actor, it, l));
    DelayCommand(0.3, Sample(k + ".t03", actor, it, l));
    DelayCommand(1.0, Sample(k + ".t1", actor, it, l));
    DelayCommand(5.0, Sample(k + ".t5", actor, it, l));
    DelayCommand(0.0, Track(k, actor, other, it, Where(it), 0));
    DelayCommand(5.2, PEvLog(k + ".ev"));
}

void Marker(string s) { PEvAdd("[" + s + "]"); }

// The next test's row: y of the row, and X0() its left end (a second column from row 20).
int Row()
{
    int n = GetLocalInt(GetModule(), "row");
    SetLocalInt(GetModule(), "row", n + 1);
    SetLocalFloat(GetModule(), "x0", n >= 20 ? 40.0 : 3.0);
    return n >= 20 ? n - 20 : n;
}

float X0() { return GetLocalFloat(GetModule(), "x0"); }

// ---------------------------------------------------------------- tests

// A giver at x=3 and a receiver fDist metres to the right (in another area when bOther).
void Give(string k, float fDist, int bTake, int bOther)
{
    object a = GetObjectByTag("PARITY_AREA");
    float y = 3.0 + 2.5 * IntToFloat(Row());
    object giver = Npc("G_" + k, At(a, X0(), y));
    object recv = Npc("R_" + k, At(bOther ? GetObjectByTag("PARITY2") : a, X0() + fDist, y));
    object it = CreateItemOnObject("nw_it_mring001", bTake ? recv : giver, 1, "I_" + k);
    DelayCommand(1.4, Clear());
    if (bTake) DelayCommand(1.5, AssignCommand(giver, ActionTakeItem(it, recv)));
    else DelayCommand(1.5, AssignCommand(giver, ActionGiveItem(it, recv)));
    DelayCommand(1.5, Watch(k, giver, recv, it));
}

// Give at 12 m, with something queued before or after.
void Queue(string k, int nKind)
{
    object a = GetObjectByTag("PARITY_AREA");
    float y = 3.0 + 2.5 * IntToFloat(Row());
    object giver = Npc("G_" + k, At(a, X0(), y));
    object recv = Npc("R_" + k, At(a, X0() + 12.0, y));
    object it = CreateItemOnObject("nw_it_mring001", giver, 1, "I_" + k);
    object it2 = CreateItemOnObject("nw_it_mring002", giver, 1, "I2_" + k);
    object other = CreateItemOnObject("nw_it_mring001", recv, 1, "IO_" + k);
    DelayCommand(1.4, Clear());
    if (nKind == 1)
    {
        DelayCommand(1.5, AssignCommand(giver, ActionWait(2.0)));
        DelayCommand(1.5, AssignCommand(giver, ActionGiveItem(it, recv)));
        DelayCommand(1.5, AssignCommand(giver, ActionDoCommand(Marker(k + ".after"))));
    }
    else if (nKind == 2)
    {
        DelayCommand(1.5, AssignCommand(giver, ActionGiveItem(it, recv)));
        DelayCommand(1.5, AssignCommand(giver, ActionMoveToLocation(At(a, X0(), y + 1.0))));
    }
    else if (nKind == 3)
    {
        // Something it can't give, then something it can.
        DelayCommand(1.5, AssignCommand(giver, ActionGiveItem(other, recv)));
        DelayCommand(1.5, AssignCommand(giver, ActionDoCommand(Marker(k + ".after"))));
        DelayCommand(1.5, AssignCommand(giver, ActionGiveItem(it, recv)));
    }
    else if (nKind == 4)
    {
        // Two in a row, then one taken back.
        DelayCommand(1.5, AssignCommand(giver, ActionGiveItem(it, recv)));
        DelayCommand(1.5, AssignCommand(giver, ActionGiveItem(it2, recv)));
        DelayCommand(1.5, AssignCommand(giver, ActionTakeItem(other, recv)));
        DelayCommand(1.6, PLog(k + ".more", Where(it2) + " " + Where(other)));
    }
    else if (nKind == 5)
    {
        // A dead receiver.
        DelayCommand(1.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), recv));
        DelayCommand(1.5, AssignCommand(giver, ActionGiveItem(it, recv)));
    }
    else if (nKind == 6)
    {
        // Giving to itself, to the module, to an item on the ground, and an item from the ground.
        object g = CreateObject(OBJECT_TYPE_ITEM, "nw_it_mring001", At(a, X0() + 1.0, y), FALSE, "IG_" + k);
        DelayCommand(1.5, AssignCommand(giver, ActionGiveItem(it, giver)));
        DelayCommand(1.5, AssignCommand(giver, ActionGiveItem(it2, GetModule())));
        DelayCommand(1.5, AssignCommand(giver, ActionGiveItem(it2, g)));
        DelayCommand(1.5, AssignCommand(giver, ActionGiveItem(g, recv)));
        DelayCommand(1.5, AssignCommand(giver, ActionDoCommand(Marker(k + ".after"))));
        DelayCommand(2.6, PLog(k + ".more", Where(it2) + " " + Where(g)));
    }
    else if (nKind == 7)
    {
        // Giving the weapon in its main hand.
        object w = GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, giver);
        DelayCommand(1.5, AssignCommand(giver, ActionGiveItem(w, recv)));
        DelayCommand(1.6, PLog(k + ".more", Where(w) + " slot=" + PEvName(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, giver))));
    }
    DelayCommand(1.5, Watch(k, giver, recv, it));
}

// Bags: giving an item from the giver's bag, taking one from the receiver's bag, giving into a bag, giving a bag.
void Bags(string k, int nKind)
{
    object a = GetObjectByTag("PARITY_AREA");
    float y = 3.0 + 2.5 * IntToFloat(Row());
    object giver = Npc("G_" + k, At(a, X0(), y));
    object recv = Npc("R_" + k, At(a, X0() + 12.0, y));
    object bag = CreateItemOnObject("nw_it_contain001", nKind == 1 ? recv : giver, 1, "B_" + k);
    object it = nKind == 2 ? CreateItemOnObject("nw_it_mring001", giver, 1, "I_" + k) : CreateItemOnObject("nw_it_mring001", bag, 1, "I_" + k);
    DelayCommand(1.4, Clear());
    if (nKind == 0) DelayCommand(1.5, AssignCommand(giver, ActionGiveItem(it, recv)));
    if (nKind == 1) DelayCommand(1.5, AssignCommand(giver, ActionTakeItem(it, recv)));
    if (nKind == 2) DelayCommand(1.5, AssignCommand(giver, ActionGiveItem(it, bag)));
    if (nKind == 3) DelayCommand(1.5, AssignCommand(giver, ActionGiveItem(bag, recv)));
    DelayCommand(1.5, Watch(k, giver, recv, it));
    DelayCommand(5.6, PLog(k + ".bag", Where(bag) + " in=" + PEvName(GetItemPossessor(it, TRUE))));
}

// Placeables: a creature giving to / taking from a chest 12 m away, and the chest giving and taking.
void Chests(string k, int nKind)
{
    object a = GetObjectByTag("PARITY_AREA");
    float y = 3.0 + 2.5 * IntToFloat(Row());
    object c = Npc("G_" + k, At(a, X0(), y));
    object chest = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", At(a, X0() + 12.0, y), FALSE, "C_" + k);
    object it = CreateItemOnObject("nw_it_mring001", nKind == 1 || nKind == 2 ? chest : c, 1, "I_" + k);
    DelayCommand(1.4, Clear());
    if (nKind == 0) { DelayCommand(1.5, AssignCommand(c, ActionGiveItem(it, chest))); DelayCommand(1.5, Watch(k, c, chest, it)); }
    if (nKind == 1) { DelayCommand(1.5, AssignCommand(c, ActionTakeItem(it, chest))); DelayCommand(1.5, Watch(k, c, chest, it)); }
    if (nKind == 2) { DelayCommand(1.5, AssignCommand(chest, ActionGiveItem(it, c))); DelayCommand(1.5, Watch(k, chest, c, it)); }
    if (nKind == 3) { DelayCommand(1.5, AssignCommand(chest, ActionTakeItem(it, c))); DelayCommand(1.5, Watch(k, chest, c, it)); }
}

// Putting down and picking up: when the item moves, near and 8 m away.
void Ground(string k, int nKind)
{
    object a = GetObjectByTag("PARITY_AREA");
    float y = 3.0 + 2.5 * IntToFloat(Row());
    object c = Npc("G_" + k, At(a, X0(), y));
    object it = nKind == 0 ? CreateItemOnObject("nw_it_mring001", c, 1, "I_" + k)
        : CreateObject(OBJECT_TYPE_ITEM, "nw_it_mring001", At(a, X0() + (nKind == 1 ? 0.5 : 8.0), y), FALSE, "I_" + k);
    DelayCommand(1.4, Clear());
    if (nKind == 0) DelayCommand(1.5, AssignCommand(c, ActionPutDownItem(it)));
    else DelayCommand(1.5, AssignCommand(c, ActionPickUpItem(it)));
    DelayCommand(1.5, Watch(k, c, c, it));
}

void Test(int n)
{
    if (n == 0) Give("near", 1.5, FALSE, FALSE);
    else if (n == 1) Give("g12", 12.0, FALSE, FALSE);
    else if (n == 2) Give("g30", 30.0, FALSE, FALSE);
    else if (n == 3) Give("garea", 0.0, FALSE, TRUE);
    else if (n == 4) Give("tnear", 1.5, TRUE, FALSE);
    else if (n == 5) Give("t5", 5.0, TRUE, FALSE);
    else if (n == 6) Give("t12", 12.0, TRUE, FALSE);
    else if (n == 7) Give("tarea", 0.0, TRUE, TRUE);
    else if (n == 8) Queue("qwait", 1);
    else if (n == 9) Queue("qmove", 2);
    else if (n == 10) Queue("qfail", 3);
    else if (n == 11) Queue("qmany", 4);
    else if (n == 12) Queue("qdead", 5);
    else if (n == 13) Queue("qodd", 6);
    else if (n == 14) Queue("qequip", 7);
    else if (n == 15) Bags("bgive", 0);
    else if (n == 16) Bags("btake", 1);
    else if (n == 17) Bags("binto", 2);
    else if (n == 18) Bags("bwhole", 3);
    else if (n == 19) Chests("cgive", 0);
    else if (n == 20) Chests("ctake", 1);
    else if (n == 21) Chests("chgive", 2);
    else if (n == 22) Chests("chtake", 3);
    else if (n == 23) Ground("put", 0);
    else if (n == 24) Ground("pick", 1);
    else if (n == 25) Ground("pick8", 2);
    if (n < 25) DelayCommand(SLOT + 1.0, Test(n + 1));
    else DelayCommand(SLOT + 1.0, PDone());
}

void main()
{
    CreateArea("parity", "PARITY2", "Other");
    DelayCommand(1.0, Test(0));
}
