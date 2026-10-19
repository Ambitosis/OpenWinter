// Hit points through a polymorph: going in and coming out (with and without the form's bonus hit points, with
// damage taken meanwhile), and dying while polymorphed.
// Left out: EffectDeath on a creature in the drow form leaves it alive at 1 hit point (its 30 bonus hit points
// carry it above the -11 EffectDeath leaves); fx_poly_death measures that.
#include "parity_fx"

int NextCell()
{
    int n = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", n + 1);
    return n;
}

void HP(string k, object o)
{
    PLog(k, IntToString(GetCurrentHitPoints(o)) + "/" + IntToString(GetMaxHitPoints(o)) + " con=" + IntToString(GetAbilityScore(o, ABILITY_CONSTITUTION)) + " dead=" + IntToString(GetIsDead(o)) + " fx=" + PFxList(o));
}

void Case(string k, int nPoly, int nDamage, int nKill)
{
    object o = PFxSpawn(NextCell(), "nw_bandit001");
    int i;
    for (i = 5000; i <= 5012; i++) if (i != 5008) SetEventScript(o, i, "");
    if (nKill) AssignCommand(o, SetIsDestroyable(FALSE, FALSE, FALSE));
    DelayCommand(1.0, HP(k + ".before", o));
    if (nPoly == -2) DelayCommand(1.1, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectTemporaryHitpoints(10), o, 4.0));
    if (nPoly >= 0) DelayCommand(1.1, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectPolymorph(nPoly), o, 4.0));
    DelayCommand(1.2, HP(k + ".during", o));
    if (nDamage > 0 && nKill == 0) DelayCommand(2.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(nDamage), o));
    if (nKill == 1) DelayCommand(2.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), o));
    if (nKill == 2) DelayCommand(2.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(nDamage), o));
    if (nKill == 3) DelayCommand(2.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), o));
    DelayCommand(2.3, HP(k + ".hit", o));
    DelayCommand(7.5, HP(k + ".after", o));
}

void Run()
{
    Case("drow", POLYMORPH_TYPE_MALE_DROW, 0, 0);
    Case("drow20", POLYMORPH_TYPE_MALE_DROW, 20, 0);
    Case("drow35", POLYMORPH_TYPE_MALE_DROW, 35, 0);
    Case("bear", POLYMORPH_TYPE_BROWN_BEAR, 0, 0);
    Case("bear15", POLYMORPH_TYPE_BROWN_BEAR, 15, 0);
    Case("pixie", POLYMORPH_TYPE_PIXIE, 0, 0);
    Case("pixie5", POLYMORPH_TYPE_PIXIE, 5, 0);
    Case("chicken", POLYMORPH_TYPE_CHICKEN, 0, 0);
    Case("drowdmg60", POLYMORPH_TYPE_MALE_DROW, 60, 2);
    Case("beardmg30", POLYMORPH_TYPE_BROWN_BEAR, 30, 2);
    Case("pixiedmg20", POLYMORPH_TYPE_PIXIE, 20, 2);
    Case("pixiekill", POLYMORPH_TYPE_PIXIE, 0, 3);
    Case("plainkill", -1, 0, 3);
    Case("plaindmg20", -1, 20, 2);
    Case("bearkill", POLYMORPH_TYPE_BROWN_BEAR, 0, 3);
    Case("plaintempkill", -2, 0, 3);
    DelayCommand(9.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}
