// What each disabling condition stops a creature doing, on nwserver. For every condition a row of
// stripped nw_bandit001 actors (no event scripts, AI normal), each with the effect applied permanently by
// a hostile creature ("M") at 1.0 s:
//   move     told to walk 8 m east at 1.5 s; distance covered and current action at 4.5 s
//   premove  told to walk at 1.0 s, the effect lands 0.3 s later; distance at 4.5 s
//   attack   +20 to hit, told to attack a passive immortal commoner at 1.5 s; hits counted to 9 s
//   wake     1 point of magical damage at 2.0 s; whether the condition is still on at 2.5 s
//   queued   the effect lasts 2 s only; told to walk at 1.5 s, while it is on; distance at 7.5 s
// Commandable is read at 1.5 s. "none" is the control row. Actors are mortal (immortal and plot creatures
// shrug most of these off, see Block); targets heal what they take.
// Then helpless targets under NPC attack: hits and damage on stunned, paralysed and awake targets (asleep ones
// wake at the first hit).
#include "parity_inc"

const int COUNT = 19;

string Name(int i)
{
    switch (i)
    {
        case 0: return "sleep"; case 1: return "stun"; case 2: return "paralyze"; case 3: return "daze";
        case 4: return "confuse"; case 5: return "fear"; case 6: return "charm"; case 7: return "dominate";
        case 8: return "knockdown"; case 9: return "entangle"; case 10: return "petrify"; case 11: return "cutparalyze";
        case 12: return "turned"; case 13: return "blind"; case 14: return "slow"; case 15: return "deaf";
        case 16: return "silence"; case 17: return "cutimmobile";
    }
    return "none";
}

effect Make(int i)
{
    switch (i)
    {
        case 0: return EffectSleep(); case 1: return EffectStunned(); case 2: return EffectParalyze();
        case 3: return EffectDazed(); case 4: return EffectConfused(); case 5: return EffectFrightened();
        case 6: return EffectCharmed(); case 7: return EffectDominated(); case 8: return EffectKnockdown();
        case 9: return EffectEntangle(); case 10: return EffectPetrify(); case 11: return EffectCutsceneParalyze();
        case 12: return EffectTurned(); case 13: return EffectBlindness(); case 14: return EffectSlow();
        case 15: return EffectDeaf(); case 16: return EffectSilence(); case 17: return EffectCutsceneImmobilize();
    }
    return EffectVisualEffect(VFX_DUR_GLOW_WHITE);
}

object M() { return GetObjectByTag("CN_M"); }

void ApplyAs(int i, object o, float fDur)
{
    if (fDur > 0.0) ApplyEffectToObject(DURATION_TYPE_TEMPORARY, Make(i), o, fDur);
    else ApplyEffectToObject(DURATION_TYPE_PERMANENT, Make(i), o);
}

void Afflict(int i, object o, float fDur = 0.0)
{
    AssignCommand(M(), ApplyAs(i, o, fDur));
}

object Spawn(float x, float y, string g, int nFaction, string sResRef = "nw_bandit001")
{
    object oArea = GetObjectByTag("PARITY_AREA");
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(oArea, Vector(x, y, 0.0), 0.0));
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    SetAILevel(o, AI_LEVEL_NORMAL);
    ChangeToStandardFaction(o, nFaction);
    SetLocalString(o, "G", g);
    SetLocalFloat(o, "X0", x);
    return o;
}

object Target(float x, float y, string g)
{
    object t = Spawn(x, y, g, STANDARD_FACTION_COMMONER);
    SetPlotFlag(t, FALSE);
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_DAMAGED, "cn_dmg");
    return t;
}

void Walk(object o)
{
    vector v = GetPosition(o);
    AssignCommand(o, ActionMoveToLocation(Location(GetArea(o), v + Vector(8.0, 0.0, 0.0), 0.0), FALSE));
}

void LogMove(object o, string k)
{
    PLogF(k + ".dx", GetPosition(o).x - GetLocalFloat(o, "X0"), 1.4);
    PLogI(k + ".action", GetCurrentAction(o));
}

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

int TypeOf(int i)
{
    effect e = Make(i);
    return GetEffectType(e);
}

void Hits(string g)
{
    PLogI(g + ".hits", GetLocalInt(GetModule(), g + ".hits") > 0);
}

