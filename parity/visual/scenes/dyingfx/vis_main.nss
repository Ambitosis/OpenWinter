// EffectHitPointChangeWhenDying on a dying player, second set: a temporary effect expiring, the effect put on
// before dying; then the stock death panel and the stock respawn (docs/DEATH_AND_DYING.md).
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
void BleedFor(object pc,float f,float d){ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectHitPointChangeWhenDying(f),pc,d);
    DyNote(pc,"bleed_temp_"+FloatToString(f,0,1)+"_"+FloatToString(d,0,1));}
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
    // G1: +1/round as a 3 s temporary effect on a dying player: what happens when it expires.
    DelayCommand(3.0,Hurt(pc,-9));
    DelayCommand(4.0,BleedFor(pc,1.0,3.0));
    // G3: the effect put on while alive, then the player brought down to dying; removed 5 s later.
    DelayCommand(11.0,Heal(pc,20));
    DelayCommand(11.5,Bleed(pc,1.0));
    DelayCommand(12.5,Hurt(pc,-9));
    DelayCommand(17.5,Strip(pc,"g3"));
    // G4: -1/round as a 2 s temporary effect on a dying player: death when it expires? Then the stock panel.
    DelayCommand(21.0,Heal(pc,20));
    DelayCommand(21.5,Hurt(pc,-8));
    DelayCommand(22.0,BleedFor(pc,-1.0,2.0));
    DelayCommand(30.0,Hurt(pc,-10));
    DelayCommand(34.0,Fallback(pc));
}
