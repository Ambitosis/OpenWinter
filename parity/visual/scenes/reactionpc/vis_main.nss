#include "visual_inc"
void Log(string key,int v){WriteTimestampedLogEntry("VISUAL|note|"+key+"|"+IntToString(v));}
void Dump(object s,object t,string key) {
 Log(key+".rep",GetReputation(s,t));Log(key+".reverse",GetReputation(t,s));
 Log(key+".friend",GetIsFriend(t,s));Log(key+".enemy",GetIsEnemy(t,s));
 Log(key+".reactionFriend",GetIsReactionTypeFriendly(t,s));Log(key+".reactionEnemy",GetIsReactionTypeHostile(t,s));
 Log(key+".sameFaction",GetFactionEqual(s,t));
}
void Case(object pc,int index) {
 object npc=CreateObject(OBJECT_TYPE_CREATURE,"nw_commale",Location(GetArea(pc),Vector(22.0,14.0,0.0),0.0));
 int i;for(i=5000;i<=5012;i++)SetEventScript(npc,i,"");SetCommandable(FALSE,npc);
 ChangeToStandardFaction(npc,STANDARD_FACTION_COMMONER);
 SetStandardFactionReputation(STANDARD_FACTION_COMMONER,50,pc);
 if(index==2)AddHenchman(pc,npc);
 object s=index==0?npc:pc;object t=index==0?pc:npc;
 string key="case"+IntToString(index);
 Dump(s,t,key+".before");
 SetIsTemporaryFriend(t,s);Dump(s,t,key+".friend");
 SetIsTemporaryNeutral(t,s);Dump(s,t,key+".neutral");
 SetIsTemporaryEnemy(t,s);Dump(s,t,key+".enemy");
 ClearPersonalReputation(t,s);Dump(s,t,key+".clear");
 DestroyObject(npc);
}
void Decay(object pc) {
 object npc=CreateObject(OBJECT_TYPE_CREATURE,"nw_commale",Location(GetArea(pc),Vector(22.0,14.0,0.0),0.0));
 int i;for(i=5000;i<=5012;i++)SetEventScript(npc,i,"");SetCommandable(FALSE,npc);
 SetStandardFactionReputation(STANDARD_FACTION_COMMONER,50,pc);
 ChangeToStandardFaction(npc,STANDARD_FACTION_COMMONER);
 Dump(npc,pc,"decay.before");
 SetIsTemporaryEnemy(pc,npc,TRUE,10.0);Dump(npc,pc,"decay.t0");
 DelayCommand(2.0,Dump(npc,pc,"decay.t2"));DelayCommand(5.0,Dump(npc,pc,"decay.t5"));
 DelayCommand(8.0,Dump(npc,pc,"decay.t8"));DelayCommand(11.0,Dump(npc,pc,"decay.t11"));
}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);Case(pc,0);Case(pc,1);Case(pc,2);Decay(pc);DelayCommand(1.0,VShot("fixture"));DelayCommand(13.0,VDone());}
