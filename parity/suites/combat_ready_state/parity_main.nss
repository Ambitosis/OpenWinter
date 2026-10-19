#include "cr_inc"
void Quiet(object o)
{
    int i;for(i=5000;i<=5013;i++)SetEventScript(o,i,"");
    AssignCommand(o,ClearAllActions(TRUE));SetAILevel(o,AI_LEVEL_NORMAL);
}
void Finish(object a,object t){State(a,t,"queue_finished");}
void Start(object a,object t,int seeker)
{
    string id=GetTag(a);PLogI(id+".valid",GetIsObjectValid(a)&&GetIsObjectValid(t));
    PLogI(id+".position",GetDistanceBetweenLocations(GetLocation(a),GetLocalLocation(a,"origin"))<0.001 && GetDistanceBetweenLocations(GetLocation(t),GetLocalLocation(t,"origin"))<0.001);
    PLogI(id+".seen",GetObjectSeen(t,a));PLogI(id+".feat",GetHasFeat(451,a));PLogI(id+".uses",GetFeatRemainingUses(451,a));
    PLogI(id+".bow",GetBaseItemType(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,a)));
    PLogI(id+".arrows",GetBaseItemType(GetItemInSlot(INVENTORY_SLOT_ARROWS,a)));
    SetLocalFloat(a,"start",PNow());SetLocalInt(a,"started",1);State(a,t,"before_request");
    if(seeker)AssignCommand(a,ActionUseFeat(451,t));else AssignCommand(a,ActionAttack(t));
    AssignCommand(a,ActionDoCommand(Finish(a,t)));Samples(a,t,"request");
}
void Setup(object a,object t)
{
    Quiet(a);Quiet(t);SetPlotFlag(t,FALSE);
    ChangeToStandardFaction(a,STANDARD_FACTION_HOSTILE);ChangeToStandardFaction(t,STANDARD_FACTION_COMMONER);
    SetEventScript(t,EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED,"cr_attack");SetEventScript(t,EVENT_SCRIPT_CREATURE_ON_SPELLCASTAT,"cr_spell");
    SetItemStackSize(GetItemInSlot(INVENTORY_SLOT_ARROWS,a),99);
}
void End(object a,object t)
{
    State(a,t,"final");PLogI(GetTag(a)+".final_uses",GetFeatRemainingUses(451,a));
    PLogI(GetTag(a)+".stationary",GetDistanceBetweenLocations(GetLocation(a),GetLocalLocation(a,"origin"))<0.001 && GetDistanceBetweenLocations(GetLocation(t),GetLocalLocation(t,"origin"))<0.001);
}
void Pair(string id,float x,int seeker)
{
    location la=PCentre(x,0.0);location lt=PCentre(x,6.0);
    object a=CreateObject(OBJECT_TYPE_CREATURE,"sa_archer",la);object t=CreateObject(OBJECT_TYPE_CREATURE,"sa_target",lt);
    SetTag(a,id);SetLocalObject(t,"caster",a);SetLocalLocation(a,"origin",la);SetLocalLocation(t,"origin",lt);
    DelayCommand(.3,Setup(a,t));DelayCommand(5.0,Start(a,t,seeker));DelayCommand(19.0,End(a,t));
}
void main()
{
    Pair("bow",0.0,FALSE);Pair("seeker",20.0,TRUE);DelayCommand(20.0,PDone());
}
