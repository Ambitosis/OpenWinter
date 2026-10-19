// Lit interior (tin01, a Chapter 1 house).
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VStandardViews(oPC);
}
