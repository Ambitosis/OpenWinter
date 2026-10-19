#include "parity_fx"
int Feat(int i){switch(i){case 1:case 8:case 9:return FEAT_IMMUNITY_TO_SLEEP;case 3:return FEAT_AURA_OF_COURAGE;case 4:return FEAT_PURITY_OF_BODY;case 5:return FEAT_DIAMOND_BODY;case 6:return FEAT_DIVINE_HEALTH;case 7:return FEAT_VENOM_IMMUNITY;}return -1;}
int Immunity(int i){if(i<2||i>=8)return IMMUNITY_TYPE_SLEEP;if(i<4)return IMMUNITY_TYPE_FEAR;if(i==4||i==6)return IMMUNITY_TYPE_DISEASE;return IMMUNITY_TYPE_POISON;}
void Read(){int i;for(i=0;i<10;i++){object t=GetLocalObject(GetModule(),IntToString(i));string k=IntToString(i);PLogI(k+".feat",GetHasFeat(Feat(i),t));PLogI(k+".immune",GetIsImmune(t,Immunity(i)));PLog(k+".types",PFxTypes(t));}PDone();}
void main(){int i;for(i=0;i<10;i++){
 object t=CreateObject(OBJECT_TYPE_CREATURE,i==8?"nw_drowmage020":"nw_bandit001",PCentre(IntToFloat(i)*3.0,0.0));int e;for(e=5000;e<=5012;e++)SetEventScript(t,e,"");SetLocalObject(GetModule(),IntToString(i),t);
 if(Feat(i)>=0&&i!=8)ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(Feat(i)),t);
 if(i==9)ApplyEffectToObject(DURATION_TYPE_TEMPORARY,IgnoreEffectImmunity(EffectSleep()),t,10.0);
 else if(i<2||i==8)ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectSleep(),t,10.0);
 else if(i<4)ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectFrightened(),t,10.0);
 else if(i==4||i==6)ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectDisease(DISEASE_BLINDING_SICKNESS),t,10.0);
 else ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectPoison(POISON_SMALL_CENTIPEDE_POISON),t,10.0);
 }DelayCommand(0.5,Read());}
