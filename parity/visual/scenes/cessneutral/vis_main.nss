#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Quiet(object o){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");SetCommandable(TRUE,o);SetPlotFlag(o,TRUE);}
void State(object o,string tag){int n=0;effect e=GetFirstEffect(o);while(GetIsEffectValid(e)){n++;e=GetNextEffect(o);}Note(tag+".valid."+IntToString(GetIsObjectValid(o))+".effects."+IntToString(n));}
void Apply(object o,int duration,float seconds,string tag){Note(tag+".valid."+IntToString(GetIsObjectValid(o)));ApplyEffectToObject(duration,EffectVisualEffect(205),o,seconds);DelayCommand(.1,State(o,tag+"_applied"));}
void Remove(object o,string tag){State(o,tag+"_before");effect e=GetFirstEffect(o);while(GetIsEffectValid(e)){RemoveEffect(o,e);e=GetNextEffect(o);}State(o,tag+"_after");}
void Cue(object o){Note("play.valid."+IntToString(GetIsObjectValid(o)));AssignCommand(o,PlaySound("sce_neutral"));}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);SetPlotFlag(pc,TRUE);object target=VSpawn(pc,"nw_bandit001",0.0,3.0,180.0);Quiet(target);
 DelayCommand(1.0,VCamera(pc,90.0,14.0,65.0));
 DelayCommand(4.0,Apply(target,DURATION_TYPE_TEMPORARY,2.0,"natural"));DelayCommand(5.5,VShot("natural_before"));DelayCommand(6.3,VShot("natural_cessation"));DelayCommand(7.5,State(target,"natural_after"));DelayCommand(7.5,VShot("natural_clear"));
 DelayCommand(12.0,Apply(target,DURATION_TYPE_TEMPORARY,8.0,"early"));DelayCommand(13.5,VShot("early_before"));DelayCommand(14.0,Remove(target,"early"));DelayCommand(14.3,VShot("early_cessation"));DelayCommand(15.5,VShot("early_clear"));
 DelayCommand(20.0,Apply(target,DURATION_TYPE_PERMANENT,0.0,"permanent"));DelayCommand(21.5,VShot("permanent_before"));DelayCommand(22.0,Remove(target,"permanent"));DelayCommand(22.3,VShot("permanent_cessation"));DelayCommand(23.5,VShot("permanent_clear"));
 DelayCommand(28.0,Cue(target));DelayCommand(32.0,VDone());}
