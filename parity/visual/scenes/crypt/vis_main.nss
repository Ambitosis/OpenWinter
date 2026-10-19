// Crypt (tdc01, Chapter 1).
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VStandardViews(oPC);
}
