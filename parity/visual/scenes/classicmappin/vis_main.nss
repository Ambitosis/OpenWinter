// Authored input probe; each intermediate image is a required positive gate.
#include "visual_inc"
void Tap(int key) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(key) + "|tap"); }
void Click(string at) { WriteTimestampedLogEntry("VISUAL|click|" + at); }
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    DelayCommand(1.0, ExploreAreaForPlayer(GetArea(pc), pc, TRUE));
    DelayCommand(2.0, Tap(77));
    DelayCommand(3.0, VShot("01_map"));
    DelayCommand(4.0, Click("0.0148,0.1028"));
    DelayCommand(5.0, VShot("02_pin_cursor"));
    DelayCommand(6.0, Click("0.0352,0.1667"));
    DelayCommand(7.0, VShot("03_pin_dialog"));
    DelayCommand(8.0, Tap(80));
    DelayCommand(8.2, Tap(73));
    DelayCommand(8.4, Tap(78));
    DelayCommand(9.0, VShot("04_entered_text"));
    DelayCommand(10.0, Tap(13));
    DelayCommand(11.0, WriteTimestampedLogEntry("VISUAL|hover|0.0352,0.1667"));
    DelayCommand(12.0, VShot("05_pin_hover"));
    DelayCommand(13.0, ExportSingleCharacter(pc));
    DelayCommand(14.0, VDone());
}
