// Enemies and a dying player (docs/DEATH_AND_DYING.md): a goblin with the stock AI beside the player as she drops
// to -3, then a goblin without AI told to attack her. Do they keep hitting her until she is dead?
#include "dy_inc"
#include "visual_inc"
void Trace(object pc){if(GetLocalInt(GetModule(),"stop"))return;DyNote(pc,"tr");DelayCommand(0.5,Trace(pc));}
void Foe(object pc,object g,string tag)
{
    DyNote(pc,tag+"|foe_act="+IntToString(GetCurrentAction(g))+"|foe_target="+GetName(GetAttackTarget(g))+"|foe_combat="+IntToString(GetIsInCombat(g))
        +"|foe_dist="+FloatToString(GetDistanceBetween(g,pc),0,2));
}
void Watch(object pc,object g,string tag,int n){if(n<=0||!GetIsObjectValid(g))return;Foe(pc,g,tag);DelayCommand(1.0,Watch(pc,g,tag,n-1));}
object Goblin(object pc,float dx,string tag,int ai)
{
    vector v=GetPosition(pc);
    object o=CreateObject(OBJECT_TYPE_CREATURE,"nw_goblina",Location(GetArea(pc),Vector(v.x+dx,v.y,v.z),180.0),FALSE,tag);
    if(!ai){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");}
    ChangeToStandardFaction(o,STANDARD_FACTION_HOSTILE);SetAILevel(o,AI_LEVEL_NORMAL);
    return o;
}
void Hurt(object pc,int to){int d=GetCurrentHitPoints(pc)-to;ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDamage(d,DAMAGE_TYPE_MAGICAL),pc);DyNote(pc,"after_hurt_"+IntToString(to));}
void Heal(object pc){if(GetIsDead(pc))ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectResurrection(),pc);ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectHeal(20),pc);DyNote(pc,"healed");}
void Gone(string tag){DestroyObject(GetObjectByTag(tag));}
void Phase1(object pc)
{
    object g=Goblin(pc,2.5,"dy_foe1",TRUE);
    SetPlotFlag(pc,TRUE);
    DelayCommand(4.0,SetPlotFlag(pc,FALSE));
    DelayCommand(4.0,Hurt(pc,-3));
    DelayCommand(4.0,Watch(pc,g,"foe1",14));
    DelayCommand(6.0,VShot("a_foe_ai"));
    DelayCommand(18.0,Gone("dy_foe1"));
}
void Phase2(object pc)
{
    Heal(pc);
    object g=Goblin(pc,2.0,"dy_foe2",FALSE);
    DelayCommand(1.0,Hurt(pc,-3));
    DelayCommand(1.5,AssignCommand(g,ActionAttack(pc)));
    DelayCommand(1.5,Watch(pc,g,"foe2",12));
    DelayCommand(4.0,VShot("b_foe_ordered"));
}
void Done(){SetLocalInt(GetModule(),"stop",1);VDone();}
void main()
{
    object pc=GetEnteringObject();object m=GetModule();
    if(!GetIsPC(pc)||GetLocalInt(m,"started"))return;
    SetLocalInt(m,"started",1);SetLocalFloat(m,"t0",DyNow());
    SetImmortal(pc,FALSE);
    SetEventScript(m,EVENT_SCRIPT_MODULE_ON_PLAYER_DYING,"dy_dying");
    SetEventScript(m,EVENT_SCRIPT_MODULE_ON_PLAYER_DEATH,"");
    SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,40.0);
    DelayCommand(0.5,VCamera(pc,90.0,8.0,60.0));
    DelayCommand(1.0,Trace(pc));
    DelayCommand(1.0,Phase1(pc));
    DelayCommand(21.0,Phase2(pc));
    DelayCommand(37.0,Done());
}
