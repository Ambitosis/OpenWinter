#include "visual_inc"
void Note(string key,int value){WriteTimestampedLogEntry("VISUAL|note|"+key+"|"+IntToString(value));}
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc)||GetLocalInt(OBJECT_SELF,"started"))return;
    SetLocalInt(OBJECT_SELF,"started",1);
    WriteTimestampedLogEntry("VISUAL|note|character-fixture|"+GetName(pc)+"|pc="+IntToString(GetIsPC(pc)));
    Note("destination.entered",1);
    Note("destination.acquires",GetLocalInt(GetModule(),"acquires"));
    Note("destination.itemLocal",GetLocalInt(GetItemPossessedBy(pc,"transfer_item"),"item_local"));
    Note("destination.local",GetLocalInt(pc,"transfer_local"));
    Note("destination.selfRef",GetLocalObject(pc,"transfer_self")==pc);
    Note("destination.gold",GetGold(pc));
    Note("destination.level",GetHitDice(pc));
    Note("destination.hp",GetCurrentHitPoints(pc));
    Note("destination.action",GetCurrentAction(pc));
    Note("destination.item",GetIsObjectValid(GetItemPossessedBy(pc,"transfer_item")));
    int perm=0;int temp=0;effect e=GetFirstEffect(pc);
    while(GetIsEffectValid(e)) {if(GetEffectTag(e)=="transfer_perm")perm++;if(GetEffectTag(e)=="transfer_temp")temp++;e=GetNextEffect(pc);}
    Note("destination.permanent",perm);Note("destination.temporary",temp);
    vector v=GetPosition(pc);Note("destination.entry",v.x>7.0&&v.x<9.0&&v.y>7.0&&v.y<9.0);
    DelayCommand(1.0,VShot("destination"));
    DelayCommand(3.0,VDone());
}
