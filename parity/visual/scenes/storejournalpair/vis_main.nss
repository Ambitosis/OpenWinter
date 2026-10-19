#include "visual_inc"
void Tap(int vk){WriteTimestampedLogEntry("VISUAL|key|"+IntToString(vk)+"|tap");}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;object store=CreateObject(OBJECT_TYPE_STORE,"nw_storgenral001",GetLocation(pc));
DelayCommand(3.0,Tap(73));DelayCommand(5.0,VShot("0_I"));
DelayCommand(7.0,Tap(74));DelayCommand(9.0,VShot("1_J"));
DelayCommand(11.0,OpenStore(store,pc));DelayCommand(13.0,VShot("2_S"));
DelayCommand(15.0,Tap(77));DelayCommand(17.0,VShot("3_M"));
DelayCommand(19.0,Tap(67));DelayCommand(21.0,VShot("4_C"));
DelayCommand(23.0,VDone());}
