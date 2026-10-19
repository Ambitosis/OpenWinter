// Player dying, bleeding, death, the stock death panel and respawn, a custom death panel, and rest refusals
// (docs/DEATH_AND_DYING.md). On the original run with --real-mouse --native-framebuffer (the Respawn clicks).
#include "dy_inc"
#include "visual_inc"
void Trace(object pc){if(GetLocalInt(GetModule(),"stop"))return;DyNote(pc,"tr");DelayCommand(0.5,Trace(pc));}
void Hurt(object pc,int to)
{
    int d=GetCurrentHitPoints(pc)-to;DyNote(pc,"hurt_to_"+IntToString(to));
    ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDamage(d,DAMAGE_TYPE_MAGICAL),pc);DyNote(pc,"after_hurt_"+IntToString(to));
}
void Heal(object pc,int n){ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectHeal(n),pc);DyNote(pc,"after_heal_"+IntToString(n));}
void Bleed(object pc,float f){ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectHitPointChangeWhenDying(f),pc);DyNote(pc,"bleed_applied_"+FloatToString(f,0,1));}
void Strip(object pc){effect e=GetFirstEffect(pc);while(GetIsEffectValid(e)){RemoveEffect(pc,e);e=GetNextEffect(pc);}DyNote(pc,"stripped");}
void Walk(object pc)
{
    vector v=GetPosition(pc);
    AssignCommand(pc,ActionMoveToLocation(Location(GetArea(pc),Vector(v.x+3.0,v.y,v.z),0.0)));DyNote(pc,"walk_ordered");
}
// If the bleeding never kills, the rest of the scene still runs.
void Fallback(object pc){if(GetLocalInt(GetModule(),"deaths")==0){DyNote(pc,"fallback_kill");ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(),pc);}}
void main()
{
    object pc=GetEnteringObject();object m=GetModule();
    if(!GetIsPC(pc)||GetLocalInt(m,"started"))return;
    SetLocalInt(m,"started",1);SetLocalFloat(m,"t0",DyNow());
    SetPlotFlag(pc,FALSE);SetImmortal(pc,FALSE);
    SetEventScript(m,EVENT_SCRIPT_MODULE_ON_PLAYER_DYING,"dy_dying");
    SetEventScript(m,EVENT_SCRIPT_MODULE_ON_PLAYER_DEATH,"dy_death");
    SetEventScript(m,EVENT_SCRIPT_MODULE_ON_RESPAWN_BUTTON_PRESSED,"dy_respawn");
    SetEventScript(m,EVENT_SCRIPT_MODULE_ON_PLAYER_REST,"dy_rest");
    SetXP(pc,1500);
    if(GetGold(pc)<1000)GiveGoldToCreature(pc,1000-GetGold(pc));else TakeGoldFromCreature(GetGold(pc)-1000,pc,TRUE);
    SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,40.0);
    DelayCommand(0.5,VCamera(pc,90.0,8.0,60.0));
    WriteTimestampedLogEntry("VISUAL|note|dy|difficulty="+IntToString(GetGameDifficulty()));
    DelayCommand(1.0,Trace(pc));
    DelayCommand(3.0,Hurt(pc,-3));
    DelayCommand(4.5,VShot("a_dying"));
    DelayCommand(5.0,Walk(pc));
    DelayCommand(17.0,Heal(pc,5));
    DelayCommand(19.0,VShot("b_healed"));
    DelayCommand(21.0,Hurt(pc,-3));
    DelayCommand(22.0,Bleed(pc,2.0));
    DelayCommand(37.0,VShot("c_after_positive"));
    DelayCommand(38.0,Strip(pc));
    DelayCommand(38.5,Hurt(pc,-8));
    DelayCommand(39.0,Bleed(pc,-1.0));
    DelayCommand(70.0,Fallback(pc));
}
