#include "parity_inc"
void Test(int input,int cell) {
 object o=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(IntToFloat(cell)*2.0));
 effect e=EffectMovementSpeedIncrease(input); string k="speed."+IntToString(input);
 PLogI(k+".valid",GetIsEffectValid(e)); PLogI(k+".before",GetEffectInteger(e,0));
 ApplyEffectToObject(DURATION_TYPE_PERMANENT,e,o);
 e=GetFirstEffect(o); while(GetIsEffectValid(e)) { if(GetEffectType(e,TRUE)==EFFECT_TYPE_MOVEMENT_SPEED_INCREASE) PLogI(k+".applied",GetEffectInteger(e,0)); e=GetNextEffect(o); }
}
void main() { PLogI("difficulty",GetGameDifficulty()); Test(-150,9); Test(-100,10); Test(-10,0); Test(0,1); Test(1,2); Test(20,3); Test(50,4); Test(99,5); Test(100,6); Test(150,7); Test(200,8); PDone(); }
