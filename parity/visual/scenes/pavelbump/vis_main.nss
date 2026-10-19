// Original placed Pavel and Prelude factions, without campaign triggers. Posted W keys
// test neutral, friendly, friendly-but-busy, then neutral again with his torch/plot flag intact.
#include "visual_inc"
object Pavel() { return GetObjectByTag("M1Q0APavel"); }
string Vec(vector v) { return FloatToString(v.x,0,3)+","+FloatToString(v.y,0,3); }
void Note(string phase) {
    object p=GetFirstPC(); object b=Pavel(); vector a=GetPosition(p); vector v=GetPosition(b);
    WriteTimestampedLogEntry("VISUAL|note|"+phase+" pc="+Vec(a)+" pavel="+Vec(v)
      +" action="+IntToString(GetCurrentAction(b))+" commandable="+IntToString(GetCommandable(b))
      +" plot="+IntToString(GetPlotFlag(b))+" seen="+IntToString(GetObjectSeen(b,p))
      +" friend_pc_to_pavel="+IntToString(GetIsFriend(b,p))+" friend_pavel_to_pc="+IntToString(GetIsFriend(p,b))
      +" rep_pc_to_pavel="+IntToString(GetReputation(p,b))+" rep_pavel_to_pc="+IntToString(GetReputation(b,p))
      +" rate="+IntToString(GetMovementRate(b)));
}
void Reset() {
    object p=GetFirstPC(); object b=Pavel();
    AssignCommand(p,ClearAllActions(TRUE)); AssignCommand(b,ClearAllActions(TRUE));
    AssignCommand(b,JumpToLocation(Location(GetArea(b),Vector(18.097637,8.886529,0.0),210.0)));
    AssignCommand(p,JumpToLocation(Location(GetArea(p),Vector(14.5,8.886529,0.0),0.0)));
    VCamera(p,0.0,9.0,65.0);
}
void Key(string state) { WriteTimestampedLogEntry("VISUAL|key|87|"+state); }
void Busy() { AssignCommand(Pavel(),ActionWait(60.0)); }
void Friendly() {
    // Plot creatures reject reputation edits. Restore Plot before testing movement.
    SetPlotFlag(Pavel(),FALSE);
    AdjustReputation(GetFirstPC(),Pavel(),100);
    SetPlotFlag(Pavel(),TRUE);
}
void Neutral() {
    SetPlotFlag(Pavel(),FALSE);
    AdjustReputation(GetFirstPC(),Pavel(),-50);
    SetPlotFlag(Pavel(),TRUE);
}
void Trial(float t, string name) {
    DelayCommand(t,Reset()); DelayCommand(t+2.0,Note(name+".before"));
    DelayCommand(t+2.2,Key("down")); DelayCommand(t+5.2,Key("up"));
    DelayCommand(t+6.0,Note(name+".after"));
}
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    Trial(3.0,"neutral");
    Trial(12.0,"friendly"); DelayCommand(13.0,Friendly()); // apply relationship after positioning
    Trial(21.0,"friendly_busy"); DelayCommand(22.0,Busy()); DelayCommand(22.1,Friendly());
    DelayCommand(29.0,Neutral()); Trial(30.0,"neutral_again");
    DelayCommand(38.0,VDone());
}
