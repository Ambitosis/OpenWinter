#include "visual_inc"
void Tap(int vk){WriteTimestampedLogEntry("VISUAL|key|"+IntToString(vk)+"|tap");}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;object store=CreateObject(OBJECT_TYPE_STORE,"nw_storgenral001",GetLocation(pc));
DelayCommand(3.0,Tap(73));DelayCommand(5.0,VShot("0_I"));
DelayCommand(7.0,Tap(67));DelayCommand(9.0,VShot("1_C"));
DelayCommand(11.0,Tap(74));DelayCommand(13.0,VShot("2_J"));
DelayCommand(15.0,Tap(77));DelayCommand(17.0,VShot("3_M"));
DelayCommand(19.0,Tap(66));DelayCommand(21.0,VShot("4_B"));
DelayCommand(23.0,Tap(73));DelayCommand(25.0,VShot("5_I"));
DelayCommand(27.0,VDone());}
