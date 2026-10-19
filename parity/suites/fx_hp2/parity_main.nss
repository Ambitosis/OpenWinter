// Hit point details: rounding of damage immunity and vulnerability, odd damage amounts, the hit
// points of creatures killed by damage, and how Constitution changes move current and maximum hit
// points over time.
#include "parity_fx"

int NextCell()
{
    int n = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", n + 1);
    return n;
}

object Make(string sResRef = "nw_bandit006")
{
    object o = PFxSpawn(NextCell(), sResRef);
    int i;
    for (i = 5000; i <= 5012; i++) if (i != 5008) SetEventScript(o, i, "");
    return o;
}

// Damage taken from hits of 1..12 cold with the given immunity (negative: vulnerability).
void Rounding(int nPercent)
{
    object o = Make();
    if (nPercent > 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageImmunityIncrease(DAMAGE_TYPE_COLD, nPercent), o);
    else ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageImmunityDecrease(DAMAGE_TYPE_COLD, -nPercent), o);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectTemporaryHitpoints(500), o);
    string s = "";
    int n;
    for (n = 1; n <= 12; n++)
    {
        int hp = GetCurrentHitPoints(o);
        ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(n, DAMAGE_TYPE_COLD), o);
        s += IntToString(hp - GetCurrentHitPoints(o)) + " ";
    }
    PLog("round." + IntToString(nPercent), s);
}

void Amounts()
{
    object o = Make();
    string s = "";
    int i;
    for (i = 0; i < 6; i++)
    {
        int n = i == 0 ? -1 : i == 1 ? -5 : i == 2 ? -100 : i == 3 ? 0 : i == 4 ? 10000 : -10000;
        int hp = GetCurrentHitPoints(o);
        ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(n, DAMAGE_TYPE_FIRE), o);
        s += IntToString(n) + ":" + IntToString(hp - GetCurrentHitPoints(o)) + " ";
        if (GetIsDead(o)) break;
    }
    PLog("amounts", s);
    object h = Make();
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(30), h);
    int hp = GetCurrentHitPoints(h);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectHeal(5000), h);
    PLog("heal.big", IntToString(GetCurrentHitPoints(h) - hp));
}

void Deaths()
{
    int i;
    string s = "";
    for (i = 0; i < 5; i++)
    {
        object o = Make();
        SetIsDestroyable(FALSE, TRUE, TRUE, o);
        int n = i == 0 ? 80 : i == 1 ? 85 : i == 2 ? 90 : i == 3 ? 91 : 120;
        ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(n, DAMAGE_TYPE_FIRE), o);
        s += IntToString(n) + ":" + IntToString(GetCurrentHitPoints(o)) + "/" + IntToString(GetIsDead(o)) + " ";
    }
    PLog("death.hp", s);
    object d = Make();
    SetIsDestroyable(FALSE, TRUE, TRUE, d);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(50, DAMAGE_TYPE_FIRE), d);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), d);
    PLog("death.effect.wounded", IntToString(GetCurrentHitPoints(d)));
    object e = Make();
    SetIsDestroyable(FALSE, TRUE, TRUE, e);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(95, DAMAGE_TYPE_FIRE), e);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectResurrection(), e);
    PLog("resurrect.hp", IntToString(GetCurrentHitPoints(e)) + " " + IntToString(GetIsDead(e)));
}

void Hp(string sKey, object o)
{
    PLog(sKey, IntToString(GetCurrentHitPoints(o)) + "/" + IntToString(GetMaxHitPoints(o)) + " dead=" + IntToString(GetIsDead(o)));
}

void RemoveAll(object o, int nType)
{
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { if (GetEffectType(e) == nType) RemoveEffect(o, e); e = GetNextEffect(o); }
}

void Constitution()
{
    // Up and back down, at full health.
    object a = Make();
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_CONSTITUTION, 4), a);
    Hp("con.up.0", a);
    DelayCommand(1.0, Hp("con.up.1", a));
    DelayCommand(2.0, RemoveAll(a, EFFECT_TYPE_ABILITY_INCREASE));
    DelayCommand(2.0, Hp("con.removed.0", a));
    DelayCommand(3.0, Hp("con.removed.1", a));
    // Up while wounded.
    object b = Make();
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(30, DAMAGE_TYPE_FIRE), b);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_CONSTITUTION, 4), b);
    Hp("con.up.wounded.0", b);
    DelayCommand(1.0, Hp("con.up.wounded.1", b));
    DelayCommand(2.0, RemoveAll(b, EFFECT_TYPE_ABILITY_INCREASE));
    DelayCommand(3.0, Hp("con.up.wounded.removed", b));
    // Down at full health, and down while badly wounded.
    object c = Make();
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityDecrease(ABILITY_CONSTITUTION, 6), c);
    Hp("con.down.0", c);
    DelayCommand(1.0, Hp("con.down.1", c));
    DelayCommand(2.0, RemoveAll(c, EFFECT_TYPE_ABILITY_DECREASE));
    DelayCommand(3.0, Hp("con.down.removed", c));
    object d = Make();
    SetIsDestroyable(FALSE, TRUE, TRUE, d);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(70, DAMAGE_TYPE_FIRE), d);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityDecrease(ABILITY_CONSTITUTION, 6), d);
    Hp("con.down.wounded.0", d);
    DelayCommand(1.0, Hp("con.down.wounded.1", d));
    // Temporary hit points and a Constitution bonus together.
    object t = Make();
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectTemporaryHitpoints(10), t);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_CONSTITUTION, 2), t);
    Hp("con.temp.0", t);
    // Constitution on a level 1 creature down to a negative bonus.
    object l = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityDecrease(ABILITY_CONSTITUTION, 8), l);
    Hp("con.low.level1.0", l);
    DelayCommand(1.0, Hp("con.low.level1.1", l));
}

void Run()
{
    Rounding(10);
    Rounding(25);
    Rounding(50);
    Rounding(75);
    Rounding(-25);
    Rounding(-50);
    Rounding(-75);
    DelayCommand(0.2, Amounts());
    DelayCommand(0.4, Deaths());
    DelayCommand(0.6, Constitution());
    DelayCommand(5.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}
