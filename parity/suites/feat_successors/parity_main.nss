#include "parity_inc"
void Read(string key, object c) {
 int f; for(f=354;f<=374;f++) { int id=f==354?257:f;
 string k=key+"."+IntToString(id);
 PLogI(k+".owns",GetHasFeat(id,c,TRUE));
 PLogI(k+".available",GetHasFeat(id,c));
 PLogI(k+".uses",GetFeatRemainingUses(id,c)); }
}
void Profile(string name) {
 object c=CreateObject(OBJECT_TYPE_CREATURE,name,PCentre());
 Read(name+".start",c);
 DecrementRemainingFeatUses(c,257);Read(name+".decBase",c);
 DecrementRemainingFeatUses(c,367);Read(name+".decRank",c);
 IncrementRemainingFeatUses(c,257);Read(name+".incBase",c);
 IncrementRemainingFeatUses(c,367);Read(name+".incRank",c);
}
void main() {
 object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_hen_sha_11",PCentre());
 PLogI("valid",GetIsObjectValid(c));
 Read("start",c);
 DecrementRemainingFeatUses(c,257);Read("decrementBase",c);
 DecrementRemainingFeatUses(c,367);Read("decrementOwned",c);
 DecrementRemainingFeatUses(c,360);Read("decrementIntermediate",c);
 DecrementRemainingFeatUses(c,368);Read("decrementUnowned",c);
 IncrementRemainingFeatUses(c,355);Read("incrementIntermediate",c);
 int i;for(i=0;i<20;i++)DecrementRemainingFeatUses(c,257);
 Read("exhausted",c);
 IncrementRemainingFeatUses(c,355);Read("restoreIntermediate",c);
 IncrementRemainingFeatUses(c,355);Read("restoreIntermediateTwice",c);
 IncrementRemainingFeatUses(c,355);Read("restoreIntermediateCap",c);
 IncrementRemainingFeatUses(c,257);Read("restoreBaseCap",c);
 IncrementRemainingFeatUses(c,367);Read("restoreOwned",c);
 IncrementRemainingFeatUses(c,368);Read("restoreUnowned",c);
 object b=CreateObject(OBJECT_TYPE_CREATURE,"nw_hen_sha_11",PCentre(4.0));
 ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(257),b);Read("bonusBase",b);
 DecrementRemainingFeatUses(b,257);Read("bonusBaseDecrement",b);
 IncrementRemainingFeatUses(b,257);Read("bonusBaseIncrement",b);
 effect highest=EffectBonusFeat(373);ApplyEffectToObject(DURATION_TYPE_PERMANENT,highest,b);Read("bonusHighest",b);
 DecrementRemainingFeatUses(b,257);Read("bonusHighestDecrement",b);
 RemoveEffect(b,highest);DelayCommand(0.2,Read("bonusHighestRemoved",b));
 Profile("fs_base");Profile("fs_both");Profile("fs_top");
 object extra=CreateObject(OBJECT_TYPE_CREATURE,"nw_hen_sha_11",PCentre());
 ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(FEAT_EXTRA_MUSIC),extra);
 Read("extraMusic",extra);DecrementRemainingFeatUses(extra,257);Read("extraMusicDecrement",extra);
 DelayCommand(0.3,PDone());
}
