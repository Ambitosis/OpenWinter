#include "visual_inc"
void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|"+s); }
void Quiet(object o) {int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");SetCommandable(FALSE,o);SetPlotFlag(o,TRUE);}
void Inspect(object o,string tag) {
    effect e=GetFirstEffect(o);int found=FALSE;
    while(GetIsEffectValid(e)) {
        if(GetEffectTag(e)==tag) {found=TRUE;Note(tag+".type."+IntToString(GetEffectDurationType(e))+".duration."+IntToString(GetEffectDuration(e)));}
        e=GetNextEffect(o);
    }
    Note(tag+".found."+IntToString(found));
}
void Apply(object pc,object target,int vfx,float duration,string tag) {
    Note(tag+".valid."+IntToString(GetIsObjectValid(target))+".distance."+FloatToString(GetDistanceBetween(pc,target)));
    AssignCommand(pc,ApplyEffectToObject(DURATION_TYPE_TEMPORARY,TagEffect(EffectVisualEffect(vfx),tag),target,duration));
    DelayCommand(0.1,Inspect(target,tag));
}
void main() {
    object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);SetPlotFlag(pc,TRUE);
    object near=VSpawn(pc,"nw_bandit001",0.0,3.0,270.0);Quiet(near);
    object far=VSpawn(pc,"nw_bandit001",0.0,20.0,270.0);Quiet(far);
    DelayCommand(1.0,VCamera(pc,90.0,14.0,65.0));
    DelayCommand(4.0,Apply(pc,near,VFX_IMP_MIRV,0.2,"near"));
    DelayCommand(4.1,VShot("near"));
    DelayCommand(4.4,Inspect(near,"near"));
    DelayCommand(10.0,Apply(pc,far,VFX_IMP_MIRV,3.0,"far"));
    DelayCommand(10.5,VShot("far"));
    DelayCommand(13.2,Inspect(far,"far"));
    DelayCommand(16.0,Apply(pc,pc,VFX_DUR_ELEMENTAL_SHIELD,0.8,"shield"));
    DelayCommand(17.0,Inspect(pc,"shield"));
    DelayCommand(18.0,VShot("shield_tail"));
    DelayCommand(26.0,VDone());
}
