// Character creation and level-up in the original's interface, driven from the main menu by
// tools/visual/menudrive.py (New -> Other Modules -> this module -> New Character). A new character
// entering gets enough experience for level 4, so three level-ups (the fourth with the ability
// increase) can be taken through the level-up window.
#include "visual_inc"
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    if (GetXP(pc) < 6000) GiveXPToCreature(pc, 6000 - GetXP(pc));
    WriteTimestampedLogEntry("VISUAL|note|chargen|entered|xp=" + IntToString(GetXP(pc)) + "|hd=" + IntToString(GetHitDice(pc))
        + "|class=" + IntToString(GetClassByPosition(1, pc)) + "|race=" + IntToString(GetRacialType(pc)));
}
