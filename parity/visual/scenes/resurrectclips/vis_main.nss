#include "visual_inc"
object Actor(object pc, string res, float x)
{
    object o=VSpawn(pc,res,x,2.0,270.0);
    int i; for(i=5000;i<=5012;i++) SetEventScript(o,i,"");
    AssignCommand(o,ClearAllActions(TRUE)); SetPlotFlag(o,FALSE);
    SetIsDestroyable(FALSE,TRUE,FALSE,o);
    return o;
}
void Kill(object o) { ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(),o); }
void Raise(object o) { ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectResurrection(),o); WriteTimestampedLogEntry("RAISE dead="+IntToString(GetIsDead(o))+" hp="+IntToString(GetCurrentHitPoints(o))); }
void main()
{
    object pc=GetEnteringObject(); if(!GetIsPC(pc)) return;
    VBegin(pc); VHidePC(pc);
    object a=Actor(pc,"nw_bandit001",-3.0);
    object b=Actor(pc,"nw_bandit001",-1.0);
    object c=Actor(pc,"nw_bandit001",1.0);
    object dog=Actor(pc,"nw_dog",3.0);
    ReplaceObjectAnimation(b,"gutokdb","sitcross"); ReplaceObjectAnimation(b,"gutokdf","sitcross");
    ReplaceObjectAnimation(c,"gustandb","worship"); ReplaceObjectAnimation(c,"gustandf","worship");
    DelayCommand(1.0,VCamera(pc,90.0,10.0,55.0));
    DelayCommand(3.0,Kill(a)); DelayCommand(3.0,Kill(b)); DelayCommand(3.0,Kill(c)); DelayCommand(3.0,Kill(dog));
    DelayCommand(6.0,VShot("dead"));
    DelayCommand(8.0,WriteTimestampedLogEntry("VISUAL|burst|raise|3|6"));
    DelayCommand(8.2,Raise(a)); DelayCommand(8.2,Raise(b)); DelayCommand(8.2,Raise(c)); DelayCommand(8.2,Raise(dog));
    DelayCommand(12.0,VShot("alive"));
    DelayCommand(14.0,VDone());
}
