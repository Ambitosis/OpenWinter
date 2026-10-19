#include "visual_inc"
void Key(int code) { WriteTimestampedLogEntry("VISUAL|key|"+IntToString(code)+"|tap"); }
void main() {
 object pc=GetEnteringObject(); if (!GetIsPC(pc)) return; SetPlotFlag(pc,TRUE); SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);
 DelayCommand(1.0, VCamera(pc,90.0,7.0,65.0));
 DelayCommand(3.0, Key(86)); DelayCommand(4.0,VShot("root"));
 DelayCommand(5.0, Key(87)); DelayCommand(6.0,VShot("combat"));
 DelayCommand(7.0, Key(69)); DelayCommand(8.0,VShot("attack"));
 DelayCommand(11.0,PlayVoiceChat(VOICE_CHAT_HELLO,pc)); DelayCommand(12.0,VShot("hello"));
 DelayCommand(15.0,PlayVoiceChat(VOICE_CHAT_GATTACK1,pc));
 DelayCommand(19.0,PlayVoiceChat(VOICE_CHAT_PAIN1,pc));
 DelayCommand(23.0,PlayVoiceChat(VOICE_CHAT_DEATH,pc));
 DelayCommand(27.0,VDone());
}
