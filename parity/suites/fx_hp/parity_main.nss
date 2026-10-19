// Hit points: EffectDamage against resistance, reduction, immunity and vulnerability; temporary
// hit points; EffectHeal; and what happens to the defensive effects as they absorb damage.
#include "parity_fx"

int NextCell()
{
    int n = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", n + 1);
    return n;
}

effect NoFx() { return EffectHeal(-1); }

// Applies the defences, then each hit in turn, logging hit points lost per hit and the effects left.
void Hits(string sName, effect eD1, effect eD2, effect eH1, effect eH2, effect eH3)
{
    object o = PFxSpawn(NextCell(), "nw_bandit006");
    if (GetEffectType(eD1, TRUE) != 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, eD1, o);
    if (GetEffectType(eD2, TRUE) != 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, eD2, o);
    string s = "";
    int hp = GetCurrentHitPoints(o);
    if (GetEffectType(eH1, TRUE) != 0) { ApplyEffectToObject(DURATION_TYPE_INSTANT, eH1, o); s += IntToString(hp - GetCurrentHitPoints(o)); hp = GetCurrentHitPoints(o); }
    if (GetEffectType(eH2, TRUE) != 0) { ApplyEffectToObject(DURATION_TYPE_INSTANT, eH2, o); s += " " + IntToString(hp - GetCurrentHitPoints(o)); hp = GetCurrentHitPoints(o); }
    if (GetEffectType(eH3, TRUE) != 0) { ApplyEffectToObject(DURATION_TYPE_INSTANT, eH3, o); s += " " + IntToString(hp - GetCurrentHitPoints(o)); }
    PLog("hp." + sName, s);
    PLog("hp." + sName + ".left", PFxList(o));
}

effect Dmg(int n, int t) { return EffectDamage(n, t); }
effect DmgP(int n, int t, int p) { return EffectDamage(n, t, p); }

void Resistance()
{
    Hits("resist.fire5", EffectDamageResistance(DAMAGE_TYPE_FIRE, 5), NoFx(), Dmg(10, DAMAGE_TYPE_FIRE), Dmg(3, DAMAGE_TYPE_FIRE), Dmg(10, DAMAGE_TYPE_COLD));
    Hits("resist.limit8", EffectDamageResistance(DAMAGE_TYPE_FIRE, 5, 8), NoFx(), Dmg(10, DAMAGE_TYPE_FIRE), Dmg(10, DAMAGE_TYPE_FIRE), Dmg(10, DAMAGE_TYPE_FIRE));
    Hits("resist.two", EffectDamageResistance(DAMAGE_TYPE_FIRE, 5), EffectDamageResistance(DAMAGE_TYPE_FIRE, 10), Dmg(12, DAMAGE_TYPE_FIRE), Dmg(12, DAMAGE_TYPE_FIRE), NoFx());
    Hits("resist.two.limits", EffectDamageResistance(DAMAGE_TYPE_FIRE, 5, 6), EffectDamageResistance(DAMAGE_TYPE_FIRE, 10, 10), Dmg(12, DAMAGE_TYPE_FIRE), Dmg(12, DAMAGE_TYPE_FIRE), Dmg(12, DAMAGE_TYPE_FIRE));
    Hits("resist.physical", EffectDamageResistance(DAMAGE_TYPE_SLASHING, 5), NoFx(), Dmg(10, DAMAGE_TYPE_SLASHING), Dmg(10, DAMAGE_TYPE_PIERCING), NoFx());
    Hits("resist.magical", EffectDamageResistance(DAMAGE_TYPE_MAGICAL, 5), NoFx(), Dmg(10, DAMAGE_TYPE_MAGICAL), NoFx(), NoFx());
    Hits("resist.ranged", EffectDamageResistance(DAMAGE_TYPE_FIRE, 5, 0, TRUE), NoFx(), Dmg(10, DAMAGE_TYPE_FIRE), NoFx(), NoFx());
    Hits("resist.vsalign", VersusAlignmentEffect(EffectDamageResistance(DAMAGE_TYPE_FIRE, 5), ALIGNMENT_ALL, ALIGNMENT_GOOD), NoFx(), Dmg(10, DAMAGE_TYPE_FIRE), NoFx(), NoFx());
}

