#include "visual_inc"
void Apply(object pc,object target) {
    WriteTimestampedLogEntry("VISUAL|note|instant.valid."+IntToString(GetIsObjectValid(target)));
    AssignCommand(pc,ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectVisualEffect(VFX_DUR_ELEMENTAL_SHIELD),pc));
    AssignCommand(pc,ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectVisualEffect(490),pc));
    AssignCommand(pc,ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectBeam(VFX_BEAM_LIGHTNING,pc,BODY_NODE_HAND),target));
}
void main() {
    object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);SetPlotFlag(pc,TRUE);
    object target=VSpawn(pc,"nw_bandit001",0.0,3.0,270.0);
    int i;for(i=5000;i<=5012;i++)SetEventScript(target,i,"");SetCommandable(FALSE,target);SetPlotFlag(target,TRUE);
    DelayCommand(1.0,VCamera(pc,90.0,10.0,65.0));
    DelayCommand(4.0,Apply(pc,target));
    DelayCommand(4.4,VShot("instant"));
    DelayCommand(6.0,VShot("tail"));
    DelayCommand(14.0,VDone());
}
