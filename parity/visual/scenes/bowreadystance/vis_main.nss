#include "visual_inc"
#include "sa_inc"
object Spawn(object pc,string blueprint,float x,float y,float facing)
{
    vector v=GetPosition(pc);
    return CreateObject(OBJECT_TYPE_CREATURE,blueprint,Location(GetArea(pc),Vector(v.x+x,v.y+y,v.z),facing));
}
void Quiet(object o)
{
    int i;for(i=5000;i<=5013;i++)SetEventScript(o,i,"");
    AssignCommand(o,ClearAllActions(TRUE));SetAILevel(o,AI_LEVEL_NORMAL);
}
void Report(object a,object t,string stage)
{
    vector p=GetPosition(a);vector q=GetPosition(t);
    SANote("stage="+stage+"|caster="+GetTag(a)
        +"|valid="+IntToString(GetIsObjectValid(a)&&GetIsObjectValid(t)&&GetArea(a)==GetArea(t))
        +"|x="+FloatToString(p.x,0,3)+"|y="+FloatToString(p.y,0,3)
        +"|tx="+FloatToString(q.x,0,3)+"|ty="+FloatToString(q.y,0,3)
        +"|distance="+FloatToString(GetDistanceBetween(a,t),0,3)
        +"|seen="+IntToString(GetObjectSeen(t,a))+"|action="+IntToString(GetCurrentAction(a))
        +"|combat="+IntToString(GetIsInCombat(a))+"|target_combat="+IntToString(GetIsInCombat(t))
        +"|aa="+IntToString(GetLevelByClass(CLASS_TYPE_ARCANE_ARCHER,a))
        +"|feat="+IntToString(GetHasFeat(451,a))+"|uses="+IntToString(GetFeatRemainingUses(451,a))
        +"|bow="+IntToString(GetBaseItemType(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,a)))
        +"|arrows="+IntToString(GetBaseItemType(GetItemInSlot(INVENTORY_SLOT_ARROWS,a)))
        +"|attacks="+IntToString(GetLocalInt(t,"attacks"))+"|events="+IntToString(GetLocalInt(t,"events"))
        +"|hp="+IntToString(GetCurrentHitPoints(t)));
}
void Finished(object a,object t){Report(a,t,"cast_finished");}
void Cast(object a,object t)
{
    Report(a,t,"cast_request");
    AssignCommand(a,ActionUseFeat(451,t));AssignCommand(a,ActionDoCommand(Finished(a,t)));
}
void Attack(object a,object t)
{
    Report(a,t,"attack_request");AssignCommand(a,ActionAttack(t));
}
void Stop(object a,object t)
{
    AssignCommand(a,ClearAllActions(TRUE));Report(a,t,"attack_stopped");
}
void Shot(object a,object ta,object b,object tb,string name)
{
    Report(a,ta,name);Report(b,tb,name);VShot(name);
}
void Setup(object a,object ta,object b,object tb)
{
    Quiet(a);Quiet(ta);Quiet(b);Quiet(tb);
    SetTag(a,"SA_STOCK");SetTag(b,"SA_REPLACED");SetTag(ta,"SA_TARGET_STOCK");SetTag(tb,"SA_TARGET_REPLACED");
    SetLocalObject(ta,"caster",a);SetLocalObject(tb,"caster",b);
    SetPlotFlag(ta,FALSE);SetPlotFlag(tb,FALSE);
    ChangeToStandardFaction(a,STANDARD_FACTION_HOSTILE);ChangeToStandardFaction(b,STANDARD_FACTION_HOSTILE);
    ChangeToStandardFaction(ta,STANDARD_FACTION_COMMONER);ChangeToStandardFaction(tb,STANDARD_FACTION_COMMONER);
    SetEventScript(ta,EVENT_SCRIPT_CREATURE_ON_SPELLCASTAT,"sa_event");SetEventScript(tb,EVENT_SCRIPT_CREATURE_ON_SPELLCASTAT,"sa_event");
    SetEventScript(ta,EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED,"sa_attack");SetEventScript(tb,EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED,"sa_attack");
    SetItemStackSize(GetItemInSlot(INVENTORY_SLOT_ARROWS,a),99);SetItemStackSize(GetItemInSlot(INVENTORY_SLOT_ARROWS,b),99);
    ReplaceObjectAnimation(b,"bowrdy","salute");
    SANote("castanim="+Get2DAString("spells","CastAnim",601)+"|impact="+Get2DAString("spells","ImpactScript",601)+"|replacement=SA_REPLACED:bowrdy->salute");
}
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc))return;
    VBegin(pc);VHidePC(pc);SetLocalFloat(GetModule(),"sa_start",SANow());
    object a=Spawn(pc,"sa_archer",-2.0,1.0,90.0);object ta=Spawn(pc,"sa_target",-2.0,7.0,270.0);
    object b=Spawn(pc,"sa_archer",2.0,1.0,90.0);object tb=Spawn(pc,"sa_target",2.0,7.0,270.0);
    DelayCommand(.3,Setup(a,ta,b,tb));DelayCommand(1.0,VCamera(pc,90.0,9.0,60.0));
    DelayCommand(4.0,Shot(a,ta,b,tb,"a_idle"));
    DelayCommand(5.0,Attack(a,ta));DelayCommand(5.0,Attack(b,tb));
    DelayCommand(5.3,Shot(a,ta,b,tb,"b_attack_030"));
    DelayCommand(5.85,Shot(a,ta,b,tb,"c_attack_085"));
    DelayCommand(6.4,Shot(a,ta,b,tb,"d_attack_140"));
    DelayCommand(7.0,Stop(a,ta));DelayCommand(7.0,Stop(b,tb));
    DelayCommand(10.0,Cast(a,ta));DelayCommand(10.0,Cast(b,tb));
    DelayCommand(10.3,Shot(a,ta,b,tb,"e_cast_030"));
    DelayCommand(10.85,Shot(a,ta,b,tb,"f_cast_085"));
    DelayCommand(11.35,Shot(a,ta,b,tb,"g_cast_135"));
    DelayCommand(12.0,Shot(a,ta,b,tb,"h_recovered"));
    DelayCommand(15.0,Shot(a,ta,b,tb,"i_impact_done"));
    DelayCommand(17.0,VDone());
}