void Reduction()
{
    Hits("dr.5.normal", EffectDamageReduction(5, DAMAGE_POWER_PLUS_ONE), NoFx(), DmgP(10, DAMAGE_TYPE_BLUDGEONING, DAMAGE_POWER_NORMAL), DmgP(10, DAMAGE_TYPE_BLUDGEONING, DAMAGE_POWER_PLUS_ONE), DmgP(10, DAMAGE_TYPE_BLUDGEONING, DAMAGE_POWER_PLUS_TWO));
    Hits("dr.5.fire", EffectDamageReduction(5, DAMAGE_POWER_PLUS_ONE), NoFx(), DmgP(10, DAMAGE_TYPE_FIRE, DAMAGE_POWER_NORMAL), DmgP(10, DAMAGE_TYPE_MAGICAL, DAMAGE_POWER_NORMAL), NoFx());
    Hits("dr.limit7", EffectDamageReduction(5, DAMAGE_POWER_PLUS_ONE, 7), NoFx(), DmgP(10, DAMAGE_TYPE_SLASHING, 0), DmgP(10, DAMAGE_TYPE_SLASHING, 0), DmgP(10, DAMAGE_TYPE_SLASHING, 0));
    Hits("dr.two", EffectDamageReduction(5, DAMAGE_POWER_PLUS_ONE), EffectDamageReduction(10, DAMAGE_POWER_PLUS_THREE), DmgP(20, DAMAGE_TYPE_SLASHING, DAMAGE_POWER_PLUS_ONE), DmgP(20, DAMAGE_TYPE_SLASHING, DAMAGE_POWER_PLUS_TWO), DmgP(20, DAMAGE_TYPE_SLASHING, DAMAGE_POWER_PLUS_THREE));
    Hits("dr.energy", EffectDamageReduction(5, DAMAGE_POWER_PLUS_ONE), NoFx(), DmgP(10, DAMAGE_TYPE_SLASHING, DAMAGE_POWER_ENERGY), DmgP(10, DAMAGE_TYPE_PIERCING, DAMAGE_POWER_PLUS_TWENTY), NoFx());
    Hits("dr.resist", EffectDamageReduction(5, DAMAGE_POWER_PLUS_ONE), EffectDamageResistance(DAMAGE_TYPE_SLASHING, 3), DmgP(12, DAMAGE_TYPE_SLASHING, 0), NoFx(), NoFx());
}

void Immunity()
{
    Hits("imm.50", EffectDamageImmunityIncrease(DAMAGE_TYPE_COLD, 50), NoFx(), Dmg(10, DAMAGE_TYPE_COLD), Dmg(7, DAMAGE_TYPE_COLD), Dmg(1, DAMAGE_TYPE_COLD));
    Hits("imm.100", EffectDamageImmunityIncrease(DAMAGE_TYPE_COLD, 100), NoFx(), Dmg(10, DAMAGE_TYPE_COLD), NoFx(), NoFx());
    Hits("imm.150", EffectDamageImmunityIncrease(DAMAGE_TYPE_COLD, 150), NoFx(), Dmg(10, DAMAGE_TYPE_COLD), NoFx(), NoFx());
    Hits("vuln.50", EffectDamageImmunityDecrease(DAMAGE_TYPE_COLD, 50), NoFx(), Dmg(10, DAMAGE_TYPE_COLD), Dmg(7, DAMAGE_TYPE_COLD), Dmg(1, DAMAGE_TYPE_COLD));
    Hits("vuln.100", EffectDamageImmunityDecrease(DAMAGE_TYPE_COLD, 100), NoFx(), Dmg(10, DAMAGE_TYPE_COLD), NoFx(), NoFx());
    Hits("imm.50.vuln.25", EffectDamageImmunityIncrease(DAMAGE_TYPE_COLD, 50), EffectDamageImmunityDecrease(DAMAGE_TYPE_COLD, 25), Dmg(10, DAMAGE_TYPE_COLD), Dmg(7, DAMAGE_TYPE_COLD), NoFx());
    Hits("imm.30.30", EffectDamageImmunityIncrease(DAMAGE_TYPE_COLD, 30), EffectDamageImmunityIncrease(DAMAGE_TYPE_COLD, 30), Dmg(10, DAMAGE_TYPE_COLD), NoFx(), NoFx());
    Hits("imm.resist", EffectDamageImmunityIncrease(DAMAGE_TYPE_COLD, 50), EffectDamageResistance(DAMAGE_TYPE_COLD, 5), Dmg(20, DAMAGE_TYPE_COLD), Dmg(9, DAMAGE_TYPE_COLD), NoFx());
    Hits("vuln.resist", EffectDamageImmunityDecrease(DAMAGE_TYPE_COLD, 50), EffectDamageResistance(DAMAGE_TYPE_COLD, 5), Dmg(20, DAMAGE_TYPE_COLD), Dmg(9, DAMAGE_TYPE_COLD), NoFx());
    Hits("imm.physical", EffectDamageImmunityIncrease(DAMAGE_TYPE_SLASHING, 50), EffectDamageReduction(5, DAMAGE_POWER_PLUS_ONE), DmgP(20, DAMAGE_TYPE_SLASHING, 0), NoFx(), NoFx());
}

