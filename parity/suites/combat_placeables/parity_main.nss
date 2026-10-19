// Attack approach must reach a hittable point, including offset PWK use points.
#include "pcombat_inc"

void Report(object a, object t, string key)
{
    vector d = GetPosition(a) - GetPosition(t);
    PLogI(key + ".attacks", GetLocalInt(t, "hits") > 0);
    PLogI(key + ".action", GetCurrentAction(a));
    // Native picks different sides around furniture; test arrival range and attacks,
    // and retain exact positions as diagnostics rather than claiming route parity.
    PLogF(key + ".distance", GetDistanceBetween(a, t), 0.25);
    PrintString("BASH_POSITION|" + key + "|" + FloatToString(d.x) + "," + FloatToString(d.y));
}

void Pair(string name, int cell, int side)
{
    object area = GetObjectByTag("PARITY_AREA");
    vector p = Vector(10.0 + IntToFloat(cell % 4) * 18.0, 10.0 + IntToFloat(cell / 4) * 18.0, 0.0);
    float angle = IntToFloat(side) * 90.0;
    vector offset = Vector(cos(angle) * 6.0, sin(angle) * 6.0, 0.0);
    object t = CreateObject(OBJECT_TYPE_PLACEABLE, "p_" + name, Location(area, p, 0.0));
    object a = CreateObject(OBJECT_TYPE_CREATURE, "nw_drowfight010", Location(area, p + offset, angle + 180.0));
    PStrip(a);
    SetAILevel(a, AI_LEVEL_NORMAL);
    PUnequip(a, INVENTORY_SLOT_RIGHTHAND);
    PEquip(a, "nw_wswls001", INVENTORY_SLOT_RIGHTHAND);
    AssignCommand(a, ActionAttack(t));
    DelayCommand(12.0, Report(a, t, name + IntToString(side)));
}

void main()
{
    int i;
    for (i = 0; i < 4; i++)
    {
        Pair("chest", i, i);
        Pair("desk", i + 4, i);
        Pair("armoire", i + 8, i);
        Pair("table", i + 12, i);
    }
    DelayCommand(13.0, PDone());
}
