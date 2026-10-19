#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Quiet(object o){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");SetAILevel(o,AI_LEVEL_NORMAL);}
void Positive(object o,int slot){Note("positive.slot."+IntToString(slot));PlayVoiceChat(slot,o);}
void main(){
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;SetPlotFlag(pc,TRUE);SetSoundset(pc,5100);SetLocalObject(GetModule(),"pc",pc);
 object a=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),Vector(30.0,33.5,0.0),270.0));Quiet(a);SetSoundset(a,5099);ChangeToStandardFaction(a,STANDARD_FACTION_HOSTILE);SetBaseAttackBonus(1,a);ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectAttackIncrease(50),a);ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectDamageIncrease(DAMAGE_BONUS_20,DAMAGE_TYPE_SLASHING),a);SetLocalObject(GetModule(),"attacker",a);
 int i;for(i=0;i<20;i++){
  object t=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),Vector(20.0+IntToFloat(i%5)*4.0,20.0+IntToFloat(i/5)*4.0,0.0),90.0));Quiet(t);SetSoundset(t,5100);SetCommandable(FALSE,t);ChangeToStandardFaction(t,STANDARD_FACTION_COMMONER);SetEventScript(t,EVENT_SCRIPT_CREATURE_ON_DAMAGED,"vis_damage");SetEventScript(t,EVENT_SCRIPT_CREATURE_ON_DEATH,"vis_death");SetLocalInt(t,"trial",i);SetLocalObject(GetModule(),"target_"+IntToString(i),t);SetIsTemporaryEnemy(t,a);SetIsTemporaryEnemy(a,t);
 }
 DelayCommand(1.0,Positive(pc,14));DelayCommand(1.4,Positive(pc,15));DelayCommand(1.8,Positive(pc,16));DelayCommand(2.2,Positive(pc,18));DelayCommand(3.0,SetSoundset(pc,5099));
 DelayCommand(1.0,VCamera(pc,90.0,8.0,65.0));DelayCommand(3.5,VShot("ready"));DelayCommand(9.0,Note("combat.begin"));DelayCommand(9.0,ExecuteScript("vis_next",GetModule()));
}
