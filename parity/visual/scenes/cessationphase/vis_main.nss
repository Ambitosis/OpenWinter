#include "visual_inc"
void Apply(object o,int v,float duration){
    WriteTimestampedLogEntry("VISUAL|note|apply."+IntToString(v));
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectVisualEffect(v),o,duration);
}
void Clear(object o){
    WriteTimestampedLogEntry("VISUAL|note|remove."+GetTag(o));
    effect e=GetFirstEffect(o);
    while(GetIsEffectValid(e)){RemoveEffect(o,e);e=GetFirstEffect(o);}
}
void main(){
    object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);
    object neg=VSpawn(pc,"nw_bandit001",2.0,0.0,90.0);
    DelayCommand(1.0,VCamera(pc,90.0,7.0,65.0));
    DelayCommand(2.0,Apply(pc,206,20.0));
    DelayCommand(2.0,Apply(neg,207,20.0));
    DelayCommand(2.3,VShot("applied"));
    DelayCommand(3.3,VShot("duration1"));
    DelayCommand(5.1,VShot("duration3"));
    DelayCommand(6.0,Clear(pc));DelayCommand(6.0,Clear(neg));
    DelayCommand(6.12,VShot("remove012"));
    DelayCommand(6.4,VShot("remove040"));
    DelayCommand(7.1,VShot("remove110"));
    DelayCommand(8.3,VShot("removed_clear"));
    DelayCommand(9.0,Apply(pc,206,1.0));
    DelayCommand(9.0,Apply(neg,207,1.0));
    DelayCommand(9.4,VShot("short_duration"));
    DelayCommand(10.15,VShot("expiry015"));
    DelayCommand(10.5,VShot("expiry050"));
    DelayCommand(12.3,VShot("expired_clear"));
    DelayCommand(13.0,VDone());
}
