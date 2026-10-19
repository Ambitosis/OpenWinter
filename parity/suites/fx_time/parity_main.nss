// Durations: when temporary effects expire, what GetEffectDurationRemaining reports over time,
// and which effects resting, dying, resurrection and dispelling remove.
#include "parity_fx"

int NextCell()
{
    int n = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", n + 1);
    return n;
}

// Checks at 0.1 s steps (each its own delay, so delays don't add up) whether the effect type is gone
// from o, and logs the first step at which it is.
void Check(object o, int nType, string sKey, int nTick)
{
    if (GetLocalInt(o, sKey)) return;
    int bHas = FALSE;
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { if (GetEffectType(e, TRUE) == nType) bHas = TRUE; e = GetNextEffect(o); }
    if (bHas && nTick < 70) return;
    SetLocalInt(o, sKey, TRUE);
    PLogF(sKey, IntToFloat(nTick) / 10.0, 0.15);
}

void Poll(object o, int nType, string sKey, int nFrom)
{
    int i;
    for (i = 0; i <= 70; i++) DelayCommand(IntToFloat(i) / 10.0, Check(o, nType, sKey, i));
}

void Remaining(object o, string sKey)
{
    effect e = GetFirstEffect(o);
    PLog(sKey, GetIsEffectValid(e) ? IntToString(GetEffectDuration(e)) + "/" + IntToString(GetEffectDurationRemaining(e)) : "none");
}

void Expiry()
{
    float fDur;
    int i;
    for (i = 0; i < 6; i++)
    {
        fDur = i == 0 ? 0.5 : i == 1 ? 1.0 : i == 2 ? 2.4 : i == 3 ? 3.0 : i == 4 ? 4.75 : 6.0;
        object o = PFxSpawn(NextCell());
        ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectACIncrease(1), o, fDur);
        Poll(o, EFFECT_TYPE_AC_INCREASE, "expire." + FloatToString(fDur, 0, 2), 0);
    }
    // Zero and negative durations.
    object z = PFxSpawn(NextCell());
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectACIncrease(1), z, 0.0);
    PLog("expire.zero.now", PFxTypes(z));
    DelayCommand(0.5, PLog("expire.zero.later", PFxTypes(z)));
    object n = PFxSpawn(NextCell());
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectACIncrease(1), n, -3.0);
    PLog("expire.negative.now", PFxTypes(n));
    DelayCommand(0.5, PLog("expire.negative.later", PFxTypes(n)));
    // A visual effect, a link and an area of effect.
    object v = PFxSpawn(NextCell());
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectLinkEffects(EffectVisualEffect(VFX_DUR_GLOW_RED), EffectACIncrease(1)), v, 2.0);
    Poll(v, EFFECT_TYPE_VISUALEFFECT, "expire.link.vfx", 0);
    Poll(v, EFFECT_TYPE_AC_INCREASE, "expire.link.ac", 0);

    // Remaining duration over time.
    object r = PFxSpawn(NextCell());
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectACIncrease(1), r, 10.0);
    Remaining(r, "remaining.0");
    DelayCommand(0.4, Remaining(r, "remaining.0.4"));
    DelayCommand(1.6, Remaining(r, "remaining.1.6"));
    DelayCommand(3.1, Remaining(r, "remaining.3.1"));
    object h = PFxSpawn(NextCell());
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectACIncrease(1), h, HoursToSeconds(2));
    Remaining(h, "remaining.hours");
    object f = PFxSpawn(NextCell());
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectACIncrease(1), f, 7.9);
    Remaining(f, "remaining.fraction");
    object big = PFxSpawn(NextCell());
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectACIncrease(1), big, 100000.0);
    Remaining(big, "remaining.big");
    object perm = PFxSpawn(NextCell());
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(1), perm, 10.0);
    Remaining(perm, "remaining.permanent");
}

