// What death keeps. Creatures carry one effect of each kind (tagged) in one subtype and duration, and die by
// EffectDeath or by damage; the tags still listed after death are read at once, 0.1 s and 1.5 s later.
// Then deaths caused from inside an effect: a poison whose Script_1 kills (Sassone Leaf's acid on a 1 hit point
// creature), a poison whose Script_2 kills (Pit Fiend Ichor, a minute later), and an EffectRunScript whose interval
// script kills its own creature.
#include "parity_fx"

object Spawn(int nCell)
{
    object o = PFxSpawn(nCell);
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    SetIsDestroyable(FALSE, FALSE, TRUE, o);
    return o;
}

string Tags(object o)
{
    string s = "";
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { s += GetEffectTag(e) + " "; e = GetNextEffect(o); }
    return s == "" ? "-" : s;
}

effect Make(int i)
{
    switch (i)
    {
        // (Poison and disease roll a save: they are measured below.)
        case 0: return EffectSkillIncrease(SKILL_LISTEN, 1);
        case 1: return EffectAbilityIncrease(ABILITY_WISDOM, 1);
        case 2: return EffectCurse(1, 1, 1, 1, 1, 1);
        case 3: return EffectAbilityIncrease(ABILITY_STRENGTH, 2);
        case 4: return EffectAbilityDecrease(ABILITY_DEXTERITY, 2);
        case 5: return EffectACIncrease(2);
        case 6: return EffectSkillIncrease(SKILL_SPOT, 3);
        case 7: return EffectHaste();
        case 8: return EffectSlow();
        case 9: return EffectRegenerate(1, 6.0);
        case 10: return EffectTemporaryHitpoints(5);
        case 11: return EffectConcealment(20);
        case 12: return EffectDamageReduction(5, DAMAGE_POWER_PLUS_ONE);
        case 13: return EffectDamageImmunityIncrease(DAMAGE_TYPE_FIRE, 50);
        case 14: return EffectSpellResistanceIncrease(10);
        case 15: return EffectInvisibility(INVISIBILITY_TYPE_NORMAL);
        case 16: return EffectSanctuary(10);
        case 17: return EffectTrueSeeing();
        case 18: return EffectBlindness();
        case 19: return EffectSilence();
        case 20: return EffectSleep();
        case 21: return EffectParalyze();
        case 22: return EffectStunned();
        case 23: return EffectEntangle();
        case 24: return EffectMovementSpeedDecrease(30);
        case 25: return EffectAttackIncrease(2);
        // (A negative level kills these level-1 bandits at once.)
        case 26: return EffectMissChance(20);
        case 27: return EffectImmunity(IMMUNITY_TYPE_TRAP);
        case 28: return EffectVisualEffect(VFX_DUR_GLOW_RED);
        case 29: return EffectRunScript("", "", "", 6.0);
        case 30: return EffectDamageShield(1, DAMAGE_BONUS_1, DAMAGE_TYPE_FIRE);
        case 31: return EffectSpellImmunity(SPELL_MAGIC_MISSILE);
        case 32: return EffectConfused();
        case 33: return EffectFrightened();
        case 34: return EffectDarkness();
        case 35: return EffectSkillDecrease(SKILL_HIDE, 3);
        case 36: return EffectDamageIncrease(2, DAMAGE_TYPE_MAGICAL);
        case 37: return EffectSavingThrowIncrease(SAVING_THROW_ALL, 2);
        case 38: return EffectCutsceneGhost();
        case 39: return EffectSeeInvisible();
    }
    return EffectVisualEffect(VFX_DUR_GLOW_BLUE);
}

// nSub: 0 magical temporary, 1 magical permanent, 2 supernatural, 3 extraordinary, 4 unyielding (permanent).
void Load(object o, int nSub)
{
    int i;
    for (i = 0; i < 40; i++)
    {
        effect e = Make(i);
        if (nSub == 2) e = SupernaturalEffect(e);
        else if (nSub == 3) e = ExtraordinaryEffect(e);
        else if (nSub == 4) e = UnyieldingEffect(e);
        e = TagEffect(e, IntToString(i));
        if (nSub == 0) ApplyEffectToObject(DURATION_TYPE_TEMPORARY, e, o, 120.0);
        else ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
    }
}

