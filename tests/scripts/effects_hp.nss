// Effects package test: heal clamp, damage, temporary hit points, resistance, death.
// Runs one step per call, chosen by the module int "stage"; the test checks hit points between steps.
void main()
{
    object oM = GetModule();
    object oT = GetObjectByTag("fx_target");
    int nStage = GetLocalInt(oM, "stage");
    if (nStage == 1) ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectHeal(100), oT);
    else if (nStage == 2) ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(5, DAMAGE_TYPE_FIRE), oT);
    else if (nStage == 3)
    {
        ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectTemporaryHitpoints(5), oT, 60.0);
        ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(8), oT);
    }
    else if (nStage == 4)
    {
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageResistance(DAMAGE_TYPE_COLD, 5), oT);
        ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(7, DAMAGE_TYPE_COLD), oT);
    }
    else if (nStage == 5) ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(500), oT);
    else if (nStage == 6) ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectResurrection(), oT);
}
