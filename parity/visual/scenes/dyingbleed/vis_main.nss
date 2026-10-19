// EffectHitPointChangeWhenDying on a dying player: how much of its change lands, and when (while on, on removal,
// on healing), then the stock death panel and the stock respawn (docs/DEATH_AND_DYING.md).
// On the original run with --real-mouse --native-framebuffer (the Respawn click).
#include "dy_inc"
#include "visual_inc"
void Trace(object pc){if(GetLocalInt(GetModule(),"stop"))return;DyNote(pc,"tr");DelayCommand(0.5,Trace(pc));}
void Hurt(object pc,int to)
{
    int d=GetCurrentHitPoints(pc)-to;DyNote(pc,"hurt_to_"+IntToString(to));
    ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDamage(d,DAMAGE_TYPE_MAGICAL),pc);DyNote(pc,"after_hurt_"+IntToString(to));
}
void Damage(object pc,int n){ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDamage(n,DAMAGE_TYPE_MAGICAL),pc);DyNote(pc,"after_damage_"+IntToString(n));}
void Heal(object pc,int n){ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectHeal(n),pc);DyNote(pc,"after_heal_"+IntToString(n));}
void Bleed(object pc,float f){ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectHitPointChangeWhenDying(f),pc);DyNote(pc,"bleed_applied_"+FloatToString(f,0,1));}
void Strip(object pc,string tag){effect e=GetFirstEffect(pc);while(GetIsEffectValid(e)){RemoveEffect(pc,e);e=GetNextEffect(pc);}DyNote(pc,"stripped_"+tag);
    DelayCommand(0.1,DyNote(pc,"strip_0.1_"+tag));}
// A trial at t: full hit points, damage to nTo, the effect fChange on 1 s later, removed fFor seconds after that;
// fHealAt > 0 heals 1 that many seconds after the effect went on.
void Trial(object pc,float t,int nTo,float fChange,float fFor,float fHealAt,string tag)
{
    DelayCommand(t,Heal(pc,20));
    DelayCommand(t+0.5,Hurt(pc,nTo));
    DelayCommand(t+1.0,Bleed(pc,fChange));
    if(fHealAt>0.0)DelayCommand(t+1.0+fHealAt,Heal(pc,1));
    DelayCommand(t+1.0+fFor,Strip(pc,tag));
}
void Fallback(object pc){if(GetLocalInt(GetModule(),"deaths")==0){DyNote(pc,"fallback_kill");ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(),pc);}}
void main()
{
    object pc=GetEnteringObject();object m=GetModule();
    if(!GetIsPC(pc)||GetLocalInt(m,"started"))return;
    SetLocalInt(m,"started",1);SetLocalFloat(m,"t0",DyNow());
    SetPlotFlag(pc,FALSE);SetImmortal(pc,FALSE);
    SetEventScript(m,EVENT_SCRIPT_MODULE_ON_PLAYER_DYING,"dy_dying");
    SetEventScript(m,EVENT_SCRIPT_MODULE_ON_PLAYER_DEATH,"db_death");
    SetEventScript(m,EVENT_SCRIPT_MODULE_ON_RESPAWN_BUTTON_PRESSED,"db_respawn");
    SetXP(pc,1500);
    if(GetGold(pc)<1000)GiveGoldToCreature(pc,1000-GetGold(pc));else TakeGoldFromCreature(GetGold(pc)-1000,pc,TRUE);
    SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,40.0);
    DelayCommand(0.5,VCamera(pc,90.0,8.0,60.0));
    // The stock respawn takes the player to NW_DEATH_TEMPLE and only then applies its XP and gold penalty.
    vector v=GetPosition(pc);
    CreateObject(OBJECT_TYPE_WAYPOINT,"nw_waypoint001",Location(GetArea(pc),Vector(v.x,v.y+6.0,v.z),270.0),FALSE,"NW_DEATH_TEMPLE");
    DelayCommand(1.0,Trace(pc));
    // Each trial: back to full, down to a dying value, the effect on, then removed (when its change shows).
    Trial(pc,3.0,-9,1.0,1.0,-1.0,"A");
    Trial(pc,9.0,-9,1.0,4.0,-1.0,"B");
    Trial(pc,18.0,-9,1.0,5.0,3.0,"C");
    Trial(pc,28.0,-9,0.5,6.0,-1.0,"D");
    Trial(pc,39.0,-2,1.0,10.0,-1.0,"E");
    Trial(pc,54.0,-3,-1.0,3.0,-1.0,"F");
    // Death at -10, the stock death panel and Respawn.
    DelayCommand(63.0,Heal(pc,20));
    DelayCommand(64.0,Hurt(pc,-10));
    DelayCommand(80.0,Fallback(pc));
}
