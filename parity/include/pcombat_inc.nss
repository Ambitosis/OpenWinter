// Combat measurement: passive targets, scripted attackers in groups, a shared clock and statistics.
//
// A suite spawns attacker/target pairs with PPair, tags each attacker with a group, wires the event
// scripts (one-line wrappers around PcOnAttacked / PcOnDamaged / PcOnEndRound / PcOnTargetEndRound)
// and reports per group at the end. Counters live on the module as "<group>.<name>".
#include "parity_inc"

// ---------------------------------------------------------------- clock

// Module clock in twentieths of a second since PClockStart, driven by DelayCommand so both engines agree.
void PClockSet(int n)
{
    SetLocalInt(GetModule(), "PCLOCK", n);
}

void PClockStart(float fSeconds)
{
    int n = FloatToInt(fSeconds * 20.0);
    int i;
    SetLocalInt(GetModule(), "PCLOCK", 0);
    for (i = 1; i <= n; i++) DelayCommand(IntToFloat(i) / 20.0, PClockSet(i));
}

int PClock()
{
    return GetLocalInt(GetModule(), "PCLOCK");
}

// ---------------------------------------------------------------- counters

void PAdd(string sKey, int n = 1)
{
    SetLocalInt(GetModule(), sKey, GetLocalInt(GetModule(), sKey) + n);
}

int PGet(string sKey)
{
    return GetLocalInt(GetModule(), sKey);
}

// A rate with a tolerance of 3.5 standard deviations of the difference of two such samples
// (fFloor keeps very lopsided rates from getting a zero tolerance).
void PLogRate(string sKey, int nNum, int nDen, float fFloor = 0.02)
{
    if (nDen <= 0) { PLog(sKey, "none"); return; }
    float p = IntToFloat(nNum) / IntToFloat(nDen);
    float q = p < 0.02 ? 0.02 : (p > 0.98 ? 0.98 : p);
    float tol = 3.5 * sqrt(2.0 * q * (1.0 - q) / IntToFloat(nDen));
    if (tol < fFloor) tol = fFloor;
    PLogF(sKey, p, tol);
}

// A mean with a tolerance from its standard deviation (fSd, per sample) over n samples.
void PLogMean(string sKey, float fSum, int n, float fSd, float fFloor = 0.01)
{
    if (n <= 0) { PLog(sKey, "none"); return; }
    float tol = 3.5 * fSd * sqrt(2.0 / IntToFloat(n));
    if (tol < fFloor) tol = fFloor;
    PLogF(sKey, fSum / IntToFloat(n), tol);
}

// ---------------------------------------------------------------- creatures

// Removes every event script so the creature only does what the suite tells it to.
void PStrip(object o)
{
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    AssignCommand(o, ClearAllActions(TRUE));
    SetAILevel(o, AI_LEVEL_NORMAL);
}

// Spawns a creature at absolute area coordinates and strips its scripts.
object PSpawn(string sResRef, float fX, float fY, float fFacing = 0.0)
{
    object oArea = GetObjectByTag("PARITY_AREA");
    location l = Location(oArea, Vector(fX, fY, 0.0), fFacing);
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, l);
    PStrip(o);
    return o;
}

// Gives the creature a new item from a blueprint and equips it.
object PEquip(object o, string sResRef, int nSlot, int nStack = 1)
{
    object it = CreateItemOnObject(sResRef, o, nStack);
    SetIdentified(it, TRUE);
    AssignCommand(o, ActionEquipItem(it, nSlot));
    return it;
}

// Destroys the item in a slot.
void PUnequip(object o, int nSlot)
{
    object it = GetItemInSlot(nSlot, o);
    if (GetIsObjectValid(it)) DestroyObject(it);
}

// Pair grid: each pair gets a 10 m cell of a size-8 area, away from tile edges.
location PCell(int n, float fDx)
{
    object oArea = GetObjectByTag("PARITY_AREA");
    float x = IntToFloat(n % 8) * 10.0 + 4.0 + fDx;
    float y = IntToFloat(n / 8) * 10.0 + 5.0;
    return Location(oArea, Vector(x, y, 0.0), fDx > 0.0 ? 180.0 : 0.0);
}

// Makes two creatures enemies of each other whatever the module's faction table says. A generated module has no
// repute.fac unless suite.json sets "factions", and then nwserver makes every NPC faction (Hostile included) a
// friend of every other (faction_nofac); Hostile is hostile only to players, and parity runs have none. A
// permanent personal reputation (SetIsTemporaryEnemy without decay) holds on both engines either way.
void PEnemies(object a, object b)
{
    SetIsTemporaryEnemy(b, a);
    SetIsTemporaryEnemy(a, b);
}

