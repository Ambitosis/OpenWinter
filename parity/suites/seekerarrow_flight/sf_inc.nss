#include "parity_inc"
#include "parity_time"
void Quiet(object o)
{
    int i;for(i=5000;i<=5013;i++)SetEventScript(o,i,"");
    AssignCommand(o,ClearAllActions(TRUE));SetAILevel(o,AI_LEVEL_NORMAL);
}
void Finished(object a)
{
    PLogF(GetTag(a)+".finish",PNow()-GetLocalFloat(a,"start"),0.2);
}
void End(object a,object t)
{
    string id=GetTag(a);
    PLogI(id+".events",GetLocalInt(t,"events"));
    PLogI(id+".damaged",GetCurrentHitPoints(t)<GetLocalInt(t,"hp"));
    PLogI(id+".remaining_uses",GetFeatRemainingUses(451,a));
}
void Go(object a,object t)
{
    string id=GetTag(a);vector c=GetPosition(a);vector v=GetPosition(t);
    PLogI(id+".valid",GetIsObjectValid(a)&&GetIsObjectValid(t)&&GetArea(a)==GetArea(t));
    PLogI(id+".position",GetDistanceBetweenLocations(GetLocation(a),GetLocalLocation(a,"expected"))<0.01 && GetDistanceBetweenLocations(GetLocation(t),GetLocalLocation(t,"expected"))<0.01);
    PLogI(id+".seen",GetObjectSeen(t,a));PLogI(id+".aa_level",GetLevelByClass(CLASS_TYPE_ARCANE_ARCHER,a));
    PLogI(id+".feat",GetHasFeat(451,a));PLogI(id+".uses",GetFeatRemainingUses(451,a));
    PLogI(id+".bow",GetBaseItemType(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,a)));
    PLogI(id+".arrows",GetBaseItemType(GetItemInSlot(INVENTORY_SLOT_ARROWS,a)));
    PLogF(id+".distance",GetDistanceBetween(a,t),0.01);
    PLogF(id+".cx",c.x,0.01);PLogF(id+".cy",c.y,0.01);PLogF(id+".tx",v.x,0.01);PLogF(id+".ty",v.y,0.01);
    SetLocalFloat(a,"start",PNow());SetLocalInt(t,"hp",GetCurrentHitPoints(t));
    AssignCommand(a,ActionUseFeat(451,t));AssignCommand(a,ActionDoCommand(Finished(a)));
}
void Setup(object a,object t)
{
    Quiet(a);Quiet(t);SetPlotFlag(t,FALSE);SetEventScript(t,EVENT_SCRIPT_CREATURE_ON_SPELLCASTAT,"sf_event");
    ChangeToStandardFaction(a,STANDARD_FACTION_HOSTILE);ChangeToStandardFaction(t,STANDARD_FACTION_COMMONER);
}
void Pair(string id,float x,float y,float distance)
{
    object a=CreateObject(OBJECT_TYPE_CREATURE,"sa_archer",PCentre(x,y));
    object t=CreateObject(OBJECT_TYPE_CREATURE,"sa_target",PCentre(x,y+distance));
    SetTag(a,id);SetLocalObject(t,"caster",a);
    SetLocalLocation(a,"expected",PCentre(x,y));SetLocalLocation(t,"expected",PCentre(x,y+distance));
    DelayCommand(.3,Setup(a,t));DelayCommand(5.0,Go(a,t));DelayCommand(21.0,End(a,t));
}
