// Observe a single lateral bump from four retained facings, keeping the camera still.
#include "visual_inc"
object B() { return GetObjectByTag("M1Q0APavel"); }
object M() { return GetObjectByTag("BUMP_MOVER"); }
location L(float x,float y,float facing) { return Location(GetArea(GetFirstPC()),Vector(x,y,0.0),facing); }
void Reset(float facing) {
    AssignCommand(M(),ClearAllActions(TRUE)); AssignCommand(B(),ClearAllActions(TRUE));
    AssignCommand(M(),JumpToLocation(L(16.4,9.2,0.0)));
    AssignCommand(B(),JumpToLocation(L(18.097637,8.886529,facing)));
}
void Friend() {
    SetPlotFlag(B(),FALSE);
    SetIsTemporaryFriend(B(),M(),FALSE); SetIsTemporaryFriend(M(),B(),FALSE);
    SetPlotFlag(B(),TRUE);
}
void Go() { Friend(); AssignCommand(M(),ActionMoveToLocation(L(20.5,9.2,0.0))); }
void Burst(string name) { WriteTimestampedLogEntry("VISUAL|burst|"+name+"|2.0|20"); }
void Note(string name) {
    vector v=GetPosition(B());
    WriteTimestampedLogEntry("VISUAL|note|"+name+"|pavel="+FloatToString(v.x,0,3)+","+FloatToString(v.y,0,3)
      +"|facing="+FloatToString(GetFacing(B()),0,2)+"|friendly="+IntToString(GetIsFriend(B(),M())));
}
void Trial(float t,float facing,string name) {
    DelayCommand(t,Reset(facing)); DelayCommand(t+1.0,Friend());
    DelayCommand(t+1.5,Burst(name)); DelayCommand(t+1.8,Go());
    int i; for(i=0;i<20;i++) DelayCommand(t+1.5+IntToFloat(i)*0.1,Note(name+IntToString(i)));
}
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    VBegin(p); VHidePC(p); ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectCutsceneGhost(),p);
    CreateObject(OBJECT_TYPE_CREATURE,"vis_mover",L(14.0,9.2,0.0),FALSE,"BUMP_MOVER");
    DelayCommand(2.0,VCamera(p,90.0,6.0,65.0));
    Trial(4.0,0.0,"side_r"); Trial(10.0,180.0,"side_l");
    Trial(16.0,90.0,"back"); Trial(22.0,270.0,"forward");
    DelayCommand(28.0,VDone());
}
