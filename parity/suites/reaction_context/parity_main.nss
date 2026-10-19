#include "parity_inc"
void Dump(object s,object t,string key) {
 PLogI(key+".valid",GetIsObjectValid(s)&&GetIsObjectValid(t));
 PLogI(key+".sameFaction",GetFactionEqual(s,t));
 PLogI(key+".reputation",GetReputation(s,t));
 PLogI(key+".reverse",GetReputation(t,s));
 PLogI(key+".friend",GetIsFriend(t,s));PLogI(key+".enemy",GetIsEnemy(t,s));
 PLogI(key+".reactFriend",GetIsReactionTypeFriendly(t,s));PLogI(key+".reactEnemy",GetIsReactionTypeHostile(t,s));
 PLogI(key+".reactNeutral",GetIsReactionTypeNeutral(t,s));
}
object Spawn(string resref) {
 object o=CreateObject(OBJECT_TYPE_CREATURE,resref,Location(GetObjectByTag("PARITY_AREA"),Vector(30.0,30.0,0.0),0.0));
 int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");return o;
}
void Case(int n) {
 object s=Spawn(n==3?"nw_bandit001":n==7?"nw_guard":n>=5?"nw_commale":"nw_hen_sha_11");
 object t=Spawn(n>=6?"nw_commale":"nw_bandit001");
 if(n>=8){ChangeToStandardFaction(s,STANDARD_FACTION_COMMONER);ChangeToStandardFaction(t,STANDARD_FACTION_DEFENDER);}
 if(n>=9)SetStandardFactionReputation(STANDARD_FACTION_COMMONER,n==9?100:n==10?90:95,t);
 PLogI("base"+IntToString(n)+".beforeDisposition",GetReputation(s,t));
 PLogI("base"+IntToString(n)+".sourcePlot",GetPlotFlag(s));
 if(n==0)ChangeFaction(t,s);
 else if(n<5 && n!=3)SetStandardFactionReputation(STANDARD_FACTION_MERCHANT,n==1?0:n==2?50:100,t);
 string key="base"+IntToString(n);
 Dump(s,t,key+".initial");
 SetIsTemporaryNeutral(t,s);Dump(s,t,key+".neutral");
 SetIsTemporaryFriend(t,s);Dump(s,t,key+".friend");
 SetIsTemporaryNeutral(t,s);Dump(s,t,key+".friendNeutral");
 SetIsTemporaryEnemy(t,s);Dump(s,t,key+".enemy");
 SetIsTemporaryNeutral(t,s);Dump(s,t,key+".enemyNeutral");
 ClearPersonalReputation(t,s);Dump(s,t,key+".clear");
}
void main(){Case(0);Case(1);Case(2);Case(3);Case(4);Case(5);Case(6);Case(7);Case(8);Case(9);Case(10);Case(11);PDone();}
