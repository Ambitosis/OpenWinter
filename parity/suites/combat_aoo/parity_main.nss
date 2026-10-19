// Attacks of opportunity, as nwserver gives them: an enemy beside a guard can walk away, shoot a bow or
// cast a spell without drawing one, whether the guard fights someone else, idles, or attacks it (then
// it only takes the guard's normal attacks). Prelude's faction table (suite.json) makes guard and runner enemies
// (gated by enemy.*); without one they were friends on nwserver, with the same readings. Not covered: an archer the guard attacks steps back from it
// each round on nwserver and draws attacks doing so; OpenWinter's archers don't step back yet.
#include "pcombat_inc"

const string FIGHTER = "nw_drowfight010";
const string TARGET = "nw_bandit001";

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

// A guard attacking a passive target, and a runner of group g standing 1.2 m behind the guard.
// bFighting: 0 guard idle, 1 guard attacks its target, 2 guard attacks the runner.
object Runner(string g, int bFighting)
{
    object a = PPair("guard." + g, FIGHTER, TARGET);
    SetBaseAttackBonus(1, a);
    Perm(a, EffectAttackIncrease(20));
    vector v = GetPosition(a);
    object r = CreateObject(OBJECT_TYPE_CREATURE, TARGET, Location(GetArea(a), Vector(v.x - 1.2, v.y, 0.0), 0.0));
    PStrip(r);
    SetImmortal(r, TRUE);
    SetLocalString(r, "R", g);
    SetLocalObject(r, "G", a);
    SetEventScript(r, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "pc_ratk");
    ChangeToStandardFaction(r, STANDARD_FACTION_DEFENDER);
    if (g == "walk") SetLocalObject(GetModule(), "R1", r);
    if (bFighting == 0) SetLocalObject(a, "T", OBJECT_INVALID);
    if (bFighting == 2) SetLocalObject(a, "T", r);
    return r;
}

void Walk(object r)
{
    vector v = GetPosition(r);
    AssignCommand(r, ActionMoveToLocation(Location(GetArea(r), Vector(v.x - 3.0, v.y + 2.5, 0.0), 0.0), TRUE));
}

void Shoot(object r)
{
    AssignCommand(r, ActionAttack(GetLocalObject(r, "G")));
}

void Cast(object r)
{
    AssignCommand(r, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, GetLocalObject(r, "G"), METAMAGIC_ANY, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
}

void Setup()
{
    int i;
    for (i = 0; i < 4; i++)
    {
        object r = Runner("walk", TRUE);
        DelayCommand(6.0, Walk(r));
        r = Runner("walk.idle", FALSE);
        DelayCommand(6.0, Walk(r));
        r = Runner("shoot", TRUE);
        PEquip(r, "nw_wbwsh001", INVENTORY_SLOT_RIGHTHAND);
        PEquip(r, "nw_wamar001", INVENTORY_SLOT_ARROWS, 99);
        DelayCommand(6.0, Shoot(r));
        r = Runner("cast", TRUE);
        DelayCommand(6.0, Cast(r));
        r = Runner("stand", TRUE);
        r = Runner("t.walk", 2);
        DelayCommand(6.0, Walk(r));
        r = Runner("t.cast", 2);
        DelayCommand(6.0, Cast(r));
        r = Runner("t.stand", 2);
    }
    PStartAll(1.0);
}

void Report()
{
    // Guard and runner must be enemies (Prelude's faction table: Hostile against Defender).
    object r = GetLocalObject(GetModule(), "R1");
    PLogI("enemy.guard.runner", GetIsEnemy(r, GetLocalObject(r, "G")));
    PLogI("enemy.runner.guard", GetIsEnemy(GetLocalObject(r, "G"), r));
    string s = "walk walk.idle shoot cast stand ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        PLogI(g + ".hits.on.runner", PGet(g + ".hits.on.runner"));
        s = GetSubString(s, sp + 1, 1000);
    }
    // Runners the guard attacks: only its normal attacks, about one a round each (a few each).
    s = "t.walk t.cast t.stand ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        PLogMean(g + ".hits.on.runner", IntToFloat(PGet(g + ".hits.on.runner")), 4, 0.8, 0.5);
        s = GetSubString(s, sp + 1, 1000);
    }
    PDone();
}

void main()
{
    PClockStart(60.0);
    DelayCommand(1.0, Setup());
    DelayCommand(30.0, Report());
}
