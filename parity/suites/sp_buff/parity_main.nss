// Spells: buffs and heals cast (cheat, instant) by a 10th level wizard and cleric. For each: the
// effects that land, their durations, creator and spell id, the stat changes and GetHasSpellEffect.
#include "parity_fx"

int NextCell()
{
    int n = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", n + 1);
    return n;
}

// Every effect on o with duration and creator. Rolled amounts (ability bonuses, temporary hit
// points) show as "r": single rolls are compared as statistics elsewhere.
string Detail(object o)
{
    string s = "";
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        if (s != "") s += " ";
        int t = GetEffectType(e, TRUE);
        string sInts = t == EFFECT_TYPE_ABILITY_INCREASE ? IntToString(GetEffectInteger(e, 0)) + ",r"
            : t == EFFECT_TYPE_TEMPORARY_HITPOINTS ? "r" : PFxInts(e);
        s += IntToString(t) + ":" + IntToString(GetEffectSubType(e)) + ":" + IntToString(GetEffectDurationType(e)) + ":" + IntToString(GetEffectSpellId(e))
            + "[" + sInts + "]d" + IntToString(GetEffectDuration(e)) + "c" + PFxCreator(e) + "l" + IntToString(GetEffectCasterLevel(e));
        e = GetNextEffect(o);
    }
    return s == "" ? "-" : s;
}

void Report(string sKey, object o, int nSpell)
{
    // Spells with rolled amounts only report what changed, not by how much.
    if (GetLocalInt(o, "again")) { }
    else if (GetLocalInt(o, "rolled")) PLog(sKey + ".changed", PFxDiff(o) == "-" ? "0" : "1");
    else PLog(sKey + ".delta", PFxDiff(o));
    PLog(sKey + ".effects", Detail(o));
    PLog(sKey + ".has", IntToString(GetHasSpellEffect(nSpell, o)));
}

