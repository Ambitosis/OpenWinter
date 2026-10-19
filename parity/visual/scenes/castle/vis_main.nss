// Castle interior (tic01, the Prelude academy).
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VStandardViews(oPC);
}
