// Target OnDamaged: counts hits and damage for the target's group, then heals what it took.
#include "parity_inc"
void main()
{
    object m = GetModule();
    string g = GetLocalString(OBJECT_SELF, "G");
    int d = GetTotalDamageDealt();
    SetLocalInt(m, g + ".hits", GetLocalInt(m, g + ".hits") + 1);
    SetLocalInt(m, g + ".dmg", GetLocalInt(m, g + ".dmg") + d);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectHeal(d), OBJECT_SELF);
}
