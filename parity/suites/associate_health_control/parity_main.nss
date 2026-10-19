#include "parity_inc"

void MakeSummon()
{
    ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY,
        EffectSummonCreature("nw_s_badgerdire", -1, 1.0, FALSE), GetLocation(OBJECT_SELF), 24.0);
}

void Alter(object master, int type, int dead)
{
    object a = GetAssociate(type, master);
    SetLocalObject(master, "SAVED_ASSOC", a);
    SetCurrentHitPoints(master, 1); // Familiar death cannot introduce a random master-HP delta.
    if (dead) ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), a);
    else SetCurrentHitPoints(a, GetMaxHitPoints(a) - 7);
}

void Snapshot(object master)
{
    object a = GetLocalObject(master, "SAVED_ASSOC");
    SetLocalInt(master, "BEFORE_VALID", GetIsObjectValid(a));
    SetLocalInt(master, "BEFORE_HP", GetIsObjectValid(a) ? GetCurrentHitPoints(a) : 0);
    SetLocalInt(master, "BEFORE_MASTER_HP", GetCurrentHitPoints(master));
    if (GetIsObjectValid(a)) SetLocalString(a, "SAVE_MARK", "original-associate");
    WriteTimestampedLogEntry("HEALTH_SAVE|" + GetTag(master) + "|valid=" + IntToString(GetIsObjectValid(a))
        + "|hp=" + IntToString(GetIsObjectValid(a) ? GetCurrentHitPoints(a) : 0));
}

void ReportOne(string phase, string tag, int type)
{
    object master = GetObjectByTag(tag);
    object a = GetLocalObject(master, "SAVED_ASSOC");
    string key = phase + "." + tag;
    PLogI(key + ".mastervalid", GetIsObjectValid(master));
    PLogI(key + ".masterhpdelta", GetCurrentHitPoints(master) - GetLocalInt(master, "BEFORE_MASTER_HP"));
    PLogI(key + ".beforevalid", GetLocalInt(master, "BEFORE_VALID"));
    PLogI(key + ".valid", GetIsObjectValid(a));
    PLogI(key + ".listed", GetIsObjectValid(GetAssociate(type, master)));
    if (type == ASSOCIATE_TYPE_FAMILIAR) PLogI(key + ".uses", GetFeatRemainingUses(FEAT_SUMMON_FAMILIAR, master));
    if (type == ASSOCIATE_TYPE_ANIMALCOMPANION) PLogI(key + ".uses", GetFeatRemainingUses(FEAT_ANIMAL_COMPANION, master));
    if (GetIsObjectValid(a))
    {
        PLog(key + ".marker", GetLocalString(a, "SAVE_MARK"));
        PLog(key + ".resref", GetResRef(a));
        PLogI(key + ".dead", GetIsDead(a));
        PLogI(key + ".hp", GetCurrentHitPoints(a));
        PLogI(key + ".hpdelta", GetCurrentHitPoints(a) - GetLocalInt(master, "BEFORE_HP"));
        PLogI(key + ".maxhp", GetMaxHitPoints(a));
        PLogI(key + ".owner", GetMaster(a) == master);
        PLogI(key + ".type", GetAssociateType(a));
        PLogI(key + ".samefaction", GetFactionEqual(a, master));
        PLogI(key + ".raiseable", GetIsRaiseable(a));
    }
    int count = 0;
    effect e = GetFirstEffect(master);
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e, TRUE) == 100)
        {
            count++;
            PLogF(key + ".remaining", IntToFloat(GetEffectDurationRemaining(e)), 1.0);
        }
        e = GetNextEffect(master);
    }
    PLogI(key + ".effects", count);
}

void Report(string phase)
{
    ReportOne(phase, "FAM_HURT", ASSOCIATE_TYPE_FAMILIAR);
    ReportOne(phase, "FAM_DEAD", ASSOCIATE_TYPE_FAMILIAR);
    ReportOne(phase, "COMP_HURT", ASSOCIATE_TYPE_ANIMALCOMPANION);
    ReportOne(phase, "COMP_DEAD", ASSOCIATE_TYPE_ANIMALCOMPANION);
    ReportOne(phase, "SUM_HURT", ASSOCIATE_TYPE_SUMMONED);
    ReportOne(phase, "SUM_DEAD", ASSOCIATE_TYPE_SUMMONED);
}

void Case(int index, string tag, int type, int dead)
{
    string resource = "sum_wiz20";
    if (type == ASSOCIATE_TYPE_ANIMALCOMPANION) resource = "sum_dru10";
    object master = CreateObject(OBJECT_TYPE_CREATURE, resource,
        PCentre(IntToFloat(index % 3) * 35.0, IntToFloat(index / 3) * 40.0), FALSE, tag);
    if (type == ASSOCIATE_TYPE_SUMMONED) DelayCommand(2.0, AssignCommand(master, MakeSummon()));
    else
    {
        int feat = FEAT_SUMMON_FAMILIAR;
        if (type == ASSOCIATE_TYPE_ANIMALCOMPANION) feat = FEAT_ANIMAL_COMPANION;
        DelayCommand(2.0, AssignCommand(master, ActionUseFeat(feat, master)));
    }
    DelayCommand(7.0, Alter(master, type, dead));
    DelayCommand(9.0, Snapshot(master));
}

void main()
{
    if (GetLocalInt(GetModule(), "SETUP")) return;
    SetLocalInt(GetModule(), "SETUP", 1);
    Case(0, "FAM_HURT", ASSOCIATE_TYPE_FAMILIAR, FALSE);
    Case(1, "FAM_DEAD", ASSOCIATE_TYPE_FAMILIAR, TRUE);
    Case(2, "COMP_HURT", ASSOCIATE_TYPE_ANIMALCOMPANION, FALSE);
    Case(3, "COMP_DEAD", ASSOCIATE_TYPE_ANIMALCOMPANION, TRUE);
    Case(4, "SUM_HURT", ASSOCIATE_TYPE_SUMMONED, FALSE);
    Case(5, "SUM_DEAD", ASSOCIATE_TYPE_SUMMONED, TRUE);
    DelayCommand(12.0, Report("restored"));
    DelayCommand(20.0, Report("late"));
    DelayCommand(35.0, Report("expiry"));
    DelayCommand(37.0, PDone());
}
