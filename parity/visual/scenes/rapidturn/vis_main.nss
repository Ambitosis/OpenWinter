#include "visual_inc"
void Key(int key,string state) { WriteTimestampedLogEntry("VISUAL|key|"+IntToString(key)+"|"+state); }
void Note(string name) { WriteTimestampedLogEntry("VISUAL|note|"+name+"|"+FloatToString(GetFacing(GetFirstPC()),0,3)); }
void Reset() { VMovePC(GetFirstPC(),20.0,20.0,0.0,0.0); }
void Trial(float t,int key,int taps,string name) {
    DelayCommand(t,Reset());
    if(taps) { int j; for(j=0;j<10;j++) DelayCommand(t+1.0+IntToFloat(j)*0.1,Key(key,"tap")); }
    else { DelayCommand(t+1.0,Key(key,"down")); DelayCommand(t+2.0,Key(key,"up")); }
    int i; for(i=0;i<15;i++) DelayCommand(t+1.0+IntToFloat(i)*0.1,Note(name+IntToString(i)));
    DelayCommand(t+2.8,VShot(name));
}
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    DelayCommand(1.0,VCamera(p,90.0,5.0,65.0));
    Trial(2.0,65,FALSE,"left_hold"); Trial(6.0,65,TRUE,"left_taps");
    Trial(10.0,68,FALSE,"right_hold"); Trial(14.0,68,TRUE,"right_taps");
    DelayCommand(18.0,VDone());
}
