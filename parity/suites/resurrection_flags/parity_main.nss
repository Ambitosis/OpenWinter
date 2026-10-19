#include "parity_fx"
void State(string key, object o) { PLog(key,IntToString(GetIsDead(o))+":"+IntToString(GetCurrentHitPoints(o))); }
void Trial(int cell, int raiseable)
{
    object o=PFxSpawn(cell);
    int i; for(i=5000;i<=5012;i++) SetEventScript(o,i,"");
    SetIsDestroyable(FALSE,raiseable,FALSE,o);
    string key=raiseable?"allowed":"blocked";
    ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(),o);
    State(key+".dead",o);
    ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectResurrection(),o);
    State(key+".raise",o);
    SetIsDestroyable(FALSE,TRUE,FALSE,o);
    ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectResurrection(),o);
    State(key+".retry",o);
}
void main() { Trial(0,FALSE); Trial(1,TRUE); PDone(); }
