#include "parity_inc"
#include "parity_time"
void Quiet(object o)
{
    int i;for(i=5000;i<=5013;i++)SetEventScript(o,i,"");
    AssignCommand(o,ClearAllActions(TRUE));SetAILevel(o,AI_LEVEL_NORMAL);
}
void Finished(object a,object t)
{
    PLogF("finish",PNow()-GetLocalFloat(a,"start"),0.25);
}
void End(object a,object t)
{
    PLogI("events",GetLocalInt(t,"events"));
    PLogI("damaged",GetCurrentHitPoints(t)<GetLocalInt(t,"hp"));
    PLogI("remaining_feat_uses",GetFeatRemainingUses(451,a));
    WriteTimestampedLogEntry("Seeker Arrow target HP: "+IntToString(GetCurrentHitPoints(t)));
    PDone();
}
void Go(object a,object t)
{
    PLogI("seen",GetObjectSeen(t,a));PLogI("enemy",GetIsEnemy(t,a));
    PLogI("aa_level",GetLevelByClass(CLASS_TYPE_ARCANE_ARCHER,a));
    PLogI("feat",GetHasFeat(451,a));PLogI("feat_uses",GetFeatRemainingUses(451,a));
    PLogI("bow",GetBaseItemType(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,a)));
    PLogI("arrows",GetBaseItemType(GetItemInSlot(INVENTORY_SLOT_ARROWS,a)));
    PLogF("distance",GetDistanceBetween(a,t),0.05);
    SetLocalFloat(a,"start",PNow());SetLocalInt(t,"hp",GetCurrentHitPoints(t));
    AssignCommand(a,ActionUseFeat(451,t));
    AssignCommand(a,ActionDoCommand(Finished(a,t)));
}
void Setup(object a,object t)
{
    Quiet(a);Quiet(t);SetPlotFlag(t,FALSE);
    SetEventScript(t,EVENT_SCRIPT_CREATURE_ON_SPELLCASTAT,"sa_event");
    ChangeToStandardFaction(a,STANDARD_FACTION_HOSTILE);
    ChangeToStandardFaction(t,STANDARD_FACTION_COMMONER);
}
void main()
{
    object a=CreateObject(OBJECT_TYPE_CREATURE,"sa_archer",PCentre(0.0,0.0));
    object t=CreateObject(OBJECT_TYPE_CREATURE,"sa_target",PCentre(0.0,6.0));
    SetLocalObject(t,"caster",a);
    DelayCommand(.3,Setup(a,t));DelayCommand(5.0,Go(a,t));DelayCommand(10.0,End(a,t));
}
