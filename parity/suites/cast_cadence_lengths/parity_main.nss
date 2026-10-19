#include "pcombat_inc"
#include "parity_time"
void Finish(object o){PLogF(GetTag(o)+".finish",PNow()-GetLocalFloat(o,"start"),0.2);}
void Begin(object o,int spell,int meta){SetLocalFloat(o,"start",PNow());int level=1;if(meta==METAMAGIC_QUICKEN)level=5;PLogI(GetTag(o)+".prepared",GetMemorizedSpellId(o,CLASS_TYPE_WIZARD,level,spell));PLogI(GetTag(o)+".ready",GetMemorizedSpellReady(o,CLASS_TYPE_WIZARD,level,spell));AssignCommand(o,ActionCastSpellAtObject(spell,o,meta,FALSE));AssignCommand(o,ActionDoCommand(Finish(o)));}
void Report(object o){PLogI(GetTag(o)+".impacts",GetLocalInt(o,"impacts"));}
void main(){int n;for(n=0;n<16;n++){object o=PSpawn("cad_wiz",5.0+IntToFloat(n%4)*18.0,5.0+IntToFloat(n/4)*18.0);SetTag(o,IntToString(n));int mode=n/4;int meta=METAMAGIC_NONE;if(mode>=2)meta=METAMAGIC_QUICKEN;if(mode==1||mode==3)ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectHaste(),o);float start=3.0+IntToFloat(n)*0.1;DelayCommand(start,Begin(o,n%4,meta));DelayCommand(start+10.0,Report(o));}DelayCommand(17.0,PDone());}