void TempHp()
{
    Hits("temp.10", EffectTemporaryHitpoints(10), NoFx(), Dmg(4, DAMAGE_TYPE_FIRE), Dmg(4, DAMAGE_TYPE_FIRE), Dmg(4, DAMAGE_TYPE_FIRE));
    Hits("temp.two", EffectTemporaryHitpoints(5), EffectTemporaryHitpoints(5), Dmg(3, DAMAGE_TYPE_FIRE), Dmg(4, DAMAGE_TYPE_FIRE), Dmg(4, DAMAGE_TYPE_FIRE));
    Hits("temp.resist", EffectTemporaryHitpoints(10), EffectDamageResistance(DAMAGE_TYPE_FIRE, 5), Dmg(8, DAMAGE_TYPE_FIRE), NoFx(), NoFx());
    Hits("temp.heal", EffectTemporaryHitpoints(10), NoFx(), Dmg(4, DAMAGE_TYPE_FIRE), EffectHeal(20), Dmg(15, DAMAGE_TYPE_FIRE));
}

void Heal()
{
    Hits("heal.over", NoFx(), NoFx(), Dmg(10, DAMAGE_TYPE_FIRE), EffectHeal(4), EffectHeal(50));
    Hits("heal.zero", NoFx(), NoFx(), Dmg(10, DAMAGE_TYPE_FIRE), EffectHeal(0), NoFx());
    Hits("damage.zero", NoFx(), NoFx(), Dmg(0, DAMAGE_TYPE_FIRE), Dmg(-5, DAMAGE_TYPE_FIRE), NoFx());
    Hits("damage.twobits", NoFx(), NoFx(), Dmg(10, DAMAGE_TYPE_FIRE | DAMAGE_TYPE_COLD), NoFx(), NoFx());
    // Damage to plot and dead creatures; healing the dead.
    object p = PFxSpawn(NextCell(), "nw_bandit006");
    SetPlotFlag(p, TRUE);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, Dmg(10, DAMAGE_TYPE_FIRE), p);
    PLog("hp.plot", IntToString(GetCurrentHitPoints(p)));
    object d = PFxSpawn(NextCell(), "nw_bandit006");
    SetIsDestroyable(FALSE, TRUE, TRUE, d);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, Dmg(95, DAMAGE_TYPE_FIRE), d);
    PLog("hp.dead", IntToString(GetCurrentHitPoints(d)) + " " + IntToString(GetIsDead(d)));
    ApplyEffectToObject(DURATION_TYPE_INSTANT, Dmg(5, DAMAGE_TYPE_FIRE), d);
    PLog("hp.dead.damaged", IntToString(GetCurrentHitPoints(d)));
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectHeal(50), d);
    PLog("hp.dead.healed", IntToString(GetCurrentHitPoints(d)) + " " + IntToString(GetIsDead(d)));
    // Immortal creatures stop at 1.
    object i = PFxSpawn(NextCell(), "nw_bandit006");
    SetImmortal(i, TRUE);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, Dmg(200, DAMAGE_TYPE_FIRE), i);
    PLog("hp.immortal", IntToString(GetCurrentHitPoints(i)) + " " + IntToString(GetIsDead(i)));
    // Exactly to zero and to -10.
    object z = PFxSpawn(NextCell(), "nw_bandit006");
    ApplyEffectToObject(DURATION_TYPE_INSTANT, Dmg(80, DAMAGE_TYPE_FIRE), z);
    PLog("hp.zero", IntToString(GetCurrentHitPoints(z)) + " " + IntToString(GetIsDead(z)));
    // Ability damage that lowers max hit points.
    object c = PFxSpawn(NextCell(), "nw_bandit006");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_CONSTITUTION, 4), c);
    PLog("hp.con.up", IntToString(GetCurrentHitPoints(c)) + "/" + IntToString(GetMaxHitPoints(c)));
    effect e = GetFirstEffect(c);
    RemoveEffect(c, e);
    PLog("hp.con.removed", IntToString(GetCurrentHitPoints(c)) + "/" + IntToString(GetMaxHitPoints(c)));
    object c2 = PFxSpawn(NextCell(), "nw_bandit006");
    ApplyEffectToObject(DURATION_TYPE_INSTANT, Dmg(70, DAMAGE_TYPE_FIRE), c2);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityDecrease(ABILITY_CONSTITUTION, 6), c2);
    PLog("hp.con.down.wounded", IntToString(GetCurrentHitPoints(c2)) + "/" + IntToString(GetMaxHitPoints(c2)) + " " + IntToString(GetIsDead(c2)));
}

void Run()
{
    Resistance();
    DelayCommand(0.1, Reduction());
    DelayCommand(0.2, Immunity());
    DelayCommand(0.3, TempHp());
    DelayCommand(0.4, Heal());
    DelayCommand(2.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}
