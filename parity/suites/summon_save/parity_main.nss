#include "parity_inc"

void MakeSummon()
{
    ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY,
        EffectSummonCreature("nw_s_badgerdire", -1, 1.0, FALSE),
        GetLocation(OBJECT_SELF), 20.0);
}

void Report(string phase)
{
    object caster = GetObjectByTag("SAVE_MASTER");
    object summon = GetAssociate(ASSOCIATE_TYPE_SUMMONED, caster);
    PLogI(phase + ".mastervalid", GetIsObjectValid(caster));
    PLogI(phase + ".summonvalid", GetIsObjectValid(summon));
    if (GetIsObjectValid(summon))
    {
        PLog(phase + ".resref", GetResRef(summon));
        PLogI(phase + ".owner", GetMaster(summon) == caster);
        PLogI(phase + ".hp", GetCurrentHitPoints(summon));
        PLogI(phase + ".type", GetAssociateType(summon));
    }
    effect e = GetFirstEffect(caster);
    int count = 0;
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e, TRUE) == 100)
        {
            count++;
            PLogF(phase + ".remaining", IntToFloat(GetEffectDurationRemaining(e)), 1.0);
        }
        e = GetNextEffect(caster);
    }
    PLogI(phase + ".effects", count);
    PLogI(phase + ".difficulty", GetGameDifficulty());
    PLogI(phase + ".setup", GetLocalInt(GetModule(), "SETUP"));
}

void main()
{
    // Native dedicated-server Load runs OnModuleLoad again. Its saved locals and
    // DelayCommands survive, so never create a second fixture on that callback.
    int loads = GetLocalInt(GetModule(), "LOADS") + 1;
    SetLocalInt(GetModule(), "LOADS", loads);
    WriteTimestampedLogEntry("SAVE_EVENT|module_load|" + IntToString(loads));
    if (GetLocalInt(GetModule(), "SETUP")) return;
    SetLocalInt(GetModule(), "SETUP", 1);
    object caster = CreateObject(OBJECT_TYPE_CREATURE, "sum_wiz20", PCentre(), FALSE, "SAVE_MASTER");
    AssignCommand(caster, MakeSummon());
    DelayCommand(4.0, PLog("__save__", "1"));
    DelayCommand(8.0, Report("resume"));
    DelayCommand(17.0, Report("beforeexpiry"));
    DelayCommand(23.0, Report("afterexpiry"));
    DelayCommand(25.0, PDone());
}