// An attacker of group sGroup and a passive, immortal target fDist metres in front of it, wired to
// the suite's event scripts pc_atk, pc_dmg, pc_eor, pc_teor and pc_back. Returns the attacker; the
// target is its local object "T".
object PPair(string sGroup, string sAttacker, string sTarget, float fDist = 1.5)
{
    int n = PGet("PAIRS");
    PAdd("PAIRS");
    object a = CreateObject(OBJECT_TYPE_CREATURE, sAttacker, PCell(n, 0.0));
    object t = CreateObject(OBJECT_TYPE_CREATURE, sTarget, PCell(n, fDist));
    PStrip(a);
    PStrip(t);
    SetLocalString(a, "G", sGroup);
    SetLocalString(t, "G", sGroup);
    SetLocalObject(a, "T", t);
    SetLocalObject(t, "A", a);
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "pc_atk");
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_DAMAGED, "pc_dmg");
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_END_COMBATROUND, "pc_teor");
    SetEventScript(a, EVENT_SCRIPT_CREATURE_ON_END_COMBATROUND, "pc_eor");
    SetEventScript(a, EVENT_SCRIPT_CREATURE_ON_DAMAGED, "pc_back");
    ChangeToStandardFaction(a, STANDARD_FACTION_HOSTILE);
    ChangeToStandardFaction(t, STANDARD_FACTION_COMMONER);
    PEnemies(a, t);
    SetImmortal(t, TRUE);
    SetImmortal(a, TRUE);
    return a;
}

// nAttackers attackers of group sGroup around one passive, immortal target (a squad), wired like PPair.
// Returns the target; each attacker's local object "T" is the target.
object PSquad(string sGroup, string sAttacker, string sTarget, int nAttackers, float fRadius = 1.5)
{
    int n = PGet("PAIRS");
    PAdd("PAIRS");
    location lc = PCell(n, 3.0);
    object t = CreateObject(OBJECT_TYPE_CREATURE, sTarget, lc);
    PStrip(t);
    SetLocalString(t, "G", sGroup);
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "pc_atk");
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_DAMAGED, "pc_dmg");
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_END_COMBATROUND, "pc_teor");
    ChangeToStandardFaction(t, STANDARD_FACTION_COMMONER);
    SetImmortal(t, TRUE);
    vector vc = GetPositionFromLocation(lc);
    int i;
    for (i = 0; i < nAttackers; i++)
    {
        float ang = 360.0 * IntToFloat(i) / IntToFloat(nAttackers);
        vector v = Vector(vc.x + fRadius * cos(ang), vc.y + fRadius * sin(ang), 0.0);
        object a = CreateObject(OBJECT_TYPE_CREATURE, sAttacker, Location(GetArea(t), v, ang + 180.0));
        PStrip(a);
        SetLocalString(a, "G", sGroup);
        SetLocalObject(a, "T", t);
        SetEventScript(a, EVENT_SCRIPT_CREATURE_ON_END_COMBATROUND, "pc_eor");
        SetEventScript(a, EVENT_SCRIPT_CREATURE_ON_DAMAGED, "pc_back");
        ChangeToStandardFaction(a, STANDARD_FACTION_HOSTILE);
        PEnemies(a, t);
        SetImmortal(a, TRUE);
    }
    return t;
}

// Applies an effect permanently to every attacker of a squad (or one attacker).
void PSquadEffect(object t, effect e)
{
    object o = GetFirstObjectInArea(GetArea(t));
    while (GetIsObjectValid(o))
    {
        if (GetLocalObject(o, "T") == t) ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
        o = GetNextObjectInArea(GetArea(t));
    }
}

// SetBaseAttackBonus for every attacker of a squad.
void PSquadAttacks(object t, int n)
{
    object o = GetFirstObjectInArea(GetArea(t));
    while (GetIsObjectValid(o))
    {
        if (GetLocalObject(o, "T") == t) SetBaseAttackBonus(n, o);
        o = GetNextObjectInArea(GetArea(t));
    }
}

void PAttack(object a)
{
    SetLocalInt(a, "START", PClock());
    AssignCommand(a, ActionAttack(GetLocalObject(a, "T")));
}

// Starts every pair's attack after fDelay seconds.
void PStartAll(float fDelay)
{
    object oArea = GetObjectByTag("PARITY_AREA");
    object o = GetFirstObjectInArea(oArea);
    while (GetIsObjectValid(o))
    {
        if (GetIsObjectValid(GetLocalObject(o, "T"))) DelayCommand(fDelay, PAttack(o));
        o = GetNextObjectInArea(oArea);
    }
}

// ---------------------------------------------------------------- events

// Target OnPhysicalAttacked: counts attacks, and their time since the attacker's last round end.
void PcOnAttacked()
{
    object a = GetLastAttacker();
    string g = GetLocalString(a, "G");
    int idx = GetLocalInt(a, "IDX");
    SetLocalInt(a, "IDX", idx + 1);
    PAdd(g + ".atk");
    int last = GetLocalInt(a, "LASTEOR");
    if (last > 0)
    {
        PAdd(g + ".off" + IntToString(idx), PClock() - last);
        PAdd(g + ".offn" + IntToString(idx));
    }
    if (GetLocalInt(a, "FIRST") == 0)
    {
        SetLocalInt(a, "FIRST", 1);
        PAdd(g + ".first", PClock() - GetLocalInt(a, "START"));
        PAdd(g + ".firstn");
    }
}

