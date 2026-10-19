#include "visual_inc"
void Tap(int key) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(key) + "|tap"); }
void Hover(string at) { WriteTimestampedLogEntry("VISUAL|hover|" + at); }
void Click(string at) { WriteTimestampedLogEntry("VISUAL|click|" + at); }
void Export(object pc) { ExportSingleCharacter(pc); WriteTimestampedLogEntry("VISUAL|note|export|" + GetName(pc)); }
void Save() { DoSinglePlayerAutoSave(); WriteTimestampedLogEntry("VISUAL|note|autosave|requested"); }
void main() {
    object pc=GetEnteringObject(); if(!GetIsPC(pc)) return;
    SetPlotFlag(pc,TRUE); ExploreAreaForPlayer(GetArea(pc),pc,TRUE); SetCreatureExploresMinimap(pc,FALSE);
    DelayCommand(1.0,Tap(74)); DelayCommand(2.0,Tap(73));
    DelayCommand(3.0,VShot("01_journal_left_inventory_right"));
    DelayCommand(4.0,Tap(74)); DelayCommand(5.0,VShot("02_inventory_survivor"));
    DelayCommand(6.0,Tap(73)); DelayCommand(7.0,Tap(77));
    DelayCommand(8.0,VShot("03_map"));
    DelayCommand(8.7,Hover("0.0148,0.1028")); DelayCommand(9.0,Click("0.0148,0.1028"));
    DelayCommand(9.7,Hover("0.0352,0.1667")); DelayCommand(10.0,Click("0.0352,0.1667"));
    DelayCommand(11.0,Tap(69)); DelayCommand(11.2,Tap(76)); DelayCommand(11.4,Tap(89));
    DelayCommand(11.6,Tap(80)); DelayCommand(11.8,Tap(73)); DelayCommand(12.0,Tap(78));
    DelayCommand(13.0,Hover("0.3828,0.7639")); DelayCommand(13.3,Click("0.3828,0.7639"));
    DelayCommand(14.0,Export(pc)); DelayCommand(15.0,Hover("0.0352,0.1667"));
    DelayCommand(16.0,VShot("04_saved_pin_hover"));
    DelayCommand(18.0,Save()); DelayCommand(22.0,VDone());
}
