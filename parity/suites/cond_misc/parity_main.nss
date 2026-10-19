// More conditions on nwserver:
//   neg.*      negative levels on level 8 henchmen (cleric Linu, sorcerer Boddyknock, fighter Daelan): what one and two
//              levels take from hit dice, hit points, attack, saves, skills and spell uses, and what comes back
//   deaf.*     deaf casters (nw_bandit001) casting by cheat at an immortal target: verbal-and-somatic Ray of Frost, verbal
//              Blindness/Deafness and somatic Lesser Dispel, with a hearing control (not touch-range Light: six casters
//              crowding the target don't all reach it here). The target counts the spells that arrive
//   sanct.*    attackers told to attack a creature under sanctuary of DC 1, 100 and 12 at 1.5 s (before their first
//              perception update at about 4 s) and again at 7 s: how often they attack; sanctuary on a creature that attacks
//   swarm.*    EffectSwarm on a creature: which creature comes, and the next one each time it dies (read 1.5 s after
//              each death: the next one came within 0.2 s once and later than that once)
//   cutimm.*   a cutscene-immobilised attacker told to attack a target in reach and one out of reach
#include "parity_fx"
#include "pcond_inc"

int HasType(object o, int nType)
{
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e) == nType) return TRUE;
        e = GetNextEffect(o);
    }
    return FALSE;
}

// ---------------------------------------------------------------- negative levels

// Spell uses by level 0-4: a cleric's memorised spells that are ready, a sorcerer's uses left of its known spells.
string Uses(object o)
{
    string s = "";
    int lvl, i;
    int cls = GetLevelByClass(CLASS_TYPE_CLERIC, o) > 0 ? CLASS_TYPE_CLERIC : CLASS_TYPE_SORCERER;
    for (lvl = 0; lvl <= 4; lvl++)
    {
        int n = 0;
        if (cls == CLASS_TYPE_CLERIC)
        {
            int c = GetMemorizedSpellCountByLevel(o, cls, lvl);
            for (i = 0; i < c; i++) n += GetMemorizedSpellReady(o, cls, lvl, i) == 1;
        }
        else
        {
            int c = GetKnownSpellCount(o, cls, lvl);
            for (i = 0; i < c; i++) n += GetSpellUsesLeft(o, cls, GetKnownSpellId(o, cls, lvl, i));
        }
        s += (lvl > 0 ? "," : "") + IntToString(n);
    }
    return s;
}

void NegRead(object o, string k)
{
    PLog(k + ".diff", PFxDiff(o));
    PLog(k + ".uses", Uses(o));
    PLogI(k + ".hd", GetHitDice(o));
    PLogI(k + ".effects", PFxCount(o));
    PLogI(k + ".dead", GetIsDead(o));
}

void NegApply(object o, int n) { ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectNegativeLevel(n), o); }

void NegRemove(object o)
{
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e) == EFFECT_TYPE_NEGATIVELEVEL) RemoveEffect(o, e);
        e = GetNextEffect(o);
    }
}

void Neg(int i, string sResRef, string g)
{
    object o = CSpawn(4.0 + IntToFloat(i) * 3.0, 4.0, g, STANDARD_FACTION_COMMONER, FALSE, sResRef);
    DelayCommand(0.4, PFxSnap(o));
    DelayCommand(0.5, PLog(g + ".base.uses", Uses(o)));
    DelayCommand(0.5, PLogI(g + ".base.hd", GetHitDice(o)));
    // (Not GetMaxHitPoints itself: OpenWinter reads 54 and 69 for these two henchmen, the original 62 and 77.)
    DelayCommand(0.5, PLogI(g + ".base.bab", GetBaseAttackBonus(o)));
    DelayCommand(1.0, NegApply(o, 1));
    DelayCommand(1.5, NegRead(o, g + ".one"));
    DelayCommand(2.0, NegApply(o, 1));
    DelayCommand(2.5, NegRead(o, g + ".two"));
    DelayCommand(3.0, NegRemove(o));
    DelayCommand(3.5, NegRead(o, g + ".removed"));
    DelayCommand(4.0, NegApply(o, 3));
    DelayCommand(4.5, NegRead(o, g + ".three"));
}

