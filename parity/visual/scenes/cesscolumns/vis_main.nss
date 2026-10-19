#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Apply(object o,int row){Note("row."+IntToString(row)+".valid."+IntToString(GetIsObjectValid(o)));ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectVisualEffect(row),o,2.0);}
void Cue(object o,string sound){Note("play."+sound+".valid."+IntToString(GetIsObjectValid(o)));AssignCommand(o,PlaySound(sound));}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);SetPlotFlag(pc,TRUE);object target=VSpawn(pc,"nw_bandit001",0.0,3.0,180.0);int i;for(i=5000;i<=5012;i++)SetEventScript(target,i,"");SetCommandable(TRUE,target);SetPlotFlag(target,TRUE);DelayCommand(1.0,VCamera(pc,90.0,14.0,65.0));
 DelayCommand(4.0,Apply(target,7001));DelayCommand(10.0,Apply(target,7002));DelayCommand(16.0,Apply(target,7003));
 DelayCommand(22.0,Cue(target,"sce_probe_fix"));DelayCommand(24.0,Cue(target,"sce_probe_legacy"));DelayCommand(26.0,Cue(target,"sce_probe_model"));DelayCommand(30.0,VDone());}