void Cast(string sKey, object oCaster, int nSpell, object oTarget, int nMeta = METAMAGIC_NONE)
{
    PFxSnap(oTarget);
    AssignCommand(oCaster, ActionCastSpellAtObject(nSpell, oTarget, nMeta, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    DelayCommand(4.0, Report(sKey, oTarget, nSpell));
}

// Casters cast at themselves: casting at another creature needs it to be seen first (see sp_target).
void Self(string sKey, string sResRef, int nSpell, int nMeta = METAMAGIC_NONE, int nDamage = 0, float fX = 0.0, float fY = 0.0)
{
    // Area spells go far from everything else (the area is 80 m across).
    object o = fY > 0.0 ? CreateObject(OBJECT_TYPE_CREATURE, sResRef, PCentre(fX, fY)) : PFxSpawn(NextCell(), sResRef);
    SetAILevel(o, AI_LEVEL_NORMAL);
    SetTag(o, "C_" + IntToString(GetLocalInt(GetModule(), "cell")));
    if (nSpell == SPELL_BULLS_STRENGTH || nSpell == SPELL_CATS_GRACE || nSpell == SPELL_CURE_LIGHT_WOUNDS) SetLocalInt(o, "rolled", TRUE);
    if (nDamage > 0) ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(nDamage), o);
    DelayCommand(1.0, Cast(sKey, o, nSpell, o, nMeta));
}

void Buffs()
{
    Self("magearmor", "nw_drowmage010", SPELL_MAGE_ARMOR);
    Self("haste", "nw_drowmage010", SPELL_HASTE);
    Self("stoneskin", "nw_drowmage010", SPELL_STONESKIN);
    Self("invisibility", "nw_drowmage010", SPELL_INVISIBILITY);
    Self("magearmor.extended", "nw_drowmage010", SPELL_MAGE_ARMOR, METAMAGIC_EXTEND);
    Self("bullstrength.max", "nw_drowmage010", SPELL_BULLS_STRENGTH, METAMAGIC_MAXIMIZE);
    Self("catsgrace.empower", "nw_drowmage010", SPELL_CATS_GRACE, METAMAGIC_EMPOWER);
    Self("bless", "nw_drowcler010", SPELL_BLESS, METAMAGIC_NONE, 0, -15.0, 45.0);
    Self("clw", "nw_drowcler010", SPELL_CURE_LIGHT_WOUNDS, METAMAGIC_MAXIMIZE, 20);
    Self("clw.empower", "nw_drowcler010", SPELL_CURE_LIGHT_WOUNDS, METAMAGIC_EMPOWER, 20);
    Self("barkskin", "nw_drowcler010", SPELL_BARKSKIN);
    Self("shieldoffaith", "nw_drowcler010", SPELL_SHIELD_OF_FAITH);
    Self("magearmor.barbarian", "nw_bandit006", SPELL_MAGE_ARMOR);
    Self("bless.barbarian", "nw_bandit006", SPELL_BLESS, METAMAGIC_NONE, 0, 15.0, 45.0);
    Self("magearmor.lvl1", "nw_bandit001", SPELL_MAGE_ARMOR);
    Self("magearmor.wiz5", "nw_drowmage005", SPELL_MAGE_ARMOR);
    Self("magearmor.ftr13", "nw_dwarfmerc005", SPELL_MAGE_ARMOR);
    Self("magearmor.wiz12", "nw_elfmerc004", SPELL_MAGE_ARMOR);
    Self("clw.undead", "nw_skeleton", SPELL_CURE_LIGHT_WOUNDS, METAMAGIC_MAXIMIZE);
}

void Again(string sKey, object o, int nSpell, int nMeta)
{
    SetLocalInt(o, "again", TRUE);
    Cast(sKey, o, nSpell, o, nMeta);
}

// The same buff cast twice on one target, and two different stacking buffs.
void Twice()
{
    object oT = PFxSpawn(NextCell(), "nw_drowmage010");
    AssignCommand(oT, ActionCastSpellAtObject(SPELL_MAGE_ARMOR, oT, METAMAGIC_NONE, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    DelayCommand(2.0, Again("magearmor.twice", oT, SPELL_MAGE_ARMOR, METAMAGIC_NONE));
    object oU = PFxSpawn(NextCell(), "nw_drowmage010");
    AssignCommand(oU, ActionCastSpellAtObject(SPELL_BULLS_STRENGTH, oU, METAMAGIC_MAXIMIZE, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    DelayCommand(2.0, Again("bull.twice", oU, SPELL_BULLS_STRENGTH, METAMAGIC_MAXIMIZE));
    object oV = PFxSpawn(NextCell(), "nw_drowmage010");
    AssignCommand(oV, ActionCastSpellAtObject(SPELL_BULLS_STRENGTH, oV, METAMAGIC_MAXIMIZE, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    DelayCommand(2.0, Again("bull.then.cats", oV, SPELL_CATS_GRACE, METAMAGIC_MAXIMIZE));
    object oW = CreateObject(OBJECT_TYPE_CREATURE, "nw_drowcler010", PCentre(45.0, 45.0));
    SetAILevel(oW, AI_LEVEL_NORMAL);
    AssignCommand(oW, ActionCastSpellAtObject(SPELL_BLESS, oW, METAMAGIC_NONE, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    DelayCommand(2.0, Again("bless.then.aid", oW, SPELL_AID, METAMAGIC_MAXIMIZE));
}

// Random bonuses: the mean of Bull's Strength over many casts.
void BullStats()
{
    int i;
    for (i = 0; i < 30; i++)
    {
        object o = PFxSpawn(NextCell(), "nw_bandit001");
        SetLocalObject(GetModule(), "bull" + IntToString(i), o);
        AssignCommand(o, ActionCastSpellAtObject(SPELL_BULLS_STRENGTH, o, METAMAGIC_NONE, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    }
}

void BullReport()
{
    int i, nSum = 0, n = 0;
    for (i = 0; i < 30; i++)
    {
        object o = GetLocalObject(GetModule(), "bull" + IntToString(i));
        effect e = GetFirstEffect(o);
        while (GetIsEffectValid(e))
        {
            if (GetEffectType(e) == EFFECT_TYPE_ABILITY_INCREASE) { nSum += GetEffectInteger(e, 1); n++; }
            e = GetNextEffect(o);
        }
    }
    PLogI("bull.count", n);
    // 1d4+1 over 30 casts: sd of the mean 0.2, so 0.6 is three of them.
    PLogF("bull.mean", n > 0 ? IntToFloat(nSum) / IntToFloat(n) : 0.0, 0.6);
}

void Run()
{
    Buffs();
    DelayCommand(0.2, Twice());
    DelayCommand(0.4, BullStats());
    DelayCommand(6.0, BullReport());
    DelayCommand(8.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}
