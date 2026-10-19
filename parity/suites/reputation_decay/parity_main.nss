#include "parity_inc"
void Read(object s,object t,string key,int time){PLogI(key+".t"+IntToString(time),GetReputation(s,t));}
object Spawn(int faction) {
 object o=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetObjectByTag("PARITY_AREA"),Vector(30.0,30.0,0.0),0.0));
 int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");
 ChangeToStandardFaction(o,faction);return o;
}
void Case(int base,int mode,float duration=10.0) {
 object s=Spawn(base+1);object t=Spawn(STANDARD_FACTION_HOSTILE);
 SetStandardFactionReputation(base+1,base*50,t);
 string key="base"+IntToString(base*50)+".mode"+IntToString(mode);
 if(duration!=10.0)key+=".duration"+IntToString(FloatToInt(duration));
 PLogI(key+".before",GetReputation(s,t));
 if(mode==0)SetIsTemporaryFriend(t,s,TRUE,duration);
 if(mode==1)SetIsTemporaryEnemy(t,s,TRUE,duration);
 if(mode==2)SetIsTemporaryNeutral(t,s,TRUE,duration);
 Read(s,t,key,0);
 if(duration==10.0 && ((base==0 && mode==0)||(base==2 && mode==2))) {
  int i;for(i=1;i<=19;i++)DelayCommand(IntToFloat(i)*0.5,Read(s,t,key+".half",i));
 }
 DelayCommand(2.0,Read(s,t,key,2));DelayCommand(5.0,Read(s,t,key,5));
 DelayCommand(8.0,Read(s,t,key,8));DelayCommand(11.0,Read(s,t,key,11));
}
void main(){int b;int m;for(b=0;b<3;b++)for(m=0;m<3;m++)Case(b,m);Case(0,0,30.0);Case(0,0,180.0);DelayCommand(12.0,PDone());}
