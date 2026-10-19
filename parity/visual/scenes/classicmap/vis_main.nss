// Fully explored authored areas isolate dimensions from fog coverage.
#include "visual_inc"
void Tap(int key) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(key) + "|tap"); }
void Click(string at) { WriteTimestampedLogEntry("VISUAL|click|" + at); }
void Mask(object pc)
{
    SetCreatureExploresMinimap(pc, FALSE);
    object area = GetArea(pc);
    int w = GetAreaSize(AREA_WIDTH, area), h = GetAreaSize(AREA_HEIGHT, area);
    int x, y;
    for (y = 0; y < h; y++) for (x = 0; x < w; x++) SetTileExplored(pc, area, x, y, x == 0 || y == 0);
    WriteTimestampedLogEntry("VISUAL|note|mask|L-shaped exploration only, not L-shaped geometry");
}
void MaskShot(object pc)
{
    object area = GetArea(pc);
    int w = GetAreaSize(AREA_WIDTH, area), h = GetAreaSize(AREA_HEIGHT, area);
    int x, y, count = 0, mismatches = 0;
    for (y = 0; y < h; y++) for (x = 0; x < w; x++)
    {
        int seen = GetTileExplored(pc, area, x, y);
        if (seen) count++;
        if (seen != (x == 0 || y == 0)) mismatches++;
    }
    WriteTimestampedLogEntry("VISUAL|note|mask-gate|auto=" + IntToString(GetCreatureExploresMinimap(pc))
        + "|count=" + IntToString(count) + "|expected=" + IntToString(w + h - 1) + "|mismatches=" + IntToString(mismatches));
    VShot("05_L_exploration");
}
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    object area = GetArea(pc);
    WriteTimestampedLogEntry("VISUAL|note|dimensions|" + IntToString(GetAreaSize(AREA_WIDTH, area)) + "x" + IntToString(GetAreaSize(AREA_HEIGHT, area)));
    SetPlotFlag(pc, TRUE);
    DelayCommand(1.0, ExploreAreaForPlayer(area, pc, TRUE));
    DelayCommand(2.0, VCamera(pc, 90.0, 8.0, 60.0));
    DelayCommand(3.0, Tap(77));
    DelayCommand(4.0, VShot("01_map_default"));
    // Header positions are candidates until the first valid map image confirms them.
    DelayCommand(5.0, Click("0.0273,0.1028"));
    DelayCommand(6.0, VShot("02_zoom_in"));
    DelayCommand(6.6, WriteTimestampedLogEntry("VISUAL|hover|0.0273,0.1028"));
    DelayCommand(7.0, WriteTimestampedLogEntry("VISUAL|mouse|0.0273,0.1028|down|left"));
    DelayCommand(7.2, WriteTimestampedLogEntry("VISUAL|mouse|0.0273,0.1028|up|left"));
    DelayCommand(8.0, VShot("03_zoom_in_twice"));
    DelayCommand(9.0, Click("0.0414,0.1028"));
    DelayCommand(10.0, VShot("04_zoom_out"));
    DelayCommand(11.0, Mask(pc));
    DelayCommand(12.0, MaskShot(pc));
    DelayCommand(13.0, VDone());
}
