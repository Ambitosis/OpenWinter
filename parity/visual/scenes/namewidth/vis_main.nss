#include "visual_inc"
void Name(string name) { SetName(GetFirstPC(),name); }
void Key(string state) { WriteTimestampedLogEntry("VISUAL|key|9|"+state); }
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    DelayCommand(2.0,VCamera(p,90.0,7.0,55.0));
    DelayCommand(3.0,Name("MrCombatTester Two")); DelayCommand(4.0,Key("down"));
    DelayCommand(5.0,VShot("medium"));
    DelayCommand(7.0,Key("up")); DelayCommand(8.0,Name("Al")); DelayCommand(9.0,Key("down"));
    DelayCommand(10.0,VShot("short"));
    DelayCommand(12.0,Key("up")); DelayCommand(13.0,Name("A Rather Long Adventurer Name")); DelayCommand(14.0,Key("down"));
    DelayCommand(15.0,VShot("long")); DelayCommand(18.0,Key("up")); DelayCommand(19.0,VDone());
}
