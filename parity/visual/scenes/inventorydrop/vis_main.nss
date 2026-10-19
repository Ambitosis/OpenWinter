#include "visual_inc"
void CreateProbe(object pc) { CreateItemOnObject("nw_aarcl002",pc); }
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 object item=GetFirstItemInInventory(pc);
 while(GetIsObjectValid(item)) { DestroyObject(item);item=GetNextItemInInventory(pc); }
 DelayCommand(0.2,CreateProbe(pc));
 DelayCommand(1.0,VCamera(pc,90.0,8.0,60.0));
}
