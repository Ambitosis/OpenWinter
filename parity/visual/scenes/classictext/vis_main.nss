#include "visual_inc"
void Tap(int k) { WriteTimestampedLogEntry("VISUAL|key|"+IntToString(k)+"|tap"); }
void Chat(object p) {
    SendMessageToPC(p,"Welcome, traveler. The quick brown fox jumps over the lazy dog.");
    SendMessageToPC(p,"Minimum, little, willing. Courage, moon, doorway. 1234567890.");
    SendMessageToPC(p,"A long message wraps when it reaches the edge of the chat window. Follow the northern road past the old tower, cross the bridge, and wait beside the gate until morning.");
}
void Talk(object n,object p) { AssignCommand(n,ActionStartConversation(p,"vis_font",FALSE,FALSE)); }
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetPlotFlag(p,TRUE);
    object n=VSpawn(p,"nw_bandit001",0.0,3.0,270.0); SetName(n,"Font Guide");
    DelayCommand(1.0,VCamera(p,90.0,8.0,60.0));
    DelayCommand(1.5,WriteTimestampedLogEntry("VISUAL|key|9|down"));
    DelayCommand(2.3,VShot("a_labels"));
    DelayCommand(2.7,WriteTimestampedLogEntry("VISUAL|key|9|up"));
    DelayCommand(3.0,Chat(p)); DelayCommand(5.0,VShot("a_chat"));
    DelayCommand(6.0,Talk(n,p)); DelayCommand(8.0,VShot("b_dialog"));
    DelayCommand(9.0,Tap(49)); DelayCommand(11.0,VShot("c_reply"));
    DelayCommand(12.0,Tap(27)); DelayCommand(13.0,VDone());
}
