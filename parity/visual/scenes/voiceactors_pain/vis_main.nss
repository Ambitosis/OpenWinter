#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Quiet(object o){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");SetAILevel(o,AI_LEVEL_NORMAL);}
void Damage(object target,int n,int amount){SetLocalInt(target,"phase",amount);Note("damage.request."+IntToString(n)+".amount."+IntToString(amount)+".hp."+IntToString(GetCurrentHitPoints(target)));ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDamage(amount,DAMAGE_TYPE_MAGICAL),target);}
void Combat(object attacker,object target){SetLocalInt(target,"phase",10);Note("combat.seen."+IntToString(GetObjectSeen(target,attacker))+".enemy."+IntToString(GetIsEnemy(target,attacker)));AssignCommand(attacker,ActionAttack(target));}
void Stop(object attacker,object target){AssignCommand(attacker,ClearAllActions(TRUE));Note("combat.done.hp."+IntToString(GetCurrentHitPoints(target))+".dead."+IntToString(GetIsDead(target))+".events."+IntToString(GetLocalInt(target,"damage_events")));}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;SetPlotFlag(pc,TRUE);SetSoundset(pc,5099);
 object target=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),Vector(30.0,32.0,0.0),90.0));Quiet(target);SetSoundset(target,5100);SetImmortal(target,TRUE);SetCommandable(FALSE,target);SetEventScript(target,EVENT_SCRIPT_CREATURE_ON_DAMAGED,"vis_damage");ChangeToStandardFaction(target,STANDARD_FACTION_COMMONER);
 object attacker=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),Vector(30.0,33.5,0.0),270.0));Quiet(attacker);SetSoundset(attacker,5099);ChangeToStandardFaction(attacker,STANDARD_FACTION_HOSTILE);SetBaseAttackBonus(6,attacker);ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectAttackIncrease(50),attacker);SetIsTemporaryEnemy(target,attacker);SetIsTemporaryEnemy(attacker,target);
 Note("target.valid."+IntToString(GetIsObjectValid(target))+".soundset."+IntToString(GetSoundset(target))+".maxhp."+IntToString(GetMaxHitPoints(target)));
 DelayCommand(1.0,PlayVoiceChat(14,target));DelayCommand(1.4,PlayVoiceChat(15,target));DelayCommand(1.8,PlayVoiceChat(16,target));
 int i;for(i=0;i<10;i++){DelayCommand(3.0+IntToFloat(i),Damage(target,i,1));DelayCommand(15.0+IntToFloat(i),Damage(target,10+i,5));}
 DelayCommand(1.0,VCamera(pc,90.0,8.0,65.0));DelayCommand(2.4,VShot("ready"));DelayCommand(29.0,Combat(attacker,target));DelayCommand(59.0,Stop(attacker,target));DelayCommand(60.0,VShot("finished"));DelayCommand(62.0,PlayVoiceChat(14,target));DelayCommand(64.0,VDone());}