void Kill(object o, int nHow)
{
    if (nHow == 0) ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), o);
    else ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(500, DAMAGE_TYPE_MAGICAL), o);
}

void KillRead(string k, object o, int nHow)
{
    PLog(k + ".before", Tags(o));
    Kill(o, nHow);
    PLog(k + ".now", Tags(o));
    DelayCommand(0.1, PLog(k + ".t01", Tags(o)));
    DelayCommand(1.5, PLog(k + ".t15", Tags(o)));
    DelayCommand(1.5, PLogI(k + ".dead", GetIsDead(o)));
}

void Matrix()
{
    int nSub, nHow;
    for (nSub = 0; nSub < 5; nSub++)
        for (nHow = 0; nHow < 2; nHow++)
        {
            object o = Spawn(nSub * 2 + nHow);
            // Saves made impossible but for a natural 20 (poison and disease would be shrugged off at random).
            ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(SupernaturalEffect(EffectSavingThrowDecrease(SAVING_THROW_ALL, 50)), "nosave"), o);
            Load(o, nSub);
            DelayCommand(1.0, KillRead("m" + IntToString(nSub) + "." + (nHow == 0 ? "death" : "damage"), o, nHow));
        }
}

// Unyielding conditions (death strips some of them even then), one creature per way of dying.
effect State(int i)
{
    switch (i)
    {
        case 0: return EffectDeaf();
        case 1: return EffectKnockdown();
        case 2: return EffectDazed();
        case 3: return EffectFrightened();
        case 4: return EffectCharmed();
        case 5: return EffectDominated();
        case 6: return EffectTurned();
        case 7: return EffectCutsceneParalyze();
        case 8: return EffectCutsceneImmobilize();
        case 9: return EffectEthereal();
        case 10: return EffectInvisibility(INVISIBILITY_TYPE_IMPROVED);
        case 11: return EffectBlindness();
        case 12: return EffectDarkness();
        case 13: return EffectCutsceneDominated();
        case 14: return EffectSlow();
        case 15: return EffectPetrify();
    }
    return EffectVisualEffect(VFX_DUR_GLOW_BLUE);
}

void States()
{
    int nHow, i;
    for (nHow = 0; nHow < 2; nHow++)
    {
        object o = Spawn(50 + nHow);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(SupernaturalEffect(EffectSavingThrowDecrease(SAVING_THROW_ALL, 50)), "nosave"), o);
        for (i = 0; i < 16; i++) ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(UnyieldingEffect(State(i)), "s" + IntToString(i)), o);
        DelayCommand(1.0, KillRead("states." + (nHow == 0 ? "death" : "damage"), o, nHow));
    }
}

int HasTag(object o, string sTag)
{
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { if (GetEffectTag(e) == sTag) return TRUE; e = GetNextEffect(o); }
    return FALSE;
}

// Saves made impossible but for a natural 20.
object Victim(int nCell, int nHp)
{
    object o = Spawn(nCell);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(SupernaturalEffect(EffectSavingThrowDecrease(SAVING_THROW_ALL, 50)), "nosave"), o);
    if (nHp > 0) SetCurrentHitPoints(o, nHp);
    return o;
}

object V(string g, int i) { return GetLocalObject(GetModule(), g + IntToString(i)); }

// Over a group of four: how many died, whether all the dead keep sTag, and whether any dead kept "nosave".
void Count(string k, string g, string sTag)
{
    int dead = 0, kept = 0, other = 0, i;
    for (i = 0; i < 4; i++)
    {
        object o = V(g, i);
        if (!GetIsDead(o)) continue;
        dead++;
        kept += HasTag(o, sTag);
        other += HasTag(o, "nosave");
    }
    PLogF(k + ".dead", IntToFloat(dead), 1.0);
    PLogI(k + ".deadKeep", dead > 0 && kept == dead);
    PLogI(k + ".deadKeepNosave", other > 0);
}

// Over a group of four with a poison and a disease, killed by EffectDeath: whether the dead that had each keep it.
void Afflicted(string k, string g)
{
    int had = 0, kept = 0, hadD = 0, keptD = 0, i;
    for (i = 0; i < 4; i++)
    {
        object o = V(g, i);
        int p = GetLocalInt(o, "hadPoison"), d = GetLocalInt(o, "hadDisease");
        had += p; hadD += d;
        kept += p && HasTag(o, "poison");
        keptD += d && HasTag(o, "disease");
    }
    PLogI(k + ".poisonKept", had > 0 && kept == had);
    PLogI(k + ".diseaseKept", hadD > 0 && keptD == hadD);
}

