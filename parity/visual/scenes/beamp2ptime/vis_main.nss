#include "visual_inc"
void Note(string k,string v){WriteTimestampedLogEntry("VISUAL|note|"+k+"|"+v);}
void Quiet(object o){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");SetCommandable(FALSE,o);SetPlotFlag(o,TRUE);}
void Beam(object s,object t,int i){string label="case"+IntToString(i);string model=Get2DAString("progfx","Param1",600+i);Note(label+".model",model);if(model!="pt_"+IntToString(i)){Note("gate_fail",label);return;}Note(label+".objects",IntToString(GetIsObjectValid(s))+"."+IntToString(GetIsObjectValid(t)));ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectBeam(210+i,s,BODY_NODE_CHEST),t,3.2);DelayCommand(.25,VShot(label+"_025"));DelayCommand(.55,VShot(label+"_055"));DelayCommand(1.1,VShot(label+"_110"));DelayCommand(2.1,VShot(label+"_210"));DelayCommand(3.7,VShot(label+"_clear"));}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);object s=VSpawn(pc,"nw_skeleton",-2.0,0.0,0.0);object t=VSpawn(pc,"nw_bandit001",2.0,0.0,180.0);Quiet(s);Quiet(t);DelayCommand(1.0,VCamera(pc,270.0,7.0,65.0));DelayCommand(2.0,VShot("baseline"));
DelayCommand(3.0,Beam(s,t,0));
DelayCommand(8.0,Beam(s,t,1));
DelayCommand(13.0,Beam(s,t,2));
DelayCommand(18.0,Beam(s,t,3));
DelayCommand(23.0,Beam(s,t,4));
DelayCommand(28.0,VDone());}
