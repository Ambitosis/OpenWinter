#include "visual_inc"
void Key(int key){WriteTimestampedLogEntry("VISUAL|key|"+IntToString(key)+"|tap");}
void Mouse(string xy,string state){WriteTimestampedLogEntry("VISUAL|mouse|"+xy+"|"+state);}
void Hover(string xy){WriteTimestampedLogEntry("VISUAL|hover|"+xy);}
void State(object pc,string phase)
{
    WriteTimestampedLogEntry("VISUAL|note|"+phase+"|main="+IntToString(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,pc)==GetLocalObject(pc,"bind_main"))
        +"|off="+IntToString(GetItemInSlot(INVENTORY_SLOT_LEFTHAND,pc)==GetLocalObject(pc,"bind_off"))
        +"|originalMain="+IntToString(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,pc)==GetLocalObject(pc,"original_main"))
        +"|originalOff="+IntToString(GetItemInSlot(INVENTORY_SLOT_LEFTHAND,pc)==GetLocalObject(pc,"original_off"))
        +"|emptyMain="+IntToString(!GetIsObjectValid(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,pc)))
        +"|emptyOff="+IntToString(!GetIsObjectValid(GetItemInSlot(INVENTORY_SLOT_LEFTHAND,pc))));
}
void Setup(object pc)
{
    object item=GetFirstItemInInventory(pc);
    while(GetIsObjectValid(item)){DestroyObject(item);item=GetNextItemInInventory(pc);}
    SetLocalObject(pc,"bind_main",CreateItemOnObject("nw_wblcl001",pc,1,"BIND_MAIN"));
    SetLocalObject(pc,"bind_off",CreateItemOnObject("nw_ashsw001",pc,1,"BIND_OFF"));
}
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc))return;
    SetLocalObject(pc,"original_main",GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,pc));
    SetLocalObject(pc,"original_off",GetItemInSlot(INVENTORY_SLOT_LEFTHAND,pc));
    WriteTimestampedLogEntry("VISUAL|note|character-fixture|"+GetName(pc)+"|pc="+IntToString(GetIsPC(pc)));
    // Preserve the equipped original F5 pair while authoring deterministic backpack items.
    DelayCommand(.5,Setup(pc));DelayCommand(1.0,VCamera(pc,90.0,5.0,65.0));
    DelayCommand(2.0,Key(73));DelayCommand(3.0,VShot("a_inventory_source_gate"));
    DelayCommand(3.7,Hover("0.015625,0.507"));
    DelayCommand(4.0,Mouse("0.015625,0.507","down"));
    DelayCommand(4.2,Hover("0.10,0.55"));DelayCommand(4.4,Hover("0.40,0.75"));
    DelayCommand(4.6,Hover("0.787,0.975"));DelayCommand(5.0,Mouse("0.787,0.975","up"));
    DelayCommand(6.0,VShot("b_main_binding"));DelayCommand(7.0,Key(73));
    DelayCommand(8.0,Key(123));DelayCommand(9.0,State(pc,"single_main_positive_control"));
    DelayCommand(10.0,Key(73));DelayCommand(10.7,Hover("0.043,0.507"));
    DelayCommand(11.0,Mouse("0.043,0.507","down"));
    DelayCommand(11.2,Hover("0.12,0.55"));DelayCommand(11.4,Hover("0.40,0.75"));
    DelayCommand(11.6,Hover("0.787,0.975"));DelayCommand(12.0,Mouse("0.787,0.975","up"));
    DelayCommand(13.0,VShot("c_offhand_over_main"));DelayCommand(14.0,Key(73));
    DelayCommand(15.0,Key(123));DelayCommand(16.0,State(pc,"compound_activation"));
    DelayCommand(17.0,VShot("d_compound_equipped"));DelayCommand(18.0,Key(123));
    DelayCommand(19.0,State(pc,"compound_toggle"));
    DelayCommand(20.0,VShot("e_new_pair_settled"));
    // The untouched genuine F5 supplies an independent draw-pose control even if drag input failed.
    DelayCommand(22.0,Key(116));
    DelayCommand(21.9,State(pc,"original_first.before"));DelayCommand(22.1,State(pc,"original_first.after"));
    DelayCommand(22.15,VShot("f_restore_015"));DelayCommand(22.4,VShot("f_restore_040"));
    DelayCommand(22.65,VShot("f_restore_065"));DelayCommand(22.9,VShot("f_restore_090"));
    DelayCommand(23.15,VShot("f_restore_115"));
    DelayCommand(25.0,Key(116));
    DelayCommand(24.9,State(pc,"original_repeat.before"));DelayCommand(25.1,State(pc,"original_repeat.after"));
    DelayCommand(25.15,VShot("g_remove_015"));DelayCommand(25.4,VShot("g_remove_040"));
    DelayCommand(25.65,VShot("g_remove_065"));DelayCommand(25.9,VShot("g_remove_090"));
    DelayCommand(26.15,VShot("g_remove_115"));
    DelayCommand(28.0,ExportSingleCharacter(pc));DelayCommand(30.0,VDone());
}