void Mark(object o)
{
    SetLocalInt(o, "hadPoison", HasTag(o, "poison"));
    SetLocalInt(o, "hadDisease", HasTag(o, "disease"));
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), o);
}

void PoisonScripts()
{
    int i;
    object m = GetModule();
    for (i = 0; i < 4; i++)
    {
        // Sassone Leaf: Script_1 deals 2d12 acid to a 1 hit point creature as the poison lands.
        object o = Victim(20 + i, 1);
        SetLocalObject(m, "sas" + IntToString(i), o);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectPoison(16), "poison"), o);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectSkillIncrease(SKILL_SPOT, 1), "spot"), o);
        // Pit Fiend Ichor: Script_2 kills a minute later.
        object p = Victim(30 + i, 0);
        SetLocalObject(m, "ich" + IntToString(i), p);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectPoison(31), "poison"), p);
        // Unyielding poison and disease, then EffectDeath.
        object q = Victim(40 + i, 0);
        SetLocalObject(m, "unp" + IntToString(i), q);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(UnyieldingEffect(EffectPoison(POISON_NIGHTSHADE)), "poison"), q);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(UnyieldingEffect(EffectDisease(DISEASE_FILTH_FEVER)), "disease"), q);
        DelayCommand(2.0, Mark(q));
        // A magical poison and disease, then EffectDeath.
        object r = Victim(44 + i, 0);
        SetLocalObject(m, "mgp" + IntToString(i), r);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectPoison(POISON_NIGHTSHADE), "poison"), r);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectDisease(DISEASE_FILTH_FEVER), "disease"), r);
        DelayCommand(2.0, Mark(r));
    }
    DelayCommand(0.1, Count("sassone.t01", "sas", "poison"));
    DelayCommand(1.5, Count("sassone.t15", "sas", "poison"));
    DelayCommand(1.5, Count("sassone.spot", "sas", "spot"));
    DelayCommand(70.0, Count("ichor.t70", "ich", "poison"));
    DelayCommand(2.5, Afflicted("unyielding", "unp"));
    DelayCommand(2.5, Afflicted("magical", "mgp"));
    // Death from an EffectRunScript interval script run by the creature itself.
    object r = Spawn(52);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectRunScript("", "", "dk_kill", 1.0), "runscript"), r);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectSkillIncrease(SKILL_SPOT, 1), "spot"), r);
    DelayCommand(0.5, PLog("runscript.before", Tags(r)));
    DelayCommand(3.0, PLog("runscript.after", Tags(r)));
    DelayCommand(3.0, PLogI("runscript.dead", GetIsDead(r)));
    // Death from an EffectRunScript OnApplied script.
    object ra = Spawn(53);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectSkillIncrease(SKILL_SPOT, 1), "spot"), ra);
    DelayCommand(1.0, ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectRunScript("dk_kill", "", "", 0.0), "runapplied"), ra));
    DelayCommand(1.1, PLog("runapplied.after", Tags(ra)));
    DelayCommand(1.1, PLogI("runapplied.dead", GetIsDead(ra)));
    DelayCommand(2.5, PLog("runapplied.later", Tags(ra)));
    // Death from the OnApplied script of an effect linked with another.
    object rl = Spawn(54);
    DelayCommand(1.0, ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectLinkEffects(EffectSkillIncrease(SKILL_HIDE, 1),
        EffectRunScript("dk_kill", "", "", 0.0)), "linked"), rl));
    DelayCommand(1.5, PLog("runlinked.after", PFxTypes(rl)));
    // Death by a command the creature runs itself (AssignCommand), not from an effect.
    object s = Spawn(55);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectSkillIncrease(SKILL_SPOT, 1), "spot"), s);
    DelayCommand(2.0, AssignCommand(s, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), s)));
    DelayCommand(2.5, PLog("selfDeath.after", Tags(s)));
}

void main()
{
    Matrix();
    States();
    DelayCommand(0.2, PoisonScripts());
    DelayCommand(78.0, PDone());
}