void Row(int i)
{
    string n = Name(i);
    float y = 3.0 + IntToFloat(i) * 3.8;
    object mv = Spawn(4.0, y, n + ".move", STANDARD_FACTION_HOSTILE);
    object pm = Spawn(14.0, y, n + ".premove", STANDARD_FACTION_HOSTILE);
    object at = Spawn(24.0, y, n + ".attack", STANDARD_FACTION_HOSTILE);
    object atT = Target(25.4, y, n + ".attack");
    object wk = Spawn(34.0, y, n + ".wake", STANDARD_FACTION_HOSTILE);
    object qu = Spawn(44.0, y, n + ".queued", STANDARD_FACTION_HOSTILE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackIncrease(20), at);

    DelayCommand(1.0, Walk(pm));
    DelayCommand(1.0, Afflict(i, mv));
    DelayCommand(1.3, Afflict(i, pm));
    DelayCommand(1.0, Afflict(i, at));
    DelayCommand(1.0, Afflict(i, wk));
    DelayCommand(1.0, Afflict(i, qu, 2.0));

    DelayCommand(1.5, PLogI(n + ".commandable", GetCommandable(mv)));
    DelayCommand(1.5, Walk(mv));
    DelayCommand(1.5, Walk(qu));
    DelayCommand(1.5, AssignCommand(at, ActionAttack(atT)));
    DelayCommand(2.0, AssignCommand(M(), ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(1, DAMAGE_TYPE_MAGICAL), wk)));
    if (i < 18) DelayCommand(1.6, PLogI(n + ".wake.before", HasType(wk, TypeOf(i))));
    if (i < 18) DelayCommand(2.5, PLogI(n + ".wake.after", HasType(wk, TypeOf(i))));
    // A confused creature attacks whatever is near at random, and a blind one misses half its few blows: those
    // readings vary from run to run.
    // Whether the attack order survives (frightened and turned creatures stand or flee by themselves, depending on
    // who is hostile in the module: see cond_fear).
    if (i != 4) DelayCommand(3.0, PLogI(n + ".attack.action", GetCurrentAction(at) == ACTION_ATTACKOBJECT ? 3 : ACTION_INVALID));
    DelayCommand(4.5, LogMove(mv, n + ".move"));
    // A confused creature walks at random or attacks by itself (cond_confuse).
    if (i != 4) DelayCommand(4.5, LogMove(pm, n + ".premove"));
    // A confused creature may be random-walking by then (ACTION_RANDOMWALK in one native run).
    if (i != 4) DelayCommand(7.5, LogMove(qu, n + ".queued"));
    // A charmed creature attacks whoever it regards as an enemy by itself (cond_charm).
    if (i != 4 && i != 6 && i != 13) DelayCommand(9.0, Hits(n + ".attack"));
}

void PAddDead(string g, object t)
{
    object m = GetModule();
    SetLocalInt(m, g + ".dead", GetLocalInt(m, g + ".dead") + GetIsDead(t));
}

// NPC attackers on helpless targets: an automatic hit, a coup de grace? Four attackers per condition (one
// row each), each against its own target; the archers stand 6 m off with a shortbow.
void Helpless(int i, int k, int nRow, int bRanged = FALSE, int nImmunity = -1)
{
    string g = "helpless." + Name(i) + (bRanged ? ".ranged" : "") + (nImmunity == IMMUNITY_TYPE_CRITICAL_HIT ? ".critimmune" : "")
        + (nImmunity == IMMUNITY_TYPE_DEATH ? ".deathimmune" : "");
    float x = 62.0 + IntToFloat(k) * 4.0;
    float y = 3.0 + IntToFloat(nRow) * 5.0;
    object a = Spawn(x, y + (bRanged ? 2.5 : 0.0), g, STANDARD_FACTION_HOSTILE);
    object t = Target(x + (bRanged ? 0.0 : 1.4), y + (bRanged ? -2.5 : 0.0), g);
    if (bRanged)
    {
        object bow = CreateItemOnObject("nw_wbwsh001", a);
        object arrows = CreateItemOnObject("nw_wamar001", a, 99);
        AssignCommand(a, ActionEquipItem(bow, INVENTORY_SLOT_RIGHTHAND));
        // nwserver equips the arrows by itself; OpenWinter doesn't yet, so equip them explicitly.
        AssignCommand(a, ActionEquipItem(arrows, INVENTORY_SLOT_ARROWS));
    }
    // About half of the blows hit an able target: automatic hits would stand out.
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(6, AC_DEFLECTION_BONUS), t);
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "cn_atk");
    if (nImmunity >= 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectImmunity(nImmunity), t);
    if (i < 18) Afflict(i, t);
    DelayCommand(1.5, AssignCommand(a, ActionAttack(t)));
    if (k == 0) DelayCommand(4.0, PLogI(g + ".killed4s", GetCurrentHitPoints(t) == -11));
    DelayCommand(3.5, PAddDead(g, t));
}

