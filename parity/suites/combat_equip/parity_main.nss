// Which weapons a creature can equip by script (ActionEquipItem): an ogre, a hill giant, a halfling, a goblin, a
// human bandit, a badger and an imp, each given every kind of weapon in turn. Logs whether the weapon ends up in the
// right hand (-1: no such blueprint), and the creature's size. Also two giants.
#include "pcombat_inc"

string WEAPONS = "nw_wswdg001 nw_wswss001 nw_wswls001 nw_wswgs001 nw_wblcl001 nw_wblms001 nw_waxgr001 nw_wplhb001 nw_wdbqs001 nw_wbwxl001 nw_wbwxh001 nw_wbwsh001 nw_wbwln001 nw_wbwsl001 nw_wblhw001 nw_wspka001 nw_waxhn001 nw_wspku001 nw_wthsh001 nw_wthdt001 nw_wblml001 nw_wspsc001 nw_wblhl001 nw_wthax001 ";

void Try(object o, string sWeapon, string sKey)
{
    object w = CreateItemOnObject(sWeapon, o);
    SetIdentified(w, TRUE);
    AssignCommand(o, ClearAllActions(TRUE));
    AssignCommand(o, ActionEquipItem(w, INVENTORY_SLOT_RIGHTHAND));
    if (!GetIsObjectValid(w)) { PLogI(sKey, -1); return; }
    DelayCommand(1.0, PLogI(sKey, GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, o) == w));
    DelayCommand(1.2, DestroyObject(w));
}

void Creature(string sResRef, string sName, float fX)
{
    object o = PSpawn(sResRef, fX, fX > 4.0 && GetStringLeft(sName, 4) == "fire" || GetStringLeft(sName, 5) == "frost" ? 30.0 : 20.0);
    PUnequip(o, INVENTORY_SLOT_RIGHTHAND);
    PUnequip(o, INVENTORY_SLOT_LEFTHAND);
    PLogI(sName + ".size", GetCreatureSize(o));
    string s = WEAPONS;
    float t = 1.0;
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string w = GetStringLeft(s, sp);
        DelayCommand(t, Try(o, w, sName + "." + w));
        t += 1.5;
        s = GetSubString(s, sp + 1, 1000);
    }
}

void main()
{
    Creature("nw_ogre01", "ogre", 5.0);
    Creature("nw_gnthill", "hillgiant", 10.0);
    Creature("nw_halfling001", "halfling", 15.0);
    Creature("nw_goblina", "goblin", 20.0);
    Creature("nw_bandit001", "bandit", 25.0);
    Creature("nw_badger", "badger", 30.0);
    Creature("nw_imp", "imp", 35.0);
    Creature("nw_gntfire", "firegiant", 5.0);
    Creature("nw_gntfrost", "frostgiant", 10.0);
    DelayCommand(40.0, PDone());
}
