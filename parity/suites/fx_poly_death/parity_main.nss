// EffectDeath on polymorphed creatures. In the original a bandit in the drow form survives EffectDeath at 1 hit
// point while bear and pixie forms die: which forms survive, and with which hit points before.
#include "parity_fx"

int NextCell()
{
    int n = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", n + 1);
    return n;
}

void HP(string k, object o)
{
    PLog(k, IntToString(GetCurrentHitPoints(o)) + "/" + IntToString(GetMaxHitPoints(o)) + " dead=" + IntToString(GetIsDead(o))
        + " app=" + IntToString(GetAppearanceType(o)) + " fx=" + PFxTypes(o));
}

// nPre: damage before the death (0 none). nHow: 0 EffectDeath, 1 EffectDeath(TRUE), 2 EffectDamage(200).
void Case(string k, string sRes, int nPoly, int nPre, int nHow, int nTemp = 0)
{
    object o = PFxSpawn(NextCell(), sRes);
    int i;
    for (i = 5000; i <= 5012; i++) if (i != 5008) SetEventScript(o, i, "");
    AssignCommand(o, SetIsDestroyable(FALSE, FALSE, FALSE));
    DelayCommand(1.0, HP(k + ".before", o));
    if (nPoly >= 0) DelayCommand(1.1, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectPolymorph(nPoly), o, 30.0));
    if (nTemp > 0) DelayCommand(1.2, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectTemporaryHitpoints(nTemp), o, 30.0));
    if (nPre > 0) DelayCommand(1.5, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(nPre), o));
    DelayCommand(1.8, HP(k + ".pre", o));
    effect eKill = nHow == 0 ? EffectDeath() : nHow == 1 ? EffectDeath(TRUE) : EffectDamage(200);
    DelayCommand(2.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, eKill, o));
    DelayCommand(2.1, HP(k + ".t01", o));
    DelayCommand(3.5, HP(k + ".t15", o));
}

void Run()
{
    Case("drow", "nw_bandit001", POLYMORPH_TYPE_MALE_DROW, 0, 0);
    Case("drow.spect", "nw_bandit001", POLYMORPH_TYPE_MALE_DROW, 0, 1);
    Case("drow.dmg1", "nw_bandit001", POLYMORPH_TYPE_MALE_DROW, 1, 0);
    Case("drow.dmg31", "nw_bandit001", POLYMORPH_TYPE_MALE_DROW, 31, 0);
    Case("drow.dmg35", "nw_bandit001", POLYMORPH_TYPE_MALE_DROW, 35, 0);
    Case("drow.elf", "nw_elfmage001", POLYMORPH_TYPE_MALE_DROW, 0, 0);
    Case("fdrow", "nw_bandit001", 70, 0, 0);
    Case("harpy", "nw_bandit001", 60, 0, 0);
    Case("harpy.dmg21", "nw_bandit001", 60, 21, 0);
    Case("kobold", "nw_bandit001", 83, 0, 0);
    Case("superchicken", "nw_bandit001", 67, 0, 0);
    Case("chicken", "nw_bandit001", POLYMORPH_TYPE_CHICKEN, 0, 0);
    Case("bear", "nw_bandit001", POLYMORPH_TYPE_BROWN_BEAR, 0, 0);
    Case("bear.elf", "nw_elfmage001", POLYMORPH_TYPE_BROWN_BEAR, 0, 0);
    Case("doom", "nw_bandit001", POLYMORPH_TYPE_DOOM_KNIGHT, 0, 0);
    Case("drow.dmg18", "nw_bandit001", POLYMORPH_TYPE_MALE_DROW, 18, 0);
    Case("drow.dmg19", "nw_bandit001", POLYMORPH_TYPE_MALE_DROW, 19, 0);
    Case("drow.dmg20", "nw_bandit001", POLYMORPH_TYPE_MALE_DROW, 20, 0);
    Case("temp10", "nw_bandit001", -1, 0, 0, 10);
    Case("temp11", "nw_bandit001", -1, 0, 0, 11);
    Case("temp12", "nw_bandit001", -1, 0, 0, 12);
    Case("temp20", "nw_bandit001", -1, 0, 0, 20);
    Case("temp20.dmg", "nw_bandit001", -1, 0, 2, 20);
    Case("pixie.temp20", "nw_bandit001", POLYMORPH_TYPE_PIXIE, 0, 0, 20);
    Case("bear.temp20", "nw_bandit001", POLYMORPH_TYPE_BROWN_BEAR, 0, 0, 20);
    Case("plain.elf", "nw_elfmage001", -1, 0, 0);
    DelayCommand(6.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}
