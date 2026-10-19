// The Valsharess's throne room (Hordes of the Underdark's opening, tid01): no sun or moon, no ambient,
// lit only by its braziers.
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VStandardViews(oPC);
}
