#include "visual_inc"
void Tap(int vk){WriteTimestampedLogEntry("VISUAL|key|"+IntToString(vk)+"|tap");}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;object store=CreateObject(OBJECT_TYPE_STORE,"nw_storgenral001",GetLocation(pc));SetEventScript(store,14001,"st_closed");
DelayCommand(3.0,Tap(73));DelayCommand(5.0,VShot("inventory"));
DelayCommand(6.0,OpenStore(store,pc));DelayCommand(8.0,VShot("store"));
DelayCommand(9.0,Tap(73));DelayCommand(11.0,VShot("close_inventory"));
DelayCommand(17.0,VDone());}
