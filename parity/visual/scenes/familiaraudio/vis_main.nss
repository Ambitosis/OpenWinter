#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void State(object pc,string tag){object f=GetAssociate(ASSOCIATE_TYPE_FAMILIAR,pc);Note(tag+".pc."+IntToString(GetIsPC(pc))+".level."+IntToString(GetLevelByClass(CLASS_TYPE_WIZARD,pc))+".uses."+IntToString(GetFeatRemainingUses(FEAT_SUMMON_FAMILIAR,pc))+".familiar."+IntToString(GetIsObjectValid(f)));if(GetIsObjectValid(f))Note(tag+".resref."+GetResRef(f));}
void Finished(object pc){State(pc,"finished");}
void Cast(object pc){State(pc,"before");Note("start.318");AssignCommand(pc,ActionUseFeat(FEAT_SUMMON_FAMILIAR,pc));AssignCommand(pc,ActionDoCommand(Finished(pc)));}
void Cue(object pc){Note("play.valid."+IntToString(GetIsObjectValid(pc)));AssignCommand(pc,PlaySound("sff_summund"));}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);SetPlotFlag(pc,TRUE);DelayCommand(1.0,VCamera(pc,90.0,5.0,65.0));DelayCommand(3.0,Cast(pc));DelayCommand(3.25,VShot("conjure"));DelayCommand(4.25,VShot("arrival"));DelayCommand(5.5,State(pc,"arrived"));DelayCommand(5.5,VShot("familiar"));DelayCommand(9.0,State(pc,"late"));DelayCommand(9.0,VShot("late"));DelayCommand(12.0,Cue(pc));DelayCommand(18.0,VDone());}
