#include "parity_inc"
#include "aex_inc"
void Start(object t,int npc){
 if(npc)ExecuteScript("aex_apply",t);
 else {SetLocalFloat(t,"start",Clock());ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectAreaOfEffect(37,"aex_none","aex_none","aex_exit"),t,GetLocalFloat(t,"duration"));}
}
void Done(){int i;for(i=0;i<32;i++){object t=GetLocalObject(GetModule(),"t"+IntToString(i));PLogI(GetLocalString(t,"key")+".exits",GetLocalInt(t,"exits"));}PDone();}
void main(){
 int i;for(i=0;i<32;i++) {
  int v=i%8;float duration=v==0?0.1:v==1?0.5:v==2?1.5:v==3?2.5:v==4?3.5:v==5?6.5:v==6?8.5:13.5;
  int npc=(i/8)%2;float delay=i>=16?0.35:0.0;
  object t=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetObjectByTag("PARITY_AREA"),Vector(10.0+IntToFloat(i%8)*12.0,10.0+IntToFloat(i/8)*12.0,0.0),0.0));
  int j;for(j=5000;j<=5012;j++)SetEventScript(t,j,"");
  string key="source"+IntToString(npc)+".phase"+IntToString(i/16)+".duration"+FloatToString(duration,0,1);
  SetLocalString(t,"key",key);SetLocalFloat(t,"duration",duration);SetLocalObject(GetModule(),"t"+IntToString(i),t);
  DelayCommand(2.0+delay,Start(t,npc));
 }
 DelayCommand(18.0,Done());
}
