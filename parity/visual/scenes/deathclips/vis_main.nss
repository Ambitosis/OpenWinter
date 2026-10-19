// Native-only clip identification by public ReplaceObjectAnimation, with an untouched control.
#include "visual_inc"
object Actor(object pc, string res, float x, float y)
{
    object o=VSpawn(pc,res,x,y,270.0);
    int i; for(i=5000;i<=5012;i++) SetEventScript(o,i,"");
    AssignCommand(o,ClearAllActions(TRUE));
    SetPlotFlag(o,FALSE);
    SetIsDestroyable(FALSE,FALSE,FALSE,o);
    return o;
}
void Kill(object o) { ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(),o); }
void main()
{
    object pc=GetEnteringObject(); if(!GetIsPC(pc)) return;
    VBegin(pc); VHidePC(pc);
    object a=Actor(pc,"nw_bandit001",-4.0,2.0);
    object b=Actor(pc,"nw_bandit001",-2.0,2.0);
    object c=Actor(pc,"nw_bandit001",0.0,2.0);
    object d=Actor(pc,"nw_bandit001",2.0,2.0);
    object dog=Actor(pc,"nw_dog",4.0,2.0);
    ReplaceObjectAnimation(b,"kdbck","sitcross"); ReplaceObjectAnimation(b,"kdfnt","worship");
    ReplaceObjectAnimation(c,"kdbckdie","sitcross"); ReplaceObjectAnimation(c,"kdfntdie","worship");
    ReplaceObjectAnimation(d,"deadbck","sitcross"); ReplaceObjectAnimation(d,"deadfnt","worship");
    DelayCommand(1.0,VCamera(pc,90.0,12.0,55.0));
    DelayCommand(3.0,VShot("alive"));
    DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|burst|death|3|6"));
    DelayCommand(4.2,Kill(a)); DelayCommand(4.2,Kill(b)); DelayCommand(4.2,Kill(c)); DelayCommand(4.2,Kill(d)); DelayCommand(4.2,Kill(dog));
    DelayCommand(8.0,VShot("dead"));
    DelayCommand(10.0,VDone());
}
