// Basics: formatting, maths and the derived stats of standard creatures.
#include "parity_inc"

void Creature(string sResRef, float fDx)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, PCentre(fDx, 0.0));
    string k = "creature." + sResRef + ".";
    if (!GetIsObjectValid(o)) { PLog(k + "valid", "0"); return; }
    PLogI(k + "ac", GetAC(o));
    PLogI(k + "hp", GetMaxHitPoints(o));
    PLogI(k + "hd", GetHitDice(o));
    PLogI(k + "bab", GetBaseAttackBonus(o));
    PLogF(k + "cr", GetChallengeRating(o));
    PLogI(k + "fort", GetFortitudeSavingThrow(o));
    PLogI(k + "ref", GetReflexSavingThrow(o));
    PLogI(k + "will", GetWillSavingThrow(o));
    int i;
    for (i = 0; i < 6; i++)
    {
        PLogI(k + "ability" + IntToString(i), GetAbilityScore(o, i));
        PLogI(k + "abilitymod" + IntToString(i), GetAbilityModifier(i, o));
    }
    PLogI(k + "skill.spot", GetSkillRank(SKILL_SPOT, o));
    PLogI(k + "skill.hide", GetSkillRank(SKILL_HIDE, o));
    PLogI(k + "skill.listen", GetSkillRank(SKILL_LISTEN, o));
    PLogI(k + "size", GetCreatureSize(o));
    PLogI(k + "speed", GetMovementRate(o));
    PLogI(k + "race", GetRacialType(o));
    PLogI(k + "faction.hostile", GetIsEnemy(GetFirstPC(), o));
    PLogI(k + "weapon", GetBaseItemType(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, o)));
    PLogI(k + "armorac", GetItemACValue(GetItemInSlot(INVENTORY_SLOT_CHEST, o)));
}

void Report()
{
    // Formatting
    PLog("fmt.float.default", FloatToString(3.14159));
    PLog("fmt.float.0.2", FloatToString(3.14159, 0, 2));
    PLog("fmt.float.10.1", FloatToString(-2.5, 10, 1));
    PLog("fmt.float.big", FloatToString(123456.789));
    PLog("fmt.int.neg", IntToString(-42));
    PLog("fmt.hex", IntToHexString(48879));
    PLog("fmt.vector", FloatToString(VectorMagnitude(Vector(3.0, 4.0, 0.0))));
    PLog("str.sub", GetSubString("Neverwinter", 5, 6));
    PLog("str.find", IntToString(FindSubString("Neverwinter Nights", "Nights")));
    PLog("str.upper", GetStringUpperCase("Lad/Lass"));
    PLog("str.left", GetStringLeft("abcdef", 10));
    PLog("conv.stoi", IntToString(StringToInt("12abc")));
    PLog("conv.stof", FloatToString(StringToFloat("2.75xyz")));
    // Maths
    PLogF("math.sqrt", sqrt(2.0));
    PLogF("math.pow", pow(2.0, 10.0));
    PLogF("math.log", log(10.0));
    PLogF("math.cos", cos(60.0));
    PLogF("math.atan", atan(1.0));
    PLogF("math.acos", acos(0.5));
    PLogI("math.abs", abs(-7));
    PLogF("math.fabs", fabs(-7.25));
    PLogI("math.intdiv", -7 / 2);
    PLogI("math.mod", -7 % 3);
    PLogI("math.ftoi", FloatToInt(-2.7));
    PLogF("math.feet", FeetToMeters(10.0));
    PLogF("math.yards", YardsToMeters(3.0));
    PLogF("time.round", RoundsToSeconds(3));
    PLogF("time.turn", TurnsToSeconds(2));
    PLogF("time.hour", HoursToSeconds(1));
    // Standard creatures from the base palette.
    Creature("nw_goblina", -6.0);
    Creature("nw_orca", -3.0);
    Creature("nw_skeleton", 0.0);
    Creature("nw_bandit001", 3.0);
    Creature("nw_dog", 6.0);
    Creature("nw_ogre01", 9.0);
    PDone();
}

void main()
{
    // Give the module a moment to finish loading before measuring.
    DelayCommand(2.0, Report());
}
