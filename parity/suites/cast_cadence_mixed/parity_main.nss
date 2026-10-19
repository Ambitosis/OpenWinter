#include "pcombat_inc"
#include "parity_time"
void Haste(object o,int on){
 effect e=GetFirstEffect(o);
 while(GetIsEffectValid(e)){if(GetEffectType(e)==EFFECT_TYPE_HASTE)RemoveEffect(o,e);e=GetNextEffect(o);}
 if(on)ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectHaste(),o);
 PLogF(GetTag(o)+".hasteChange"+IntToString(on),PNow()-GetLocalFloat(o,"start"),0.2);
}
void Mark(object o,int i){
 PLogF(GetTag(o)+".after"+IntToString(i),PNow()-GetLocalFloat(o,"start"),0.2);
 int n=GetLocalInt(o,"case");
 if(n==4&&i<2)Haste(o,i==0);
 if(n==5&&i<2)Haste(o,i==1);
}
void WaitEnd(object o){PLogF(GetTag(o)+".waitEnd",PNow()-GetLocalFloat(o,"start"),0.2);}
void Begin(object o){
 int n=GetLocalInt(o,"case");SetLocalFloat(o,"start",PNow());
 PLogI(GetTag(o)+".pc",GetIsPC(o));PLogI(GetTag(o)+".combat",GetIsInCombat(o));PLogI(GetTag(o)+".quickenFeat",GetHasFeat(FEAT_QUICKEN_SPELL,o));
 if(n==5||n==7)Haste(o,TRUE);
 if(n==6)DelayCommand(3.5,Haste(o,TRUE));
 if(n==7)DelayCommand(2.5,Haste(o,FALSE));
 if(n==14)DelayCommand(1.0,Haste(o,TRUE));
 if(n==15){Haste(o,TRUE);DelayCommand(0.5,Haste(o,FALSE));}
 int i;
 for(i=0;i<3;i++){
  int meta=METAMAGIC_NONE;
  if(n==1||(n==2&&i==1)||(n==3&&i!=1))meta=METAMAGIC_QUICKEN;
  int instant=(n==11&&i==0)||(n==12&&i==1)||(n==13&&i<2);
  AssignCommand(o,ActionCastSpellAtObject(0,o,meta,FALSE,0,PROJECTILE_PATH_TYPE_DEFAULT,instant));
  AssignCommand(o,ActionDoCommand(Mark(o,i)));
  if(i==0&&(n==8||n==9)){float wait=1.0;if(n==9)wait=4.0;AssignCommand(o,ActionWait(wait));AssignCommand(o,ActionDoCommand(WaitEnd(o)));}
  if(i==0&&n==10){location dest=GetLocation(o);vector v=GetPosition(o);v.x=v.x+3.0;dest=Location(GetArea(o),v,0.0);AssignCommand(o,ActionMoveToLocation(dest,FALSE));AssignCommand(o,ActionDoCommand(WaitEnd(o)));}
 }
}
void Report(object o){
 string k=GetTag(o);PLogI(k+".impacts",GetLocalInt(o,"impacts"));
 int i;for(i=0;i<3;i++){PLogI(k+".normalReady"+IntToString(i),GetMemorizedSpellReady(o,CLASS_TYPE_WIZARD,1,i));PLogI(k+".quickReady"+IntToString(i),GetMemorizedSpellReady(o,CLASS_TYPE_WIZARD,5,i));}
}
void main(){
 int n;for(n=0;n<16;n++){
  float x=5.0+IntToFloat(n%4)*18.0;float y=5.0+IntToFloat(n/4)*18.0;
  object o=PSpawn("cad_wiz",x,y);SetTag(o,IntToString(n));SetLocalInt(o,"case",n);SetPlotFlag(o,TRUE);
  float start=4.0+IntToFloat(n)*0.1;DelayCommand(start,Begin(o));DelayCommand(start+26.0,Report(o));
 }
 DelayCommand(34.0,PDone());
}
