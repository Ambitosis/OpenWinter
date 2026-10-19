#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Quiet(object o){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");SetCommandable(FALSE,o);SetPlotFlag(o,TRUE);SetIsDestroyable(TRUE,TRUE,TRUE,o);}
void Beam(object s,object t){ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectBeam(VFX_BEAM_LIGHTNING,s,BODY_NODE_HAND),t);Note("apply."+GetTag(t));}
void State(object s,object t,string label){Note(label+"."+GetTag(t)+".valid."+IntToString(GetIsObjectValid(s))+IntToString(GetIsObjectValid(t))+".distance."+FloatToString(GetDistanceBetween(s,t),0,3));}
void Shot(object s,object t,object rs,object rt,string label){State(s,t,label);State(rs,rt,label);VShot(label);}
void Remove(object t){SetPlotFlag(t,FALSE);SetIsDestroyable(TRUE,TRUE,TRUE,t);DestroyObject(t);}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);SetPlotFlag(pc,TRUE);
 object s=VSpawn(pc,"nw_bandit001",-2.0,3.0,0.0);object t=VSpawn(pc,"nw_bandit001",2.0,3.0,180.0);
 object rs=VSpawn(pc,"nw_bandit001",-2.0,6.0,0.0);object rt=VSpawn(pc,"nw_bandit001",2.0,6.0,180.0);
 SetTag(t,"single");SetTag(rt,"repeated");Quiet(s);Quiet(t);Quiet(rs);Quiet(rt);
 DelayCommand(1.0,VCamera(pc,90.0,17.0,65.0));
 DelayCommand(2.0,Beam(s,t));DelayCommand(2.0,Beam(rs,rt));
 DelayCommand(4.0,Beam(rs,rt));DelayCommand(6.0,Beam(rs,rt));DelayCommand(8.0,Beam(rs,rt));DelayCommand(10.0,Beam(rs,rt));
 DelayCommand(3.0,Shot(s,t,rs,rt,"a_1s"));DelayCommand(12.0,Shot(s,t,rs,rt,"b_10s"));
 DelayCommand(32.0,Shot(s,t,rs,rt,"c_30s"));DelayCommand(62.0,Shot(s,t,rs,rt,"d_60s"));
 DelayCommand(122.0,Shot(s,t,rs,rt,"e_120s"));DelayCommand(124.0,Remove(t));DelayCommand(124.0,Remove(rt));
 DelayCommand(126.0,Shot(s,t,rs,rt,"f_removed"));DelayCommand(128.0,VDone());}
