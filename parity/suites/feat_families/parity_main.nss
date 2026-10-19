#include "parity_inc"
void Read(string key,object c,int f) {
 PLogI(key+".owns",GetHasFeat(f,c,TRUE));PLogI(key+".available",GetHasFeat(f,c));PLogI(key+".uses",GetFeatRemainingUses(f,c));
}
void Ranks(string key,object c,int base,int highest) {
 Read(key+".base",c,base);Read(key+".highest",c,highest);
 if(base==293){int i;for(i=326;i<331;i++)Read(key+"."+IntToString(i),c,i);}
}
void Profile(string resref,int cls,int base,int highest) {
 object c=CreateObject(OBJECT_TYPE_CREATURE,resref,PCentre());
 PLogI(resref+".level",GetLevelByClass(cls,c));Ranks(resref+".start",c,base,highest);
 DecrementRemainingFeatUses(c,base);Ranks(resref+".decBase",c,base,highest);
 DecrementRemainingFeatUses(c,highest);Ranks(resref+".decRank",c,base,highest);
 IncrementRemainingFeatUses(c,base);Ranks(resref+".incBase",c,base,highest);
 IncrementRemainingFeatUses(c,highest);Ranks(resref+".incRank",c,base,highest);
 effect e=EffectBonusFeat(highest);ApplyEffectToObject(DURATION_TYPE_PERMANENT,e,c);
 Ranks(resref+".bonus",c,base,highest);
 DecrementRemainingFeatUses(c,base);Ranks(resref+".bonusDec",c,base,highest);
 RemoveEffect(c,e);DelayCommand(0.2,Ranks(resref+".removed",c,base,highest));
}
void main() {
 Profile("ff_rage",CLASS_TYPE_BARBARIAN,293,331);
 Profile("ff_rageboth",CLASS_TYPE_BARBARIAN,293,331);
 Profile("ff_ragebonus",CLASS_TYPE_BARBARIAN,293,331);
 Profile("ff_stun",CLASS_TYPE_MONK,39,39);
 Profile("ff_stunbonus",CLASS_TYPE_MONK,39,39);
 DelayCommand(0.3,PDone());
}
