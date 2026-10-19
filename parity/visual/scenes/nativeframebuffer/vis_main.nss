#include "visual_inc"
void Chest(object pc) { object o=VPlaceable(pc,"plc_chest1",3.0,0.0); SetTag(o,"FRAME_CHEST"); }
void Table(object pc) { object o=VPlaceable(pc,"plc_table",3.0,0.0); SetTag(o,"FRAME_TABLE"); }
void RemoveChest() { DestroyObject(GetObjectByTag("FRAME_CHEST")); }
void main()
{
    object pc=GetEnteringObject(); if(!GetIsPC(pc)) return;
    DelayCommand(.5,Chest(pc));
    DelayCommand(1.0,VCamera(pc,90.0,8.0,60.0));
    DelayCommand(3.0,VShot("a_chest"));
    DelayCommand(4.0,RemoveChest());
    DelayCommand(5.0,VShot("b_empty"));
    DelayCommand(6.0,Table(pc));
    DelayCommand(7.0,VShot("c_table"));
    DelayCommand(8.0,VDone());
}
