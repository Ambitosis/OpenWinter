#include "pbashed_inc"
void main()
{
    object d = OBJECT_SELF;
    string t = GetTag(d);
    BEv(d, "death", GetLastKiller());
    PLogI(t + ".indeath.valid", GetIsObjectValid(d));
    if (GetLocalInt(GetModule(), "BT_HPSIGN_" + t)) PLogI(t + ".indeath.hp.above0", GetCurrentHitPoints(d) > 0);
    else PLogI(t + ".indeath.hp", GetCurrentHitPoints(d));
    PLogI(t + ".indeath.open", GetIsOpen(d));
    PLogI(t + ".indeath.locked", GetLocked(d));
    // A bashed door's readings after its death are taken from here (the scripted kills schedule their own).
    if (GetLocalInt(GetModule(), "BT_FOLLOW_" + t)) BFollow(d, t);
}
