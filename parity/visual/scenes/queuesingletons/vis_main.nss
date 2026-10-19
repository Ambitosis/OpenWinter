#include "visual_inc"
void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|"+s); }
void Key(int key) { WriteTimestampedLogEntry("VISUAL|key|"+IntToString(key)+"|tap"); }
void Read(object pc, string label)
{
    int count=0; int total=0;
    object item=GetFirstItemInInventory(pc);
    while(GetIsObjectValid(item))
    {
        if(GetBaseItemType(item)==49 && GetItemPropertySubType(GetFirstItemProperty(item))==66)
        {
            count++; total+=GetItemStackSize(item);
            Note(label+".item."+IntToString(count)+".stack."+IntToString(GetItemStackSize(item)));
        }
        item=GetNextItemInInventory(pc);
    }
    Note(label+".objects."+IntToString(count)+".total."+IntToString(total)+".action."+IntToString(GetCurrentAction(pc)));
}
void main()
{
    object pc=GetEnteringObject(); if(!GetIsPC(pc))return;
    SetPlotFlag(pc,TRUE);
    Note("character-fixture|"+GetName(pc)+"|pc="+IntToString(GetIsPC(pc)));
    DelayCommand(.5,Read(pc,"before"));
    DelayCommand(1.0,VCamera(pc,90.0,5.0,65.0));
    DelayCommand(2.0,Key(73));
    DelayCommand(2.5,VShot("inventory_before"));
    DelayCommand(3.0,Key(73));
    DelayCommand(4.0,VShot("hotbar_before"));
    DelayCommand(5.0,Key(118));
    DelayCommand(5.15,Key(118));
    DelayCommand(5.5,VShot("two_queued"));
    DelayCommand(12.0,Read(pc,"after"));
    DelayCommand(12.5,VShot("hotbar_after"));
    DelayCommand(13.0,Key(73));
    DelayCommand(13.5,VShot("inventory_after"));
    DelayCommand(15.0,VDone());
}
