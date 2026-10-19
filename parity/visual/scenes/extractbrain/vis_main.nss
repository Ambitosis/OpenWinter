// Installed impact/AI source uses this real action; the impact script remains untouched.
// cspecial is an installed-model candidate until the replacement control identifies it.
#include "visual_inc"
#include "eb_inc"
object Spawn(object pc,string blueprint,float x,float y,float facing)
{
    vector v=GetPosition(pc);
    return CreateObject(OBJECT_TYPE_CREATURE,blueprint,
        Location(GetArea(pc),Vector(v.x+x,v.y+y,v.z),facing));
}
void Quiet(object actor)
{
    int i; for(i=5000;i<=5013;i++) SetEventScript(actor,i,"");
    AssignCommand(actor,ClearAllActions(TRUE));
    SetAILevel(actor,AI_LEVEL_NORMAL);
}
int Paralyzed(object actor)
{
    effect e=GetFirstEffect(actor);
    while(GetIsEffectValid(e))
    {
        if(GetEffectType(e)==EFFECT_TYPE_PARALYZE) return TRUE;
        e=GetNextEffect(actor);
    }
    return FALSE;
}
void Report(object caster, object target, string stage)
{
    vector c=GetPosition(caster); vector t=GetPosition(target);
    EBNote("stage="+stage+"|caster="+GetTag(caster)
        +"|valid="+IntToString(GetIsObjectValid(caster)&&GetIsObjectValid(target)&&GetArea(caster)==GetArea(target))
        +"|appearance="+IntToString(GetAppearanceType(caster))
        +"|c="+VectorToString(c)+"|target="+VectorToString(t)
        +"|distance="+FloatToString(GetDistanceBetween(caster,target),0,3)
        +"|action="+IntToString(GetCurrentAction(caster))
        +"|enemy="+IntToString(GetIsEnemy(target,caster))
        +"|seen="+IntToString(GetObjectSeen(target,caster))
        +"|race="+IntToString(GetRacialType(target))
        +"|plot="+IntToString(GetPlotFlag(caster))
        +"|paralyzed="+IntToString(Paralyzed(target))
        +"|intelligence="+IntToString(GetAbilityScore(target,ABILITY_INTELLIGENCE))
        +"|events="+IntToString(GetLocalInt(target,"eb_events")));
}
void Finished(object caster, object target) { Report(caster,target,"queue_finished"); }
void Cast(object caster, object target)
{
    Report(caster,target,"request");
    AssignCommand(caster,ActionCastSpellAtObject(716,target,METAMAGIC_ANY,TRUE,0,PROJECTILE_PATH_TYPE_DEFAULT));
    AssignCommand(caster,ActionDoCommand(Finished(caster,target)));
}
void Shot(object a, object ta, object b, object tb, string name)
{
    Report(a,ta,name); Report(b,tb,name); VShot(name);
}
void Setup(object pc, object a, object ta, object b, object tb)
{
    Quiet(a); Quiet(ta); Quiet(b); Quiet(tb);
    // Match the successful headless stock dispatch control; no VSpawn faction/plot overrides.
    SetPlotFlag(ta,FALSE); SetPlotFlag(tb,FALSE);
    SetIsTemporaryEnemy(ta,a); SetIsTemporaryEnemy(a,ta);
    SetIsTemporaryEnemy(tb,b); SetIsTemporaryEnemy(b,tb);
    SetTag(a,"EB_STOCK"); SetTag(b,"EB_REPLACED");
    SetTag(ta,"EB_TARGET_STOCK"); SetTag(tb,"EB_TARGET_REPLACED");
    SetLocalObject(ta,"eb_caster",a); SetLocalObject(tb,"eb_caster",b);
    SetEventScript(ta,EVENT_SCRIPT_CREATURE_ON_SPELLCASTAT,"eb_event");
    SetEventScript(tb,EVENT_SCRIPT_CREATURE_ON_SPELLCASTAT,"eb_event");
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectParalyze(),ta,20.0);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectParalyze(),tb,20.0);
    ReplaceObjectAnimation(b,"cspecial","ctaunt");
    EBNote("castanim="+Get2DAString("spells","CastAnim",716)
        +"|casttime="+Get2DAString("spells","CastTime",716)
        +"|impact="+Get2DAString("spells","ImpactScript",716)
        +"|replacement=EB_REPLACED:cspecial->ctaunt");
}
void main()
{
    object pc=GetEnteringObject(); if(!GetIsPC(pc))return;
    VBegin(pc); VHidePC(pc);
    SetLocalFloat(GetModule(),"eb_start",EBNow());
    object a=Spawn(pc,"x2_mindflayer001",-2.0,2.0,270.0);
    object ta=Spawn(pc,"nw_bandit001",-2.0,0.5,90.0);
    object b=Spawn(pc,"x2_mindflayer001",2.0,2.0,270.0);
    object tb=Spawn(pc,"nw_bandit001",2.0,0.5,90.0);
    DelayCommand(0.3,Setup(pc,a,ta,b,tb));
    DelayCommand(1.0,VCamera(pc,90.0,9.0,60.0));
    DelayCommand(2.5,Shot(a,ta,b,tb,"a_idle"));
    DelayCommand(5.0,Cast(a,ta)); DelayCommand(5.0,Cast(b,tb));
    DelayCommand(5.3,Shot(a,ta,b,tb,"b_cast_030"));
    DelayCommand(5.85,Shot(a,ta,b,tb,"c_cast_085"));
    DelayCommand(6.4,Shot(a,ta,b,tb,"d_cast_140"));
    DelayCommand(6.95,Shot(a,ta,b,tb,"e_cast_195"));
    DelayCommand(7.5,Shot(a,ta,b,tb,"f_cast_250"));
    DelayCommand(8.05,Shot(a,ta,b,tb,"g_cast_305"));
    DelayCommand(8.35,Shot(a,ta,b,tb,"h_tail_335"));
    DelayCommand(9.2,Shot(a,ta,b,tb,"i_recovered"));
    DelayCommand(11.0,VDone());
}
