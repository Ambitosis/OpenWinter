#include "visual_inc"
void Note(string key,int value){WriteTimestampedLogEntry("VISUAL|note|"+key+"|"+IntToString(value));}
void Switch(object pc)
{
    Note("source.local",GetLocalInt(pc,"transfer_local"));
    Note("source.gold",GetGold(pc));
    Note("source.level",GetHitDice(pc));
    Note("source.hp",GetCurrentHitPoints(pc));
    Note("source.before",1);
    StartNewModule("vis_moduledest");
    Note("source.after",1);
    DelayCommand(5.0, Note("source.timerSurvived",1));
}
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc)||GetLocalInt(OBJECT_SELF,"started"))return;
    SetLocalInt(OBJECT_SELF,"started",1);
    WriteTimestampedLogEntry("VISUAL|note|character-fixture|"+GetName(pc)+"|pc="+IntToString(GetIsPC(pc)));
    VBegin(pc);
    SetEventScript(GetModule(), EVENT_SCRIPT_MODULE_ON_CLIENT_EXIT,"ms_leave");
    SetLocalInt(pc,"transfer_local",73);
    SetLocalObject(pc,"transfer_self",pc);
    object item=CreateItemOnObject("nw_it_mpotion001",pc,2,"transfer_item");
    SetLocalInt(item,"item_local",91);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,TagEffect(EffectACIncrease(2),"transfer_perm"),pc);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY,TagEffect(EffectAbilityIncrease(ABILITY_STRENGTH,2),"transfer_temp"),pc,60.0);
    AssignCommand(pc,ActionWait(100.0));
    DelayCommand(1.0,Switch(pc));
}
