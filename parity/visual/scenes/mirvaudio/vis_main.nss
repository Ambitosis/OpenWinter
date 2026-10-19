#include "visual_inc"
void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|"+s); }
void Quiet(object o) {
    int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");
    SetCommandable(FALSE,o);SetPlotFlag(o,TRUE);
}
void Mirv(object pc,object target,string label) {
    Note(label+".valid."+IntToString(GetIsObjectValid(target))+".distance."+FloatToString(GetDistanceBetween(pc,target)));
    AssignCommand(pc,ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectVisualEffect(VFX_IMP_MIRV),target));
}
void Impact(object pc,int temporary,string label) {
    Note(label);
    AssignCommand(pc,ApplyEffectToObject(temporary?DURATION_TYPE_TEMPORARY:DURATION_TYPE_INSTANT,EffectVisualEffect(VFX_IMP_MAGBLUE),pc,0.8));
}
void main() {
    object pc=GetEnteringObject();if(!GetIsPC(pc))return;
    VBegin(pc);SetPlotFlag(pc,TRUE);
    object near=VSpawn(pc,"nw_bandit001",0.0,3.0,270.0);Quiet(near);
    object far=VSpawn(pc,"nw_bandit001",0.0,20.0,270.0);Quiet(far);
    DelayCommand(1.0,VCamera(pc,90.0,14.0,65.0));
    DelayCommand(4.0,Mirv(pc,near,"near"));
    DelayCommand(4.2,VShot("near"));
    DelayCommand(14.0,Mirv(pc,far,"far"));
    DelayCommand(14.5,VShot("far"));
    DelayCommand(24.0,Impact(pc,FALSE,"instant"));
    DelayCommand(27.5,VShot("instant_tail"));
    DelayCommand(34.0,Impact(pc,TRUE,"temporary"));
    DelayCommand(35.0,VShot("temporary_tail"));
    DelayCommand(44.0,VDone());
}
