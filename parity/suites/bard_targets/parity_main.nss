#include "parity_fx"
void Quiet(object o) { int i; for(i=5000;i<=5012;i++) SetEventScript(o,i,""); SetAILevel(o,AI_LEVEL_NORMAL); AssignCommand(o,ClearAllActions(TRUE)); }
object Actor(string blueprint, string tag, float x, float y) {
 object o=CreateObject(OBJECT_TYPE_CREATURE,blueprint,Location(GetObjectByTag("PARITY_AREA"),Vector(x,y,0.0),0.0),FALSE,tag); Quiet(o); return o;
}
void Report(object o, string key, int spell) {
 PLogI(key+".has",GetHasSpellEffect(spell,o)); if (spell == 411) PLog(key+".delta",PFxDiff(o)); else PLogI(key+".weakened",GetAbilityScore(o,ABILITY_STRENGTH)<GetLocalInt(o,"beforestr"));
}
void Cast(object c, int spell) { AssignCommand(c,ActionCastSpellAtObject(spell,c,METAMAGIC_NONE,TRUE,0,PROJECTILE_PATH_TYPE_DEFAULT,TRUE)); }
void Group(float y, int spell, string key) {
 object c=Actor("nw_hen_sha_11",key+"caster",20.0,y);
 object ally=Actor("nw_bandit001",key+"ally",22.0,y);
 object hench=Actor("nw_bandit001",key+"hench",18.0,y);
 object enemy=Actor("nw_bandit001",key+"enemy",20.0,y+3.0);
 object deaf=Actor("nw_bandit001",key+"deaf",20.0,y-3.0);
 object far=Actor("nw_bandit001",key+"far",34.0,y);
 SetIsTemporaryFriend(ally,c); SetIsTemporaryFriend(c,ally);
 SetIsTemporaryFriend(deaf,c); SetIsTemporaryFriend(c,deaf);
 SetIsTemporaryFriend(far,c); SetIsTemporaryFriend(c,far);
 AddHenchman(c,hench); Quiet(hench);
 SetIsTemporaryEnemy(enemy,c); SetIsTemporaryEnemy(c,enemy);
 ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectDeaf(),deaf);
 if (spell == 445) ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectSavingThrowDecrease(SAVING_THROW_FORT,50),enemy);
 PFxSnap(c); PFxSnap(ally); PFxSnap(hench); PFxSnap(enemy); PFxSnap(deaf); PFxSnap(far);
 SetLocalInt(c,"beforestr",GetAbilityScore(c,ABILITY_STRENGTH)); SetLocalInt(ally,"beforestr",GetAbilityScore(ally,ABILITY_STRENGTH));
 SetLocalInt(hench,"beforestr",GetAbilityScore(hench,ABILITY_STRENGTH)); SetLocalInt(enemy,"beforestr",GetAbilityScore(enemy,ABILITY_STRENGTH));
 SetLocalInt(deaf,"beforestr",GetAbilityScore(deaf,ABILITY_STRENGTH)); SetLocalInt(far,"beforestr",GetAbilityScore(far,ABILITY_STRENGTH));
 PLogI(key+".master",GetMaster(hench)==c); PLogI(key+".allyfriend",GetIsFriend(ally,c));
 PLogI(key+".henchfriend",GetIsFriend(hench,c)); PLogI(key+".enemy",GetIsEnemy(enemy,c));
 DelayCommand(5.0,Cast(c,spell));
 DelayCommand(28.0,Report(c,key+".caster",spell)); DelayCommand(28.0,Report(ally,key+".ally",spell));
 DelayCommand(28.0,Report(hench,key+".hench",spell)); DelayCommand(28.0,Report(enemy,key+".enemy",spell));
 DelayCommand(28.0,Report(deaf,key+".deaf",spell)); DelayCommand(28.0,Report(far,key+".far",spell));
}
void main() { Group(20.0,411,"song"); Group(55.0,445,"dirge"); DelayCommand(29.0,PDone()); }
