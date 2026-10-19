// Destination movement uses the same MoveAction as a ground click in OpenWinter.
#include "visual_inc"
void Reset() { VMovePC(GetFirstPC(),40.0,40.0,0.0,0.0); }
void Move(float x,int run) {
    object p=GetFirstPC();
    AssignCommand(p,ActionMoveToLocation(Location(GetArea(p),Vector(x,40.0,0.0),0.0),run));
}
void Burst(string name) { WriteTimestampedLogEntry("VISUAL|burst|"+name+"|1.6|5"); }
void Note(string name) {
    object p=GetFirstPC();
    WriteTimestampedLogEntry("VISUAL|note|"+name+"|x="+FloatToString(GetPosition(p).x,0,4)+"|action="+IntToString(GetCurrentAction(p)));
}
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    VBegin(p); DelayCommand(2.0,VCamera(p,90.0,4.5,65.0));
    DelayCommand(3.0,Reset()); DelayCommand(4.0,Move(44.0,TRUE)); DelayCommand(5.0,Burst("runstop"));
    DelayCommand(9.0,Reset()); DelayCommand(10.0,Move(42.0,FALSE)); DelayCommand(11.0,Burst("walkstop"));
    int i; for(i=0;i<25;i++) {
        DelayCommand(4.0+IntToFloat(i)*0.1,Note("run"+IntToString(i)));
        DelayCommand(10.0+IntToFloat(i)*0.1,Note("walk"+IntToString(i)));
    }
    DelayCommand(15.0,VDone());
}
