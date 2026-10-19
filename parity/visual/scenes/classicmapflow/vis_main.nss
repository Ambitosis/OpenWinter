#include "visual_inc"
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    ExploreAreaForPlayer(GetArea(pc), pc, TRUE);
    SetCreatureExploresMinimap(pc, FALSE);
    WriteTimestampedLogEntry("VISUAL|note|context|map-flow-size=" + IntToString(GetAreaSize(AREA_WIDTH, GetArea(pc))) + "x" + IntToString(GetAreaSize(AREA_HEIGHT, GetArea(pc))));
}
