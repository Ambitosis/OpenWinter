// GetNearestCreature / GetNearestCreatureToLocation: nth, criteria and their combinations.
// Creatures stand where the original places them exactly (checked by "pos.*").
// Not covered: with two or more objects on the low-x side of the target, the original's nth (> 1) searches
// sometimes return the next object instead (see the report); the main target here has everything on its high-x side.
// Reputation is not covered either: the generated module has no faction file.
#include "parity_inc"

object Spawn(string sResRef, float x, float y, string sTag)
{
    object a = GetObjectByTag("PARITY_AREA");
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(a, Vector(x, y, 0.0), 0.0), FALSE, sTag);
    PLogI("pos." + sTag, GetDistanceBetweenLocations(GetLocation(o), Location(a, Vector(x, y, 0.0), 0.0)) < 0.01);
    return o;
}

string N(int t1, int v1, object o, int nth, int t2 = -1, int v2 = -1, int t3 = -1, int v3 = -1)
{
    return GetTag(GetNearestCreature(t1, v1, o, nth, t2, v2, t3, v3));
}

void Setup()
{
    Spawn("nw_dog", 22.0, 20.0, "dog");
    Spawn("nw_goblina", 22.43, 21.76, "gob");
    Spawn("nw_bandit001", 21.24, 23.80, "bandit");
    Spawn("nw_orca", 15.15, 23.53, "orc");
    Spawn("nw_oldman", 13.53, 15.30, "oldman");
    Spawn("nw_kobold001", 23.09, 10.49, "kobold");
    object a = GetObjectByTag("PARITY_AREA");
    CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", Location(a, Vector(12.0, 20.0, 0.0), 0.0), FALSE, "centre");
}

void Tests()
{
    object c = GetObjectByTag("centre");
    object dog = GetObjectByTag("dog");
    int i;
    for (i = -1; i <= 7; i++)
    {
        string k = IntToString(i);
        PLog("alive." + k, N(CREATURE_TYPE_IS_ALIVE, TRUE, c, i));
        if (i <= 1) PLog("fromdog.alive." + k, N(CREATURE_TYPE_IS_ALIVE, TRUE, dog, i));
    }
    PLog("race.animal", N(CREATURE_TYPE_RACIAL_TYPE, RACIAL_TYPE_ANIMAL, c, 1));
    PLog("race.human.1", N(CREATURE_TYPE_RACIAL_TYPE, RACIAL_TYPE_HUMAN, c, 1));
    PLog("race.human.2", N(CREATURE_TYPE_RACIAL_TYPE, RACIAL_TYPE_HUMAN, c, 2));
    PLog("race.none", N(CREATURE_TYPE_RACIAL_TYPE, RACIAL_TYPE_DRAGON, c, 1));
    PLog("race.all", N(CREATURE_TYPE_RACIAL_TYPE, RACIAL_TYPE_ALL, c, 1));
    PLog("class.fighter", N(CREATURE_TYPE_CLASS, CLASS_TYPE_FIGHTER, c, 1));
    PLog("class.commoner", N(CREATURE_TYPE_CLASS, CLASS_TYPE_COMMONER, c, 1));
    PLog("pc.no", N(CREATURE_TYPE_PLAYER_CHAR, PLAYER_CHAR_NOT_PC, c, 1));
    PLog("pc.yes", N(CREATURE_TYPE_PLAYER_CHAR, PLAYER_CHAR_IS_PC, c, 1));
    PLog("dead", N(CREATURE_TYPE_IS_ALIVE, FALSE, c, 1));
    PLog("combo.human.alive", N(CREATURE_TYPE_RACIAL_TYPE, RACIAL_TYPE_HUMAN, c, 1, CREATURE_TYPE_IS_ALIVE, TRUE));
    PLog("combo.three", N(CREATURE_TYPE_IS_ALIVE, TRUE, c, 2, CREATURE_TYPE_PLAYER_CHAR, PLAYER_CHAR_NOT_PC, CREATURE_TYPE_RACIAL_TYPE, RACIAL_TYPE_HUMANOID_GOBLINOID));
    PLog("combo.badtype", N(99, 1, c, 1));
    PLog("combo.spell", N(CREATURE_TYPE_HAS_SPELL_EFFECT, SPELL_BLESS, c, 1));
    PLog("combo.nospell", N(CREATURE_TYPE_DOES_NOT_HAVE_SPELL_EFFECT, SPELL_BLESS, c, 1));
    PLog("target.invalid", N(CREATURE_TYPE_IS_ALIVE, TRUE, OBJECT_INVALID, 1));
    PLog("target.module", N(CREATURE_TYPE_IS_ALIVE, TRUE, GetModule(), 1));
    location l = Location(GetObjectByTag("PARITY_AREA"), Vector(12.5, 16.0, 0.0), 0.0);
    for (i = 0; i <= 3; i++) PLog("loc.alive." + IntToString(i), GetTag(GetNearestCreatureToLocation(CREATURE_TYPE_IS_ALIVE, TRUE, l, i)));
    PLog("loc.race", GetTag(GetNearestCreatureToLocation(CREATURE_TYPE_RACIAL_TYPE, RACIAL_TYPE_HUMAN, l, 1)));
    PLog("loc.invalid", GetTag(GetNearestCreatureToLocation(CREATURE_TYPE_IS_ALIVE, TRUE, Location(OBJECT_INVALID, Vector(), 0.0), 1)));
    // A dead creature (killed outright) and a hidden one.
    object o = GetObjectByTag("oldman");
    AssignCommand(o, SetIsDestroyable(FALSE, FALSE, FALSE));
    DelayCommand(0.1, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), o));
    DelayCommand(0.5, PLog("after.dead", N(CREATURE_TYPE_IS_ALIVE, FALSE, c, 1)));
    DelayCommand(0.5, PLog("after.alive.5", N(CREATURE_TYPE_IS_ALIVE, TRUE, c, 5)));
    DelayCommand(0.5, PLog("after.race.human.2", N(CREATURE_TYPE_RACIAL_TYPE, RACIAL_TYPE_HUMAN, c, 2)));
}

void main()
{
    DelayCommand(1.0, Setup());
    DelayCommand(1.5, Tests());
    DelayCommand(3.0, PDone());
}
