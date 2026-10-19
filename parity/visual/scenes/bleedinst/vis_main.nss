// bleedinst (docs/DEATH_AND_DYING.md): EffectHitPointChangeWhenDying applied as INSTANT on a
// dying player (the usual way modules bleed), its tick period for -1, -2, -0.5 and +2 per round, what removing the
// effects does while she is still dying, a temporary one removed by script, and where hit points end on a death. Hit points every 0.5 s.
#include "dy_inc"
#include "visual_inc"
void Trace(object pc){if(GetLocalInt(GetModule(),"stop"))return;DyNote(pc,"tr");DelayCommand(0.5,Trace(pc));}
void Hurt(object pc,int to)
{
    int d=GetCurrentHitPoints(pc)-to;
    ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDamage(d,DAMAGE_TYPE_MAGICAL),pc);DyNote(pc,"after_hurt_"+IntToString(to));
}
void Heal(object pc,int n){ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectHeal(n),pc);DyNote(pc,"after_heal_"+IntToString(n));}
void Raise(object pc)
{
    if(GetIsDead(pc)&&GetCurrentHitPoints(pc)<=-10)ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectResurrection(),pc);
    ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectHeal(60),pc);DyNote(pc,"raised");
}
void Bleed(object pc,float f){ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectHitPointChangeWhenDying(f),pc);DyNote(pc,"bleed_instant_"+FloatToString(f,0,1));}
void BleedFor(object pc,float f,float d){ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectHitPointChangeWhenDying(f),pc,d);
    DyNote(pc,"bleed_temp_"+FloatToString(f,0,1)+"_"+FloatToString(d,0,1));}
void Strip(object pc,string tag){effect e=GetFirstEffect(pc);while(GetIsEffectValid(e)){RemoveEffect(pc,e);e=GetNextEffect(pc);}DyNote(pc,"stripped_"+tag);
    DelayCommand(0.05,DyNote(pc,"strip_0.05_"+tag));DelayCommand(0.3,DyNote(pc,"strip_0.3_"+tag));}
void Stop(){SetLocalInt(GetModule(),"stop",1);VDone();}
void main()
{
    object pc=GetEnteringObject();object m=GetModule();
    if(!GetIsPC(pc)||GetLocalInt(m,"started"))return;
    SetLocalInt(m,"started",1);SetLocalFloat(m,"t0",DyNow());
    SetPlotFlag(pc,FALSE);SetImmortal(pc,FALSE);
    SetEventScript(m,EVENT_SCRIPT_MODULE_ON_PLAYER_DYING,"dy_dying");
    SetEventScript(m,EVENT_SCRIPT_MODULE_ON_PLAYER_DEATH,"bi_death");
    SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,40.0);
    DelayCommand(0.5,VCamera(pc,90.0,8.0,60.0));
    DelayCommand(1.0,Trace(pc));
    // I1: -1 per round, instant, from -1: a tick every 6 s? At +20 s every effect is removed while she is dying.
    DelayCommand(2.0,Hurt(pc,-1));
    DelayCommand(2.5,Bleed(pc,-1.0));
    DelayCommand(22.5,Strip(pc,"i1"));
    DelayCommand(31.0,Raise(pc));
    // I2: -2 per round from -1: every 3 s?
    DelayCommand(33.0,Hurt(pc,-1));
    DelayCommand(33.5,Bleed(pc,-2.0));
    DelayCommand(44.0,Raise(pc));
    // I3: +2 per round from -3: up by one every 3 s, stopping at 1?
    DelayCommand(46.0,Hurt(pc,-3));
    DelayCommand(46.5,Bleed(pc,2.0));
    DelayCommand(62.0,Raise(pc));
    // I4: -0.5 per round from -1: every 12 s?
    DelayCommand(64.0,Hurt(pc,-1));
    DelayCommand(64.5,Bleed(pc,-0.5));
    DelayCommand(90.0,Raise(pc));
    // T1: a 4 s temporary -1 removed by script after 2 s: a tick at the removal, then on every 4 s?
    DelayCommand(92.0,Hurt(pc,-1));
    DelayCommand(92.5,BleedFor(pc,-1.0,4.0));
    DelayCommand(94.5,Strip(pc,"t1"));
    DelayCommand(106.0,Raise(pc));
    // I5: -1 instant from -8: death at -10, then raised.
    DelayCommand(108.0,Hurt(pc,-8));
    DelayCommand(108.5,Bleed(pc,-1.0));
    DelayCommand(124.0,Raise(pc));
    // D2: -1 instant from -9: the first tick kills. D3: removed at -9: the removal's tick kills. D4: a 2 s temporary
    // -1 from -9 (dyingfx G4 read -11 after such a death).
    DelayCommand(126.0,Hurt(pc,-9));
    DelayCommand(126.5,Bleed(pc,-1.0));
    DelayCommand(134.0,Raise(pc));
    DelayCommand(136.0,Hurt(pc,-9));
    DelayCommand(136.5,Bleed(pc,-1.0));
    DelayCommand(138.5,Strip(pc,"d3"));
    DelayCommand(141.0,Raise(pc));
    DelayCommand(143.0,Hurt(pc,-9));
    DelayCommand(143.5,BleedFor(pc,-1.0,2.0));
    DelayCommand(148.0,Raise(pc));
    DelayCommand(150.0,VShot("a_end"));
    DelayCommand(152.0,Stop());
}
