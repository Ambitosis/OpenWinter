#include "visual_inc"
void Tap(int k) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(k) + "|tap"); }
void Move(object pc, float x) { AssignCommand(pc, JumpToLocation(Location(GetArea(pc), Vector(x,x,0.0),90.0))); }
void Shot(object pc, string name) {
    vector p = GetPosition(pc);
    WriteTimestampedLogEntry("VISUAL|note|position|" + name + "|" + FloatToString(p.x) + "|" + FloatToString(p.y));
    VShot(name);
}
void main() {
    object pc=GetEnteringObject(); if(!GetIsPC(pc)) return;
    SetPlotFlag(pc,TRUE);
    ExploreAreaForPlayer(GetArea(pc),pc,TRUE);
    SetCreatureExploresMinimap(pc,FALSE);
    DelayCommand(2.0,Tap(77));
    DelayCommand(3.0,WriteTimestampedLogEntry("VISUAL|click|0.0273,0.1028"));
    DelayCommand(4.0,Shot(pc,"01_near_origin"));
    DelayCommand(5.0,Move(pc,80.0));
    DelayCommand(7.0,Shot(pc,"02_center"));
    DelayCommand(8.0,Move(pc,150.0));
    DelayCommand(10.0,Shot(pc,"03_far_corner"));
    DelayCommand(11.0,VDone());
}
