// Script-timed turns and movement interruption; capture does not drive keys or the real pointer.
#include "visual_inc"
object PC() { return GetFirstPC(); }
void Reset() {
    AssignCommand(PC(),ClearAllActions(TRUE));
    VMovePC(PC(),40.0,40.0,0.0,0.0);
}
void Face(float angle) { AssignCommand(PC(),SetFacing(angle)); }
void Run(float x) {
    AssignCommand(PC(),ClearAllActions(TRUE));
    AssignCommand(PC(),ActionMoveToLocation(Location(GetArea(PC()),Vector(x,40.0,0.0),0.0),TRUE));
}
void Sit() { AssignCommand(PC(),ActionPlayAnimation(ANIMATION_LOOPING_SIT_CROSS,1.0,10.0)); }
void Burst(string name) { WriteTimestampedLogEntry("VISUAL|burst|"+name+"|1.4|15"); }
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    VBegin(p);
    DelayCommand(2.0,VCamera(p,90.0,4.5,65.0));
    DelayCommand(3.0,Reset()); DelayCommand(4.0,Burst("quarter")); DelayCommand(4.3,Face(90.0));
    DelayCommand(7.0,Reset()); DelayCommand(8.0,Burst("half")); DelayCommand(8.3,Face(180.0));
    DelayCommand(11.0,Reset()); DelayCommand(11.5,Sit());
    DelayCommand(13.0,Burst("sitrun")); DelayCommand(13.3,Run(45.0));
    DelayCommand(17.0,Reset()); DelayCommand(18.0,Run(47.0));
    DelayCommand(18.6,Burst("reverse")); DelayCommand(18.9,Run(37.0));
    DelayCommand(23.0,VDone());
}
