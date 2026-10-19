#include "parity_inc"

void Make(float duration, float delay = 1.0, object adopt = OBJECT_INVALID)
{
    string resource = "nw_s_badgerdire";
    if (GetIsObjectValid(adopt)) resource = "";
    ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY,
        EffectSummonCreature(resource, -1, delay, FALSE, -1, adopt),
        GetLocation(OBJECT_SELF), duration);
}

object Caster(string tag, float x, float y, string resource = "sum_wiz20")
{
    object c = CreateObject(OBJECT_TYPE_CREATURE, resource, PCentre(x, y), FALSE, tag);
    SetLocalObject(GetModule(), tag, c);
    SetAILevel(c, AI_LEVEL_NORMAL);
    return c;
}

void Remember(string tag, int type)
{
    object c = GetObjectByTag(tag);
    SetLocalObject(GetModule(), tag + "_ASSOC", GetAssociate(type, c));
}

void RemoveSummon(object c)
{
    effect e = GetFirstEffect(c);
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e, TRUE) == 100) { RemoveEffect(c, e); return; }
        e = GetNextEffect(c);
    }
}

void ReportOne(string phase, string tag, int type)
{
    string key = phase + "." + tag;
    object c = GetObjectByTag(tag);
    object a = GetAssociate(type, c);
    object original = GetLocalObject(GetModule(), tag + "_ASSOC");
    PLogI(key + ".mastervalid", GetIsObjectValid(c));
    PLogI(key + ".masterref", c == GetLocalObject(GetModule(), tag));
    PLogI(key + ".valid", GetIsObjectValid(a));
    PLogI(key + ".oldvalid", GetIsObjectValid(original));
    PLogI(key + ".same", GetIsObjectValid(a) && a == original);
    if (GetIsObjectValid(a))
    {
        PLog(key + ".resref", GetResRef(a));
        PLog(key + ".objectname", GetName(a));
        PLogI(key + ".owner", GetMaster(a) == c);
        PLogI(key + ".type", GetAssociateType(a));
        PLogI(key + ".faction", GetFactionEqual(c, a));
        PLogI(key + ".hd", GetHitDice(a));
        PLogI(key + ".hp", GetCurrentHitPoints(a));
        PLogI(key + ".maxhp", GetMaxHitPoints(a));
        PLogI(key + ".ac", GetAC(a));
        PLogI(key + ".appearance", GetAppearanceType(a));
        int ability;
        for (ability = 0; ability < 6; ability++)
            PLogI(key + ".ability" + IntToString(ability), GetAbilityScore(a, ability));
    }
    effect e = GetFirstEffect(c);
    int count = 0;
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e, TRUE) == 100)
        {
            PLogF(key + ".remaining" + IntToString(count), IntToFloat(GetEffectDurationRemaining(e)), 1.0);
            count++;
        }
        e = GetNextEffect(c);
    }
    PLogI(key + ".effects", count);
    if (type == ASSOCIATE_TYPE_FAMILIAR)
    {
        PLogI(key + ".selected", GetFamiliarCreatureType(c));
        PLog(key + ".name", GetFamiliarName(c));
        PLogI(key + ".uses", GetFeatRemainingUses(FEAT_SUMMON_FAMILIAR, c));
    }
    if (type == ASSOCIATE_TYPE_ANIMALCOMPANION)
    {
        PLogI(key + ".selected", GetAnimalCompanionCreatureType(c));
        PLog(key + ".name", GetAnimalCompanionName(c));
        PLogI(key + ".uses", GetFeatRemainingUses(FEAT_ANIMAL_COMPANION, c));
    }
    if (tag == "SPELL")
    {
        PLogI(key + ".known", GetIsInKnownSpellList(c, CLASS_TYPE_WIZARD, SPELL_SUMMON_CREATURE_I));
        PLogI(key + ".ready0", GetMemorizedSpellReady(c, CLASS_TYPE_WIZARD, 1, 0));
        PLogI(key + ".ready1", GetMemorizedSpellReady(c, CLASS_TYPE_WIZARD, 1, 1));
    }
}

void Report(string phase)
{
    ReportOne(phase, "FAMILIAR", ASSOCIATE_TYPE_FAMILIAR);
    ReportOne(phase, "COMPANION", ASSOCIATE_TYPE_ANIMALCOMPANION);
    ReportOne(phase, "SPELL", ASSOCIATE_TYPE_SUMMONED);
    ReportOne(phase, "PENDING", ASSOCIATE_TYPE_SUMMONED);
    ReportOne(phase, "REPLACE", ASSOCIATE_TYPE_SUMMONED);
    ReportOne(phase, "ADOPT", ASSOCIATE_TYPE_SUMMONED);
    ReportOne(phase, "REMOVE", ASSOCIATE_TYPE_SUMMONED);
}

void main()
{
    if (GetLocalInt(GetModule(), "SETUP")) return;
    SetLocalInt(GetModule(), "SETUP", 1);
    object familiar = Caster("FAMILIAR", 0.0, 0.0);
    object companion = Caster("COMPANION", 30.0, 0.0, "sum_dru10");
    object spell = Caster("SPELL", 60.0, 0.0);
    object pending = Caster("PENDING", 90.0, 0.0);
    object replace = Caster("REPLACE", 0.0, 35.0);
    object adopt = Caster("ADOPT", 30.0, 35.0);
    object remove = Caster("REMOVE", 60.0, 35.0);
    object adopted = CreateObject(OBJECT_TYPE_CREATURE, "nw_s_badgerdire", PCentre(30.0, 43.0));
    DelayCommand(2.0, AssignCommand(familiar, ActionUseFeat(FEAT_SUMMON_FAMILIAR, familiar)));
    DelayCommand(2.0, AssignCommand(companion, ActionUseFeat(FEAT_ANIMAL_COMPANION, companion)));
    DelayCommand(2.0, AssignCommand(spell, ActionCastSpellAtLocation(SPELL_SUMMON_CREATURE_I,
        GetLocation(spell), METAMAGIC_NONE, FALSE, PROJECTILE_PATH_TYPE_DEFAULT, FALSE, CLASS_TYPE_WIZARD)));
    AssignCommand(pending, Make(25.0, 12.0));
    AssignCommand(replace, Make(14.0));
    AssignCommand(adopt, Make(20.0, 2.0, adopted));
    AssignCommand(remove, Make(20.0));
    DelayCommand(6.0, Remember("FAMILIAR", ASSOCIATE_TYPE_FAMILIAR));
    DelayCommand(6.0, Remember("COMPANION", ASSOCIATE_TYPE_ANIMALCOMPANION));
    DelayCommand(6.0, Remember("SPELL", ASSOCIATE_TYPE_SUMMONED));
    DelayCommand(6.0, Remember("PENDING", ASSOCIATE_TYPE_SUMMONED));
    DelayCommand(6.0, Remember("REPLACE", ASSOCIATE_TYPE_SUMMONED));
    DelayCommand(6.0, Remember("ADOPT", ASSOCIATE_TYPE_SUMMONED));
    DelayCommand(6.0, Remember("REMOVE", ASSOCIATE_TYPE_SUMMONED));
    DelayCommand(8.0, PLog("__save__", "1"));
    DelayCommand(10.0, Report("restored"));
    DelayCommand(12.0, AssignCommand(replace, Make(20.0)));
    DelayCommand(16.0, RemoveSummon(remove));
    DelayCommand(18.0, Report("changed"));
    DelayCommand(27.0, Report("expired"));
    DelayCommand(35.0, Report("final"));
    DelayCommand(37.0, PDone());
}
