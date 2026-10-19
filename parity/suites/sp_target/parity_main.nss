// Spells cast at other creatures (cheat casts by a 10th level wizard, caster level 10, DC 13 + innate):
// damage statistics for Magic Missile and Fireball, Hold Person and Sleep save rates and effects,
// Dispel Magic against spell buffs. Casters are defenders so the hostile bandits are valid targets;
// every creature's event scripts are cleared so the AI stays out of it.
#include "parity_fx"

void Quiet(object o)
{
    int i;
    for (i = 5000; i <= 5012; i++) if (i != 5008) SetEventScript(o, i, "");
    SetAILevel(o, AI_LEVEL_NORMAL);
}

object Make(string sResRef, float fX, float fY)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetObjectByTag("PARITY_AREA"), Vector(fX, fY, 0.0), 0.0));
    Quiet(o);
    return o;
}

object Caster(float fX, float fY)
{
    object o = Make("nw_drowmage010", fX, fY);
    ChangeToStandardFaction(o, STANDARD_FACTION_DEFENDER);
    return o;
}

// The generated module has no faction table, so make the two sides enemies directly.
void Enemies(object a, object b)
{
    SetIsTemporaryEnemy(a, b);
    SetIsTemporaryEnemy(b, a);
}

// Casts only when the caster sees the target (casting at an unseen creature fails); counts the casts.
void CastAt(object oCaster, int nSpell, object oTarget)
{
    if (!GetObjectSeen(oTarget, oCaster)) return;
    SetLocalInt(oTarget, "cast", TRUE);
    AssignCommand(oCaster, ActionCastSpellAtObject(nSpell, oTarget, METAMAGIC_NONE, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
}

void CastAtLocation(object oCaster, int nSpell, location l)
{
    AssignCommand(oCaster, ActionCastSpellAtLocation(nSpell, l, METAMAGIC_NONE, TRUE, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
}

int HasType(object o, int nType)
{
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { if (GetEffectType(e, TRUE) == nType) return TRUE; e = GetNextEffect(o); }
    return FALSE;
}

// ---------------------------------------------------------------- Magic Missile

const int MM = 16;

void MissileSetup()
{
    int i;
    for (i = 0; i < MM; i++)
    {
        float y = 5.0 + IntToFloat(i) * 4.5;
        object c = Caster(5.0, y), t = Make("nw_bandit006", 7.0, y);
        Enemies(c, t);
        SetLocalObject(GetModule(), "mmc" + IntToString(i), c);
        SetLocalObject(GetModule(), "mmt" + IntToString(i), t);
    }
}

void MissileCast()
{
    int i;
    for (i = 0; i < MM; i++) CastAt(GetLocalObject(GetModule(), "mmc" + IntToString(i)), SPELL_MAGIC_MISSILE, GetLocalObject(GetModule(), "mmt" + IntToString(i)));
}

void MissileReport()
{
    int i, nSum = 0, nHit = 0, nCast = 0, nMin = 999, nMax = 0;
    for (i = 0; i < MM; i++)
    {
        object o = GetLocalObject(GetModule(), "mmt" + IntToString(i));
        if (!GetLocalInt(o, "cast")) continue;
        nCast++;
        int d = 80 - GetCurrentHitPoints(o);
        nSum += d;
        if (d > 0) nHit++;
        if (d < nMin) nMin = d;
        if (d > nMax) nMax = d;
    }
    PLogI("mm.allhit", nHit == nCast);
    PLogI("mm.inrange", nMin >= 10 && nMax <= 25);
    // Five missiles of 1d4+1: mean 17.5, sd 2.5; over a dozen targets the mean's sd is about 0.7.
    PLogF("mm.mean", IntToFloat(nSum) / IntToFloat(nCast), 2.7);
}

// ---------------------------------------------------------------- Fireball

const int FB = 12;

void FireballSetup()
{
    object oC = Caster(30.0, 11.0);
    SetLocalObject(GetModule(), "fbc", oC);
    int i;
    for (i = 0; i < FB; i++)
    {
        float a = IntToFloat(i) * 30.0;
        object o = Make("nw_bandit006", 30.0 + cos(a) * 2.5, 20.0 + sin(a) * 2.5);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectTemporaryHitpoints(150), o);
        Enemies(oC, o);
        SetLocalObject(GetModule(), "fbt" + IntToString(i), o);
    }
}

void FireballSnap()
{
    int i;
    for (i = 0; i < FB; i++)
    {
        object o = GetLocalObject(GetModule(), "fbt" + IntToString(i));
        SetLocalInt(o, "hp", GetCurrentHitPoints(o));
    }
}

void FireballTally()
{
    int i;
    for (i = 0; i < FB; i++)
    {
        object o = GetLocalObject(GetModule(), "fbt" + IntToString(i));
        int d = GetLocalInt(o, "hp") - GetCurrentHitPoints(o);
        SetLocalInt(GetModule(), "fbsum", GetLocalInt(GetModule(), "fbsum") + d);
        SetLocalInt(GetModule(), "fbn", GetLocalInt(GetModule(), "fbn") + 1);
        if (d > 0) SetLocalInt(GetModule(), "fbhit", GetLocalInt(GetModule(), "fbhit") + 1);
        if (d > 30) SetLocalInt(GetModule(), "fbfull", GetLocalInt(GetModule(), "fbfull") + 1);
    }
}

void FireballCast()
{
    FireballSnap();
    CastAtLocation(GetLocalObject(GetModule(), "fbc"), SPELL_FIREBALL, Location(GetObjectByTag("PARITY_AREA"), Vector(30.0, 20.0, 0.0), 0.0));
    DelayCommand(4.0, FireballTally());
}

void FireballReport()
{
    int n = GetLocalInt(GetModule(), "fbn");
    PLogI("fb.samples", n);
    PLogI("fb.hit", GetLocalInt(GetModule(), "fbhit"));
    // 10d6 halved on a reflex save (DC 16 against +5: half the time): mean 26.25, sd about 11.5;
    // over 36 samples the mean's sd is 1.9.
    PLogF("fb.mean", IntToFloat(GetLocalInt(GetModule(), "fbsum")) / IntToFloat(n), 8.0);
    // Damage above 30 means a failed save (a save halves 10d6 to at most 30): about 0.4 of the time;
    // the difference of two such rates over 36 samples has a sd of 0.115.
    PLogF("fb.full", IntToFloat(GetLocalInt(GetModule(), "fbfull")) / IntToFloat(n), 0.33);
}

// ---------------------------------------------------------------- Hold Person

const int HP = 44;

void HoldSetup()
{
    int i;
    for (i = 0; i < HP; i++)
    {
        float y = 5.0 + IntToFloat(i % 16) * 4.5;
        float x = i < 16 ? 45.0 : i < 32 ? 52.0 : 59.0;
        object c = Caster(x, y);
        SetLocalObject(GetModule(), "hpc" + IntToString(i), c);
        object o = Make("nw_bandit001", x + 2.0, y);
        Enemies(c, o);
        // The first four can't save but on a natural 20: their effects are compared exactly.
        if (i < 4) ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(EffectSavingThrowDecrease(SAVING_THROW_WILL, 20)), o);
        SetLocalObject(GetModule(), "hpt" + IntToString(i), o);
    }
}

void HoldCast()
{
    int i;
    for (i = 0; i < HP; i++) CastAt(GetLocalObject(GetModule(), "hpc" + IntToString(i)), SPELL_HOLD_PERSON, GetLocalObject(GetModule(), "hpt" + IntToString(i)));
}

string Effects(object o)
{
    string s = "";
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        if (GetEffectSubType(e) != SUBTYPE_SUPERNATURAL)
            s += PFxShort(e) + "d" + IntToString(GetEffectDuration(e)) + " ";
        e = GetNextEffect(o);
    }
    return s == "" ? "-" : s;
}

void HoldReport()
{
    int i, nFree = 0, nHeldFree = 0, nCmd = 0;
    string sHeld = "";
    for (i = 0; i < HP; i++)
    {
        object o = GetLocalObject(GetModule(), "hpt" + IntToString(i));
        if (!GetLocalInt(o, "cast")) continue;
        int bHeld = HasType(o, EFFECT_TYPE_PARALYZE);
        if (bHeld && GetCommandable(o)) nCmd++;
        if (i < 4) { if (bHeld && sHeld == "") sHeld = Effects(o); continue; }
        nFree++;
        if (bHeld) nHeldFree++;
    }
    PLog("hold.effects", sHeld);
    PLogI("hold.commandable", nCmd);
    // Will +1 against DC 16 (13 + innate 3): held 70% of the time; 40 targets give a sd of 0.072 per engine.
    PLogF("hold.rate", IntToFloat(nHeldFree) / IntToFloat(nFree), 0.3);
}

// ---------------------------------------------------------------- Sleep

void SleepSetup()
{
    object oC = Caster(30.0, 51.0);
    SetLocalObject(GetModule(), "slc", oC);
    int i;
    for (i = 0; i < 8; i++)
    {
        float a = IntToFloat(i) * 45.0;
        object o = Make("nw_bandit001", 30.0 + cos(a) * 2.0, 60.0 + sin(a) * 2.0);
        Enemies(oC, o);
        SetLocalObject(GetModule(), "slt" + IntToString(i), o);
    }
}

void SleepCast()
{
    CastAtLocation(GetLocalObject(GetModule(), "slc"), SPELL_SLEEP, Location(GetObjectByTag("PARITY_AREA"), Vector(30.0, 60.0, 0.0), 0.0));
}

void SleepReport()
{
    int i, n = 0;
    for (i = 0; i < 8; i++) if (HasType(GetLocalObject(GetModule(), "slt" + IntToString(i)), EFFECT_TYPE_SLEEP)) n++;
    // Sleep takes 2d4 hit dice of creatures that fail a will save (DC 14 against +1: 65%).
    PLogF("sleep.asleep", IntToFloat(n), 3.0);
}

// ---------------------------------------------------------------- Dispel Magic

const int DM = 16;

void DispelSetup()
{
    int i;
    for (i = 0; i < DM; i++)
    {
        float y = 5.0 + IntToFloat(i) * 4.5;
        object c = Caster(70.0, y);
        SetLocalObject(GetModule(), "dmc" + IntToString(i), c);
        object o = Make("nw_bandit006", 72.0, y);
        Enemies(c, o);
        SetLocalObject(GetModule(), "dmt" + IntToString(i), o);
        AssignCommand(o, ActionCastSpellAtObject(SPELL_MAGE_ARMOR, o, METAMAGIC_NONE, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
        AssignCommand(o, ActionCastSpellAtObject(SPELL_BULLS_STRENGTH, o, METAMAGIC_NONE, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, SetEffectSpellId(EffectSkillIncrease(SKILL_SPOT, 1), SPELL_BLESS), o);
    }
}

void DispelCast()
{
    int i;
    for (i = 0; i < DM; i++) CastAt(GetLocalObject(GetModule(), "dmc" + IntToString(i)), SPELL_DISPEL_MAGIC, GetLocalObject(GetModule(), "dmt" + IntToString(i)));
}

void DispelReport()
{
    int i, nMA = 0, nBull = 0, nMod = 0;
    for (i = 0; i < DM; i++)
    {
        object o = GetLocalObject(GetModule(), "dmt" + IntToString(i));
        nMA += GetHasSpellEffect(SPELL_MAGE_ARMOR, o);
        nBull += GetHasSpellEffect(SPELL_BULLS_STRENGTH, o);
        nMod += GetHasSpellEffect(SPELL_BLESS, o);
    }
    // Caster level 10 against 10: d20 + 10 against 21, so each buff goes half the time.
    PLogF("dispel.magearmor.left", IntToFloat(nMA) / IntToFloat(DM), 0.4);
    PLogF("dispel.bull.left", IntToFloat(nBull) / IntToFloat(DM), 0.4);
    // An effect the module made with a spell id and no caster level.
    PLogF("dispel.module.left", IntToFloat(nMod) / IntToFloat(DM), 0.4);
}

void Run()
{
    MissileSetup();
    FireballSetup();
    HoldSetup();
    SleepSetup();
    DispelSetup();
    // Casting at a creature needs the caster to have seen it.
    DelayCommand(9.0, MissileCast());
    DelayCommand(9.0, HoldCast());
    DelayCommand(9.0, SleepCast());
    DelayCommand(9.0, DispelCast());
    DelayCommand(9.0, FireballCast());
    DelayCommand(14.0, FireballCast());
    DelayCommand(19.0, FireballCast());
    DelayCommand(15.0, MissileReport());
    DelayCommand(15.0, HoldReport());
    DelayCommand(15.0, SleepReport());
    DelayCommand(15.0, DispelReport());
    DelayCommand(24.0, FireballReport());
    DelayCommand(25.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}