// ---------------------------------------------------------------- deafness

void Caster(object t, int nSpell, int nLeft, int bCheat)
{
    int n;
    for (n = 0; n < nLeft; n++) ActionCastSpellAtObject(nSpell, t, METAMAGIC_ANY, bCheat);
}

// A caster only casts at a creature it has seen, and perception first updates 4 s after a creature appears.
void Deaf(int i, string g, int nSpell, int bDeaf, int nCasters, string sCaster = "nw_bandit001", int bCheat = TRUE)
{
    float y = 12.0 + IntToFloat(i) * 4.0;
    object t = CNeighbour(40.0, y, g, "T", STANDARD_FACTION_COMMONER);
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_SPELLCASTAT, "pc_cnspell");
    int k;
    for (k = 0; k < nCasters; k++)
    {
        object a = CSpawn(32.0 - IntToFloat(k) * 1.2, y, g, STANDARD_FACTION_HOSTILE, FALSE, sCaster);
        if (bDeaf) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDeaf(), a);
        DelayCommand(5.0, AssignCommand(a, Caster(t, nSpell, 5, bCheat)));
        if (k == 0) DelayCommand(6.0, PLogI(g + ".action", GetCurrentAction(a)));
    }
    DelayCommand(38.0, PLogF(g + ".rate", IntToFloat(GetLocalInt(GetModule(), g + ".cast." + IntToString(nSpell))) / IntToFloat(5 * nCasters), 0.25));
    DelayCommand(38.0, PLogI(g + ".anyFailed", GetLocalInt(GetModule(), g + ".cast." + IntToString(nSpell)) < 5 * nCasters));
}

// ---------------------------------------------------------------- sanctuary

void Sanct(int i, string g, int nDC, int nAttackers)
{
    float y = 40.0 + IntToFloat(i) * 5.0;
    object s = CNeighbour(40.0, y, g, "S", STANDARD_FACTION_COMMONER);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSanctuary(nDC), s);
    int k;
    for (k = 0; k < nAttackers; k++)
    {
        float a0 = IntToFloat(k) * 360.0 / IntToFloat(nAttackers);
        object a = CSpawn(40.0 + 1.6 * cos(a0), y + 1.6 * sin(a0), g, STANDARD_FACTION_HOSTILE);
        DelayCommand(1.5, AssignCommand(a, ActionAttack(s)));
        DelayCommand(7.0, AssignCommand(a, ActionAttack(s)));
        if (k == 0)
        {
            DelayCommand(3.0, PLogI(g + ".a0.action", GetCurrentAction(a)));
            DelayCommand(3.0, PLogI(g + ".a0.target", GetAttackTarget(a) == s));
        }
    }
    DelayCommand(13.0, PLogF(g + ".attacks", IntToFloat(GetLocalInt(GetModule(), g + ".atk.S")) / IntToFloat(nAttackers), 1.1));
    DelayCommand(13.0, PLogI(g + ".attackedAny", GetLocalInt(GetModule(), g + ".atk.S") > 0));
    DelayCommand(13.0, PLogI(g + ".still", HasType(s, EFFECT_TYPE_SANCTUARY)));
}

// A sanctuaried creature that attacks: does its sanctuary end?
void SanctAttacker()
{
    object s = CSpawn(60.0, 40.0, "sanct.attacker", STANDARD_FACTION_HOSTILE);
    object t = CNeighbour(61.4, 40.0, "sanct.attacker", "T", STANDARD_FACTION_COMMONER);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSanctuary(10), s);
    DelayCommand(1.5, AssignCommand(s, ActionAttack(t)));
    DelayCommand(1.4, PLogI("sanct.attacker.before", HasType(s, EFFECT_TYPE_SANCTUARY)));
    DelayCommand(6.0, PLogI("sanct.attacker.after", HasType(s, EFFECT_TYPE_SANCTUARY)));
    DelayCommand(6.0, PLogI("sanct.attacker.hit", GetLocalInt(GetModule(), "sanct.attacker.atk.T") > 0));
}

// ---------------------------------------------------------------- swarm

