#include "pcombat_inc"
#include "parity_time"
void After(object o){PLogF(GetTag(o)+".after",PNow()-GetLocalFloat(o,"start"),0.15);}
void Clear(object o,int combat,int meta){PLogF(GetTag(o)+".clear",PNow()-GetLocalFloat(o,"start"),0.15);AssignCommand(o,ClearAllActions(combat));AssignCommand(o,ActionCastSpellAtObject(0,o,meta,FALSE));AssignCommand(o,ActionDoCommand(After(o)));}
void Begin(object o,float delay,int combat,int meta){SetLocalFloat(o,"start",PNow());AssignCommand(o,ActionCastSpellAtObject(0,o,METAMAGIC_NONE,FALSE));DelayCommand(delay,Clear(o,combat,meta));}
void Report(object o){string k=GetTag(o);PLogI(k+".impacts",GetLocalInt(o,"impacts"));int i;for(i=0;i<3;i++){PLogI(k+".normalReady"+IntToString(i),GetMemorizedSpellReady(o,CLASS_TYPE_WIZARD,1,i));PLogI(k+".quickReady"+IntToString(i),GetMemorizedSpellReady(o,CLASS_TYPE_WIZARD,5,i));}}
void Case(int n,float delay,int combat,int meta){object o=PSpawn("cad_wiz",5.0+IntToFloat(n%4)*18.0,5.0+IntToFloat(n/4)*20.0);SetTag(o,IntToString(n));SetPlotFlag(o,TRUE);float start=3.0+IntToFloat(n)*0.15;DelayCommand(start,Begin(o,delay,combat,meta));DelayCommand(start+14.0,Report(o));}
void main(){Case(0,0.25,TRUE,METAMAGIC_NONE);Case(1,0.75,TRUE,METAMAGIC_NONE);Case(2,1.25,TRUE,METAMAGIC_NONE);Case(3,1.6,TRUE,METAMAGIC_NONE);Case(4,2.2,TRUE,METAMAGIC_NONE);Case(5,0.25,FALSE,METAMAGIC_NONE);Case(6,0.75,FALSE,METAMAGIC_NONE);Case(7,1.25,FALSE,METAMAGIC_NONE);Case(8,0.25,TRUE,METAMAGIC_QUICKEN);Case(9,1.6,TRUE,METAMAGIC_QUICKEN);DelayCommand(20.0,PDone());}