// Target OnDamaged: counts hits and damage, and how weapon damage is reported by type.
void PcOnDamaged()
{
    object a = GetLastDamager();
    string g = GetLocalString(a, "G");
    int d = GetTotalDamageDealt();
    PAdd(g + ".hit");
    PAdd(g + ".dmg", d);
    PAdd(g + ".dsq", d * d);
    PAdd(g + ".d" + IntToString(d));
    if (d > PGet(g + ".dmax")) SetLocalInt(GetModule(), g + ".dmax", d);
    if (PGet(g + ".hit") == 1 || d < PGet(g + ".dmin")) SetLocalInt(GetModule(), g + ".dmin", d);
    int nCrit = PGet(g + ".critat");
    if (nCrit > 0 && d >= nCrit)
    {
        PAdd(g + ".crit");
        PAdd(g + ".critsum", d);
        PAdd(g + ".critsq", d * d);
    }
    if (GetDamageDealtByType(DAMAGE_TYPE_BASE_WEAPON) >= 0) PAdd(g + ".dbase");
    if (GetDamageDealtByType(DAMAGE_TYPE_SLASHING) >= 0 || GetDamageDealtByType(DAMAGE_TYPE_PIERCING) >= 0
        || GetDamageDealtByType(DAMAGE_TYPE_BLUDGEONING) >= 0) PAdd(g + ".dphys");
    int nFire = GetDamageDealtByType(DAMAGE_TYPE_FIRE);
    if (nFire >= 0)
    {
        PAdd(g + ".fire", nFire);
        PAdd(g + ".firen");
        PAdd(g + ".firesq", nFire * nFire);
        if (nFire > PGet(g + ".firemax")) SetLocalInt(GetModule(), g + ".firemax", nFire);
        if (PGet(g + ".firen") == 1 || nFire < PGet(g + ".firemin")) SetLocalInt(GetModule(), g + ".firemin", nFire);
    }
    // Damage values in a window (two weapons with separate ranges).
    int lo = PGet(g + ".winlo");
    int hi = PGet(g + ".winhi");
    if (hi > 0 && d >= lo && d <= hi)
    {
        PAdd(g + ".win");
        PAdd(g + ".winsum", d);
        if (d > PGet(g + ".winmax")) SetLocalInt(GetModule(), g + ".winmax", d);
        if (PGet(g + ".winmin") == 0 || d < PGet(g + ".winmin")) SetLocalInt(GetModule(), g + ".winmin", d);
    }
}

// Attacker OnEndCombatRound: round lengths and attacks per complete round.
void PcOnEndRound()
{
    object a = OBJECT_SELF;
    string g = GetLocalString(a, "G");
    int now = PClock();
    int last = GetLocalInt(a, "LASTEOR");
    if (last > 0)
    {
        PAdd(g + ".rlen", now - last);
        PAdd(g + ".rounds");
        PAdd(g + ".ratk", GetLocalInt(a, "IDX"));
    }
    SetLocalInt(a, "LASTEOR", now);
    SetLocalInt(a, "IDX", 0);
}

// Target OnEndCombatRound: the defender's own rounds.
void PcOnTargetEndRound()
{
    object t = OBJECT_SELF;
    string g = GetLocalString(t, "G");
    int now = PClock();
    int last = GetLocalInt(t, "LASTEOR");
    if (last > 0)
    {
        PAdd(g + ".trlen", now - last);
        PAdd(g + ".trounds");
    }
    SetLocalInt(t, "LASTEOR", now);
}

// Attacker OnDamaged: the target fought back.
void PcOnHitBack()
{
    PAdd(GetLocalString(OBJECT_SELF, "G") + ".back");
}

// ---------------------------------------------------------------- reports

// Attacks per complete round and the round length in seconds (nwserver's rounds run up to ~0.12 s long
// under load, hence the tolerance).
void PReportRounds(string g)
{
    PLogMean(g + ".apr", IntToFloat(PGet(g + ".ratk")), PGet(g + ".rounds"), 0.05, 0.05);
    PLogMean(g + ".round", IntToFloat(PGet(g + ".rlen")) / 20.0, PGet(g + ".rounds"), 0.05, 0.15);
}

void PReportHits(string g)
{
    PLogRate(g + ".hitrate", PGet(g + ".hit"), PGet(g + ".atk"));
}

// Standard deviation of a sample from its sum and sum of squares.
float PSd(int nSum, int nSq, int n)
{
    if (n < 2) return 1.0;
    float m = IntToFloat(nSum) / IntToFloat(n);
    float v = IntToFloat(nSq) / IntToFloat(n) - m * m;
    return v > 0.0 ? sqrt(v) : 0.0;
}

// Damage per hit: smallest, largest and mean.
void PReportDamage(string g)
{
    int n = PGet(g + ".hit");
    PLogI(g + ".min", PGet(g + ".dmin"));
    PLogI(g + ".max", PGet(g + ".dmax"));
    PLogMean(g + ".mean", IntToFloat(PGet(g + ".dmg")), n, PSd(PGet(g + ".dmg"), PGet(g + ".dsq"), n), 0.05);
}