string Who(object o)
{
    if (!GetIsObjectValid(o)) return "-";
    return GetResRef(o);
}

void SwarmRead(object c, string k)
{
    object s = GetAssociate(ASSOCIATE_TYPE_SUMMONED, c);
    PLog(k + ".summon", Who(s));
    if (GetIsObjectValid(s)) PLogF(k + ".dist", GetDistanceBetween(s, c), 1.0);
    if (GetIsObjectValid(s)) PLogI(k + ".factionEqual", GetFactionEqual(s, c));
    PLog(k + ".effects", PFxTypes(c));
}

void SwarmKill(object c)
{
    object s = GetAssociate(ASSOCIATE_TYPE_SUMMONED, c);
    if (GetIsObjectValid(s)) ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), s);
}

void Swarm(int i, string g, int bLoop, float fDur)
{
    object c = CSpawn(10.0 + IntToFloat(i) * 12.0, 70.0, g, STANDARD_FACTION_DEFENDER);
    effect e = EffectSwarm(bLoop, "nw_kobold001", "nw_goblina", "nw_bandit001");
    if (fDur > 0.0) ApplyEffectToObject(DURATION_TYPE_TEMPORARY, e, c, fDur);
    else ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, c);
    DelayCommand(0.1, SwarmRead(c, g + ".t0_1"));
    DelayCommand(1.0, SwarmRead(c, g + ".t1"));
    int n;
    for (n = 0; n < 4; n++)
    {
        DelayCommand(2.0 + IntToFloat(n) * 2.0, SwarmKill(c));
        DelayCommand(3.5 + IntToFloat(n) * 2.0, SwarmRead(c, g + ".k" + IntToString(n) + "b"));
    }
    DelayCommand(12.0, SwarmRead(c, g + ".t12"));
    DelayCommand(16.0, SwarmRead(c, g + ".t16"));
}

// ---------------------------------------------------------------- cutscene immobilise

void CutImm(int i, string g, float fGap)
{
    float y = 60.0 + IntToFloat(i) * 4.0;
    object a = CSpawn(60.0, y, g, STANDARD_FACTION_HOSTILE);
    object t = CNeighbour(60.0 + fGap, y, g, "T", STANDARD_FACTION_COMMONER);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackIncrease(20), a);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneImmobilize(), a);
    DelayCommand(1.0, AssignCommand(a, ActionAttack(t)));
    DelayCommand(2.0, PLogI(g + ".action2", GetCurrentAction(a)));
    DelayCommand(6.0, PLogI(g + ".action6", GetCurrentAction(a)));
    DelayCommand(6.0, PLogI(g + ".target", GetAttackTarget(a) == t));
    DelayCommand(9.0, PLogI(g + ".hit", GetLocalInt(GetModule(), g + ".atk.T") > 0));
    DelayCommand(9.0, PLogF(g + ".moved", CDist0(a), 0.3));
    DelayCommand(9.0, PLogI(g + ".combat", GetIsInCombat(a)));
}

void main()
{
    Neg(0, "nw_hen_lin_05", "neg.cleric");
    Neg(1, "nw_hen_bod_05", "neg.sorcerer");
    Neg(2, "nw_hen_dae_05", "neg.fighter");
    Deaf(0, "deaf.ray", SPELL_RAY_OF_FROST, TRUE, 6);
    Deaf(1, "deaf.blind", SPELL_BLINDNESS_AND_DEAFNESS, TRUE, 6);
    Deaf(2, "deaf.dispel", SPELL_LESSER_DISPEL, TRUE, 6);
    Deaf(3, "hear.ray", SPELL_RAY_OF_FROST, FALSE, 3);
    Sanct(0, "sanct.dc1", 1, 3);
    Sanct(1, "sanct.dc100", 100, 3);
    Sanct(2, "sanct.dc12", 12, 6);
    SanctAttacker();
    Swarm(0, "swarm.once", FALSE, 0.0);
    Swarm(1, "swarm.loop", TRUE, 0.0);
    Swarm(2, "swarm.short", FALSE, 3.0);
    CutImm(0, "cutimm.near", 1.4);
    CutImm(1, "cutimm.far", 4.0);
    DelayCommand(39.0, PDone());
}