// Which effects an immortal or a plot creature refuses (and a normal one, the control). Not time stop: it
// would freeze the whole suite.
effect MakeAny(int i)
{
    if (i < 18) return Make(i);
    switch (i)
    {
        case 18: return EffectPoison(POISON_NIGHTSHADE); case 19: return EffectDisease(DISEASE_BLINDING_SICKNESS);
        case 20: return EffectCurse(); case 21: return EffectNegativeLevel(1); case 22: return EffectAbilityDecrease(ABILITY_STRENGTH, 2);
        case 23: return EffectACDecrease(2); case 24: return EffectAttackDecrease(2); case 25: return EffectSavingThrowDecrease(SAVING_THROW_ALL, 2);
        case 26: return EffectSkillDecrease(SKILL_ALL_SKILLS, 2); case 27: return EffectMovementSpeedDecrease(50); case 28: return EffectHaste();
        case 29: return EffectInvisibility(INVISIBILITY_TYPE_NORMAL); case 30: return EffectSanctuary(10); case 31: return EffectDarkness();
        case 32: return EffectRegenerate(1, 6.0); case 33: return EffectDamageImmunityDecrease(DAMAGE_TYPE_FIRE, 10); case 34: return EffectSpellResistanceDecrease(5);
        case 35: return EffectDamageDecrease(2); case 36: return EffectSwarm(FALSE, "nw_bandit001"); case 37: return EffectEthereal();
        case 38: return IgnoreEffectImmunity(EffectSleep()); case 39: return IgnoreEffectImmunity(EffectSlow());
        case 40: return EffectCutsceneDominated(); case 41: return EffectPoison(POISON_NIGHTSHADE);
    }
    return EffectVisualEffect(VFX_DUR_GLOW_WHITE);
}

string NameAny(int i)
{
    if (i < 18) return Name(i);
    switch (i)
    {
        case 18: return "poison"; case 19: return "disease"; case 20: return "curse"; case 21: return "neglevel"; case 22: return "abilitydec";
        case 23: return "acdec"; case 24: return "attackdec"; case 25: return "savedec"; case 26: return "skilldec"; case 27: return "speeddec";
        case 28: return "haste"; case 29: return "invis"; case 30: return "sanctuary"; case 31: return "darkness"; case 32: return "regenerate";
        case 33: return "dmgimmdec"; case 34: return "srdec"; case 35: return "dmgdec"; case 36: return "swarm"; case 37: return "ethereal";
        case 38: return "sleep.ignoreimmunity"; case 39: return "slow.ignoreimmunity"; case 40: return "cutdominate";
    }
    return "?";
}

void ApplyAny(int i, object o) { ApplyEffectToObject(DURATION_TYPE_PERMANENT, MakeAny(i), o); }

int CountEffects(object o)
{
    int n = 0;
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { n++; e = GetNextEffect(o); }
    return n;
}

void Block(int i)
{
    object oArea = GetObjectByTag("PARITY_AREA");
    float x = 4.0 + IntToFloat(i) * 1.8;
    object a = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(oArea, Vector(x, 78.5, 0.0), 0.0));
    object b = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(oArea, Vector(x, 76.5, 0.0), 0.0));
    object c = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(oArea, Vector(x, 74.5, 0.0), 0.0));
    int ev;
    for (ev = 5000; ev <= 5012; ev++) { SetEventScript(a, ev, ""); SetEventScript(b, ev, ""); SetEventScript(c, ev, ""); }
    SetImmortal(a, TRUE);
    SetPlotFlag(b, TRUE);
    AssignCommand(M(), ApplyAny(i, a));
    AssignCommand(M(), ApplyAny(i, b));
    AssignCommand(M(), ApplyAny(i, c));
    string n = NameAny(i);
    // Poison and disease are saved against at random here (see Afflictions).
    if (i == 18 || i == 19) return;
    DelayCommand(0.5, PLogI("block." + n + ".immortal", CountEffects(a)));
    DelayCommand(0.5, PLogI("block." + n + ".plot", CountEffects(b)));
    DelayCommand(0.5, PLogI("block." + n + ".normal", CountEffects(c)));
}

