// Skills package test: OBJECT_SELF reports trap, lock and skill state about its "watch" object.
void main()
{
    object oWatch = GetLocalObject(OBJECT_SELF, "watch");
    SetLocalInt(OBJECT_SELF, "trapped", GetIsTrapped(oWatch));
    SetLocalInt(OBJECT_SELF, "trap_detected", GetTrapDetectedBy(oWatch, OBJECT_SELF));
    SetLocalInt(OBJECT_SELF, "locked", GetLocked(oWatch));
    SetLocalInt(OBJECT_SELF, "enemy", GetIsEnemy(oWatch));
    SetLocalInt(OBJECT_SELF, "ac", GetAC(OBJECT_SELF));
    SetLocalInt(OBJECT_SELF, "hp", GetCurrentHitPoints(OBJECT_SELF));
    SetLocalObject(OBJECT_SELF, "nearest_trap", GetNearestTrapToObject(OBJECT_SELF, FALSE));
    int nEffects = 0;
    effect e = GetFirstEffect(OBJECT_SELF);
    while (GetIsEffectValid(e))
    {
        nEffects++;
        if (GetEffectType(e) == EFFECT_TYPE_AC_DECREASE) SetLocalInt(OBJECT_SELF, "ac_decrease", GetEffectInteger(e, 1));
        if (GetEffectType(e) == EFFECT_TYPE_SPELL_FAILURE) SetLocalInt(OBJECT_SELF, "spell_failure", GetEffectInteger(e, 0));
        if (GetEffectType(e) == EFFECT_TYPE_POISON) SetLocalInt(OBJECT_SELF, "poisoned", 1);
        e = GetNextEffect(OBJECT_SELF);
    }
    SetLocalInt(OBJECT_SELF, "effects", nEffects);
}
