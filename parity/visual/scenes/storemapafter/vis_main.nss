#include "visual_inc"
void Tap(int vk){WriteTimestampedLogEntry("VISUAL|key|"+IntToString(vk)+"|tap");}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;object store=CreateObject(OBJECT_TYPE_STORE,"nw_storgenral001",GetLocation(pc));
DelayCommand(3.0,OpenStore(store,pc));DelayCommand(5.0,VShot("0_S"));
DelayCommand(7.0,Tap(77));DelayCommand(9.0,VShot("1_M"));
DelayCommand(11.0,Tap(77));DelayCommand(13.0,VShot("2_M"));
DelayCommand(15.0,Tap(73));DelayCommand(17.0,VShot("3_I"));
DelayCommand(19.0,Tap(73));DelayCommand(21.0,VShot("4_I"));
DelayCommand(23.0,Tap(67));DelayCommand(25.0,VShot("5_C"));
DelayCommand(27.0,Tap(74));DelayCommand(29.0,VShot("6_J"));
DelayCommand(31.0,Tap(77));DelayCommand(33.0,VShot("7_M"));
DelayCommand(35.0,VDone());}
