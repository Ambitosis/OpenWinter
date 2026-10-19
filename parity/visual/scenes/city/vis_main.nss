// City exterior (tcn01, Chapter 1 Peninsula), day and night.
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VStandardViews(oPC);
}
