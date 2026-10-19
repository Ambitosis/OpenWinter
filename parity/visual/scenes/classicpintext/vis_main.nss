#include "visual_inc"
void Tap(int key) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(key) + "|tap"); }
void Click(string at) { WriteTimestampedLogEntry("VISUAL|click|" + at + "|0.35"); }
void Text() { WriteTimestampedLogEntry("VISUAL|text|ELYPIN2026"); }
void Save(object pc) {
    ExportSingleCharacter(pc);
    DoSinglePlayerAutoSave();
    WriteTimestampedLogEntry("VISUAL|note|save|requested");
}
void main() {
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    ExploreAreaForPlayer(GetArea(pc), pc, TRUE);
    SetCreatureExploresMinimap(pc, FALSE);
    DelayCommand(1.0, Tap(77));
    DelayCommand(3.0, VShot("01_map"));
    DelayCommand(4.5, Click("0.0148,0.1028"));
    DelayCommand(6.0, VShot("02_pin_mode"));
    DelayCommand(7.5, Click("0.0352,0.1667"));
    DelayCommand(9.0, WriteTimestampedLogEntry("VISUAL|note|editor_observation_window|begin"));
    DelayCommand(30.0, Text());
    DelayCommand(34.0, Click("0.3828,0.7639"));
    DelayCommand(37.0, WriteTimestampedLogEntry("VISUAL|hover|0.0352,0.1667"));
    DelayCommand(39.0, VShot("03_saved_pin_hover"));
    DelayCommand(42.0, Save(pc));
    DelayCommand(48.0, VDone());
}
