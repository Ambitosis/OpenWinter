#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Positive(object target,int slot){Note("positive.slot."+IntToString(slot));PlayVoiceChat(slot,target);}
void Quiet(object o){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");SetAILevel(o,AI_LEVEL_NORMAL);}
void Damage(object target,int n,int amount){SetLocalInt(target,"phase",amount);Note("damage.request."+IntToString(n)+".amount."+IntToString(amount)+".hp."+IntToString(GetCurrentHitPoints(target)));ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDamage(amount,DAMAGE_TYPE_SLASHING),target);}
void Combat(object attacker,object target){SetLocalInt(target,"phase",10);Note("combat.seen."+IntToString(GetObjectSeen(target,attacker))+".enemy."+IntToString(GetIsEnemy(target,attacker)));AssignCommand(attacker,ActionAttack(target));}
void Stop(object attacker,object target){AssignCommand(attacker,ClearAllActions(TRUE));Note("combat.done.hp."+IntToString(GetCurrentHitPoints(target))+".dead."+IntToString(GetIsDead(target))+".damage."+IntToString(GetLocalInt(target,"damage_events"))+".attacks."+IntToString(GetLocalInt(target,"attack_events")));}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 object target=pc;Quiet(target);SetPlotFlag(target,FALSE);SetSoundset(target,5100);SetImmortal(target,TRUE);SetCommandable(FALSE,target);SetEventScript(target,EVENT_SCRIPT_CREATURE_ON_DAMAGED,"vis_damage");SetEventScript(target,EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED,"vis_atk");
 object attacker=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),Vector(30.0,31.5,0.0),270.0));Quiet(attacker);SetSoundset(attacker,5099);ChangeToStandardFaction(attacker,STANDARD_FACTION_HOSTILE);SetBaseAttackBonus(6,attacker);ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectAttackIncrease(50),attacker);SetIsTemporaryEnemy(target,attacker);SetIsTemporaryEnemy(attacker,target);
 Note("target.pc."+IntToString(GetIsPC(target))+".name."+GetName(target)+".soundset."+IntToString(GetSoundset(target))+".maxhp."+IntToString(GetMaxHitPoints(target)));
 DelayCommand(1.0,Positive(target,14));DelayCommand(1.4,Positive(target,15));DelayCommand(1.8,Positive(target,16));
 DelayCommand(3.0,Damage(target,0,5));DelayCommand(4.0,Damage(target,1,5));DelayCommand(5.0,Damage(target,2,5));
 DelayCommand(1.0,VCamera(pc,90.0,8.0,65.0));DelayCommand(2.4,VShot("ready"));DelayCommand(9.0,Combat(attacker,target));DelayCommand(59.0,Stop(attacker,target));DelayCommand(61.0,VShot("finished"));DelayCommand(62.0,Positive(target,14));DelayCommand(62.4,Positive(target,15));DelayCommand(62.8,Positive(target,16));DelayCommand(64.0,VDone());}