// Poison allows a saving throw (disease, even at -50, landed on an immortal target in one native run of two): the targets get -50 on saves first (and plot or immortality after).
void ApplySure(int bDisease, object o)
{
    effect e = bDisease ? EffectDisease(DISEASE_BLINDING_SICKNESS) : EffectPoison(POISON_BLACK_LOTUS_EXTRACT);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

void Afflictions(int bDisease)
{
    object oArea = GetObjectByTag("PARITY_AREA");
    float x = 70.0 + IntToFloat(bDisease) * 4.0;
    object a = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(oArea, Vector(x, 72.0, 0.0), 0.0));
    object b = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(oArea, Vector(x, 70.0, 0.0), 0.0));
    object c = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(oArea, Vector(x, 68.0, 0.0), 0.0));
    effect saves = EffectSavingThrowDecrease(SAVING_THROW_ALL, 50);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, saves, a);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, saves, b);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, saves, c);
    SetImmortal(a, TRUE);
    SetPlotFlag(b, TRUE);
    DelayCommand(0.2, AssignCommand(M(), ApplySure(bDisease, a)));
    DelayCommand(0.2, AssignCommand(M(), ApplySure(bDisease, b)));
    DelayCommand(0.2, AssignCommand(M(), ApplySure(bDisease, c)));
    string n = bDisease ? "disease" : "poison";
    // One effect is the save penalty.
    DelayCommand(0.7, PLogI("block.sure." + n + ".immortal", CountEffects(a) - 1));
    DelayCommand(0.7, PLogI("block.sure." + n + ".plot", CountEffects(b) - 1));
    DelayCommand(0.7, PLogI("block.sure." + n + ".normal", CountEffects(c) - 1));
}

void Report(string g)
{
    object m = GetModule();
    int h = GetLocalInt(m, g + ".hits");
    // Cutscene-paralysed targets sometimes die of ordinary blows (2 of 4 in one native run): only "most".
    PLogI(g + ".mostlyDead", GetLocalInt(m, g + ".dead") >= 3);
    PLogI(g + ".hitsAny", h > 0);
    PLogF(g + ".attacks", IntToFloat(GetLocalInt(m, g + ".atk")), 10.0);
    PLogF(g + ".hits", IntToFloat(h), 12.0);
    if (h > 0) PLogF(g + ".perHit", IntToFloat(GetLocalInt(m, g + ".dmg")) / IntToFloat(h), 5.0);
}

// SetCommandable(FALSE) refuses most actions, but not ActionMoveToLocation.
void Uncommandable()
{
    object o = Spawn(62.0, 68.0, "uncommandable", STANDARD_FACTION_HOSTILE);
    SetCommandable(FALSE, o);
    DelayCommand(0.5, Walk(o));
    DelayCommand(3.5, LogMove(o, "uncommandable.walk"));
}

void main()
{
    object oArea = GetObjectByTag("PARITY_AREA");
    CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(oArea, Vector(78.0, 78.0, 0.0), 0.0), FALSE, "CN_M");
    int i;
    for (i = 0; i < COUNT; i++) DelayCommand(0.5 + IntToFloat(i) * 0.01, Row(i));
    int k;
    for (k = 0; k < 4; k++)
    {
        DelayCommand(0.6, Helpless(0, k, 0));
        DelayCommand(0.6, Helpless(1, k, 1));
        DelayCommand(0.6, Helpless(2, k, 2));
        DelayCommand(0.6, Helpless(3, k, 3));
        DelayCommand(0.6, Helpless(8, k, 4));
        DelayCommand(0.6, Helpless(9, k, 5));
        DelayCommand(0.6, Helpless(10, k, 6));
        DelayCommand(0.6, Helpless(11, k, 7));
        DelayCommand(0.6, Helpless(18, k, 8));
        DelayCommand(0.6, Helpless(2, k, 9, TRUE));
        DelayCommand(0.6, Helpless(18, k, 10, TRUE));
        DelayCommand(0.6, Helpless(2, k, 11, FALSE, IMMUNITY_TYPE_CRITICAL_HIT));
        DelayCommand(0.6, Helpless(2, k, 12, FALSE, IMMUNITY_TYPE_DEATH));
    }
    for (i = 0; i < 41; i++) DelayCommand(12.0 + IntToFloat(i) * 0.02, Block(i));
    DelayCommand(25.0, Uncommandable());
    DelayCommand(26.0, Afflictions(FALSE));

    DelayCommand(30.0, Report("helpless.sleep"));
    DelayCommand(30.0, Report("helpless.stun"));
    DelayCommand(30.0, Report("helpless.paralyze"));
    DelayCommand(30.0, Report("helpless.daze"));
    DelayCommand(30.0, Report("helpless.knockdown"));
    DelayCommand(30.0, Report("helpless.entangle"));
    DelayCommand(30.0, Report("helpless.petrify"));
    DelayCommand(30.0, Report("helpless.cutparalyze"));
    DelayCommand(30.0, Report("helpless.none"));
    DelayCommand(30.0, Report("helpless.paralyze.ranged"));
    DelayCommand(30.0, Report("helpless.none.ranged"));
    DelayCommand(30.0, Report("helpless.paralyze.critimmune"));
    DelayCommand(30.0, Report("helpless.paralyze.deathimmune"));
    DelayCommand(30.5, PDone());
}
