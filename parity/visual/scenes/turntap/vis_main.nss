#include "visual_inc"
void Key(int k,string state) { WriteTimestampedLogEntry("VISUAL|key|"+IntToString(k)+"|"+state); }
void Note(string phase) { WriteTimestampedLogEntry("VISUAL|note|"+phase+"|"+FloatToString(GetFacing(GetFirstPC()),0,3)); }
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    DelayCommand(2.0,VCamera(p,90.0,4.5,65.0));
    DelayCommand(3.0,Key(65,"tap")); DelayCommand(3.3,Note("left1")); DelayCommand(3.5,VShot("left1"));
    DelayCommand(5.0,Key(65,"tap")); DelayCommand(5.3,Note("left2"));
    DelayCommand(7.0,Key(68,"tap")); DelayCommand(7.3,Note("right1"));
    DelayCommand(9.0,Key(68,"tap")); DelayCommand(9.3,Note("right2"));
    DelayCommand(11.0,Key(65,"down")); DelayCommand(11.1,Note("hold_100"));
    DelayCommand(11.2,Note("hold_200")); DelayCommand(11.3,Note("hold_300"));
    DelayCommand(11.4,Note("hold_400")); DelayCommand(11.5,Key(65,"up")); DelayCommand(11.7,Note("released"));
    DelayCommand(13.0,VDone());
}
