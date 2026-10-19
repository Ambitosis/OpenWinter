// Encumbrance and movement (docs/ENCUMBRANCE.md): a Strength 10 goblin (encumbrance.2da row 10: Normal 860,
// Heavy 1100 tenths of a pound) carrying exact weights made of a 1-tenth probe item (p_weight, a cloned potion row
// with TenthLBS 1), walking and running. Weights at, just over and far over each limit; Strength raised and lowered
// by effects; weight added while running. Speeds are measured between 0.5 s and 2.5 s after the order, as
// movement_speed does, three runs each, median reported. The original keeps no carried weight for creatures that
// aren't players: GetWeight reads 0 (the items are there: .itemweight) and no load slows them.
#include "parity_inc"

object Area() { return GetObjectByTag("PARITY_AREA"); }
location L(float x, float y) { return Location(Area(), Vector(x, y, 0.0), 0.0); }

float Median(float a, float b, float c)
{
    if (a > b) { float t = a; a = b; b = t; }
    if (b > c) b = c;
    return a > b ? a : b;
}

void Mark(object o) { SetLocalFloat(o, "X0", GetPosition(o).x); }

void Speed(object o, string k)
{
    object m = GetModule();
    float v = (GetPosition(o).x - GetLocalFloat(o, "X0")) / 2.0;
    int n = GetLocalInt(m, "RUN_" + k) + 1;
    SetLocalInt(m, "RUN_" + k, n);
    SetLocalFloat(m, "V" + IntToString(n) + "_" + k, v);
    if (n < 3) return;
    v = Median(GetLocalFloat(m, "V1_" + k), GetLocalFloat(m, "V2_" + k), v);
    PLogF(k + ".speed", v, 0.01 + v * 0.015);
}

// Adds probe weight until the creature carries nTotal tenths (or nothing when nTotal is below what it has).
void Load(object o, int nTotal)
{
    int nNeed = nTotal - GetWeight(o);
    while (nNeed > 0)
    {
        int n = nNeed > 9000 ? 9000 : nNeed;
        CreateItemOnObject("p_weight", o, n);
        nNeed -= n;
    }
}

void Report(object o, string k)
{
    object m = GetModule();
    if (GetLocalInt(m, "REPORTED_" + k)) return;
    SetLocalInt(m, "REPORTED_" + k, 1);
    PLogI(k + ".weight", GetWeight(o));
    int nItems = 0, nSum = 0;
    object it = GetFirstItemInInventory(o);
    while (GetIsObjectValid(it)) { nItems++; nSum += GetWeight(it); it = GetNextItemInInventory(o); }
    PLogI(k + ".items", nItems);
    PLogI(k + ".itemweight", nSum);
    PLogI(k + ".str", GetAbilityScore(o, ABILITY_STRENGTH));
    PLogI(k + ".rate", GetMovementRate(o));
}

void Order(object o, float y, int bRun)
{
    AssignCommand(o, ActionMoveToLocation(L(57.0, y), bRun));
}

// nStr: 0 none, else an ability effect of that many points (negative lowers Strength).
void Lane(string k, float y, int bRun, int nTotal, int nStr)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "p_mover", L(2.0, y), FALSE, k);
    SetAILevel(o, AI_LEVEL_NORMAL);
    if (nStr > 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_STRENGTH, nStr), o);
    if (nStr < 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityDecrease(ABILITY_STRENGTH, -nStr), o);
    Load(o, nTotal);
    DelayCommand(0.8, Report(o, k));
    DelayCommand(1.0, Order(o, y, bRun));
    DelayCommand(1.5, Mark(o));
    DelayCommand(3.5, Speed(o, k));
    DelayCommand(4.0, DestroyObject(o));
}

// Running unloaded; at 1 s into the run the creature picks up 2000 tenths. Speed from 2 s to 4 s after the order.
void LateLoad(object o) { Load(o, 2000); }
void Late(string k, float y)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "p_mover", L(2.0, y), FALSE, k);
    SetAILevel(o, AI_LEVEL_NORMAL);
    DelayCommand(1.0, Order(o, y, TRUE));
    DelayCommand(2.0, LateLoad(o));
    DelayCommand(3.0, Mark(o));
    DelayCommand(5.0, Speed(o, k));
    DelayCommand(5.5, DestroyObject(o));
}

void Pair(string k, float y, int nTotal, int nStr)
{
    Lane(k + ".walk", y, FALSE, nTotal, nStr);
    Lane(k + ".run", y + 1.5, TRUE, nTotal, nStr);
}

void Lanes()
{
    Pair("w0", 1.5, 0, 0);          // what it carries anyway
    Pair("w860", 4.5, 860, 0);      // = Normal
    Pair("w861", 7.5, 861, 0);      // Normal + 1
    Pair("w1100", 10.5, 1100, 0);   // = Heavy
    Pair("w1101", 13.5, 1101, 0);   // Heavy + 1
    Pair("w2200", 16.5, 2200, 0);   // twice Heavy
    Pair("w5500", 19.5, 5500, 0);   // five times Heavy
    Pair("str14.w1000", 22.5, 1000, 4);   // Strength 14 by effect: Normal 1160
    Pair("str6.w650", 25.5, 650, -4);     // Strength 6 by effect: Normal 600, Heavy 700
    Pair("str6.w701", 28.5, 701, -4);
    Late("late.run", 31.5);
}

void Table()
{
    int i;
    for (i = 8; i <= 12; i++)
        PLog("2da." + IntToString(i), Get2DAString("encumbrance", "Normal", i) + "/" + Get2DAString("encumbrance", "Heavy", i));
    object o = CreateObject(OBJECT_TYPE_ITEM, "p_weight", L(30.0, 40.0), FALSE);
    SetItemStackSize(o, 1);
    PLogI("probe.1", GetWeight(o));
    SetItemStackSize(o, 250);
    PLogI("probe.250", GetWeight(o));
    PLogI("probe.250.stack", GetItemStackSize(o));
    DestroyObject(o);
}

void main()
{
    DelayCommand(1.0, Table());
    DelayCommand(2.0, Lanes());
    DelayCommand(10.0, Lanes());
    DelayCommand(18.0, Lanes());
    DelayCommand(26.0, PDone());
}