// One of each subtype and duration type, tagged by name.
void Kit(object o)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectSkillIncrease(SKILL_SPOT, 1), "pm"), o);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(ExtraordinaryEffect(EffectSkillIncrease(SKILL_SPOT, 1)), "pe"), o);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(SupernaturalEffect(EffectSkillIncrease(SKILL_SPOT, 1)), "ps"), o);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(UnyieldingEffect(EffectSkillIncrease(SKILL_SPOT, 1)), "pu"), o);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, TagEffect(EffectSkillIncrease(SKILL_SPOT, 1), "tm"), o, 300.0);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, TagEffect(ExtraordinaryEffect(EffectSkillIncrease(SKILL_SPOT, 1)), "te"), o, 300.0);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, TagEffect(SupernaturalEffect(EffectSkillIncrease(SKILL_SPOT, 1)), "ts"), o, 300.0);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, TagEffect(UnyieldingEffect(EffectSkillIncrease(SKILL_SPOT, 1)), "tu"), o, 300.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(SetEffectSpellId(EffectSkillIncrease(SKILL_SPOT, 1), SPELL_BLESS), "pmspell"), o);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(SupernaturalEffect(EffectParalyze()), "psparalyze"), o);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(UnyieldingEffect(EffectParalyze()), "puparalyze"), o);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(ExtraordinaryEffect(EffectVisualEffect(VFX_DUR_GLOW_RED)), "pevfx"), o);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(SupernaturalEffect(EffectTemporaryHitpoints(5)), "pstemphp"), o);
}

string Tags(object o)
{
    string s = "";
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { s += GetEffectTag(e) + " "; e = GetNextEffect(o); }
    return s == "" ? "-" : s;
}

void Rest()
{
    object o = PFxSpawn(NextCell());
    Kit(o);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(3), o);
    PLog("rest.before", Tags(o));
    ForceRest(o);
    PLog("rest.after", Tags(o));
    PLog("rest.hp", IntToString(GetCurrentHitPoints(o)) + "/" + IntToString(GetMaxHitPoints(o)));
    DelayCommand(1.0, PLog("rest.after1", Tags(o)));
}

void Death()
{
    object o = PFxSpawn(NextCell());
    SetIsDestroyable(FALSE, TRUE, TRUE, o);
    Kit(o);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), o);
    PLog("death.after", Tags(o));
    DelayCommand(1.0, PLog("death.after1", Tags(o)));
    DelayCommand(1.5, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectResurrection(), o));
    DelayCommand(1.6, PLog("death.resurrected", Tags(o)));
    DelayCommand(1.6, PLog("death.resurrected.hp", IntToString(GetCurrentHitPoints(o))));
    // Effects applied to a corpse.
    object c = PFxSpawn(NextCell());
    SetIsDestroyable(FALSE, TRUE, TRUE, c);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), c);
    DelayCommand(0.5, ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_SPOT, 1), c));
    DelayCommand(0.5, ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(EffectSkillIncrease(SKILL_SPOT, 1)), c));
    DelayCommand(0.5, ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectVisualEffect(VFX_DUR_GLOW_RED), c));
    DelayCommand(0.6, PLog("death.corpse", PFxTypes(c)));
    // Damage that kills.
    object k = PFxSpawn(NextCell());
    SetIsDestroyable(FALSE, TRUE, TRUE, k);
    Kit(k);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(200), k);
    PLog("death.damage", Tags(k));
}

void Dispel()
{
    object o = PFxSpawn(NextCell());
    Kit(o);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(SetEffectCasterLevel(SetEffectSpellId(EffectSkillIncrease(SKILL_HIDE, 1), SPELL_BLESS), 1), "cl1"), o);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDispelMagicAll(60), o);
    PLog("dispel.all", Tags(o));
    object p = PFxSpawn(NextCell());
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(SetEffectCasterLevel(SetEffectSpellId(EffectSkillIncrease(SKILL_HIDE, 1), SPELL_BLESS), 1), "a1"), p);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(SetEffectCasterLevel(SetEffectSpellId(EffectSkillIncrease(SKILL_HIDE, 1), SPELL_HASTE), 5), "b5"), p);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(SetEffectCasterLevel(SetEffectSpellId(EffectSkillIncrease(SKILL_HIDE, 1), SPELL_STONESKIN), 3), "c3"), p);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDispelMagicBest(60), p);
    PLog("dispel.best", Tags(p));
}

void Run()
{
    Expiry();
    DelayCommand(0.1, Rest());
    DelayCommand(0.2, Death());
    DelayCommand(0.3, Dispel());
    DelayCommand(9.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}
