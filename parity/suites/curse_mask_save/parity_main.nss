#include "parity_fx"

void AddImmunity(object target)
{
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectImmunity(IMMUNITY_TYPE_ABILITY_DECREASE), target, 1.0);
}

void CopySavedCurse()
{
    object source = GetLocalObject(GetModule(), "case0");
    object target = GetLocalObject(GetModule(), "case4");
    effect e = GetFirstEffect(source);
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e) == EFFECT_TYPE_CURSE)
        {
            ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, target);
            return;
        }
        e = GetNextEffect(source);
    }
}

void Report(string phase)
{
    int i;
    for (i = 0; i < 7; i++)
    {
        object target = GetLocalObject(GetModule(), "case" + IntToString(i));
        string key = phase + "." + IntToString(i);
        PLogI(key + ".valid", GetIsObjectValid(target));
        PLog(key + ".stats", PFxDiff(target));
        PLog(key + ".effects", PFxTypes(target));
        effect e = GetFirstEffect(target);
        while (GetIsEffectValid(e))
        {
            if (GetEffectType(e) == EFFECT_TYPE_CURSE)
            {
                string payload = "";
                int a;
                for (a = 0; a < 6; a++) payload += IntToString(GetEffectInteger(e, a)) + " ";
                PLog(key + ".payload", payload);
            }
            e = GetNextEffect(target);
        }
    }
}

void main()
{
    if (GetLocalInt(GetModule(), "SETUP")) return;
    SetLocalInt(GetModule(), "SETUP", 1);
    int i;
    for (i = 0; i < 7; i++)
    {
        object target = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit006", PCentre(IntToFloat(i) * 8.0, 0.0));
        int eventId;
        for (eventId = 5000; eventId <= 5012; eventId++) SetEventScript(target, eventId, "");
        SetLocalObject(GetModule(), "case" + IntToString(i), target);
        PFxSnap(target);
        effect curse = EffectCurse(2, 2, 2, 2, 2, 2);
        if (i == 0) { AddImmunity(target); ApplyEffectToObject(DURATION_TYPE_PERMANENT, curse, target); }
        if (i == 1) ApplyEffectToObject(DURATION_TYPE_PERMANENT,
            EffectLinkEffects(curse, EffectImmunity(IMMUNITY_TYPE_ABILITY_DECREASE)), target);
        if (i == 2) { ApplyEffectToObject(DURATION_TYPE_PERMANENT, curse, target); DelayCommand(1.0, AddImmunity(target)); }
        if (i == 3) { AddImmunity(target); ApplyEffectToObject(DURATION_TYPE_PERMANENT, IgnoreEffectImmunity(curse), target); }
        if (i == 5) { ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectImmunity(IMMUNITY_TYPE_ABILITY_DECREASE), target);
            ApplyEffectToObject(DURATION_TYPE_PERMANENT, IgnoreEffectImmunity(curse), target); }
        if (i == 6) { ApplyEffectToObject(DURATION_TYPE_PERMANENT, curse, target);
            DelayCommand(1.0, ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectImmunity(IMMUNITY_TYPE_ABILITY_DECREASE), target)); }
    }
    DelayCommand(4.0, PLog("__save__", "1"));
    DelayCommand(6.0, Report("restored"));
    DelayCommand(7.0, CopySavedCurse());
    DelayCommand(9.0, Report("copied"));
    DelayCommand(11.0, PDone());
}
