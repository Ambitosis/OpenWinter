#include "parity_inc"
void Summon() { ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY,
    EffectSummonCreature("nw_s_badgerdire", -1, 0.0, FALSE), GetLocation(OBJECT_SELF), 15.0); }
void Die(object master, int stock)
{
    object a = GetAssociate(ASSOCIATE_TYPE_SUMMONED, master);
    SetLocalObject(master, "OLD", a);
    SetLocalObject(a, "OWNER", master);
    SetLocalString(a, "KEY", GetTag(master));
    if (stock) SetLocalString(a, "STOCK", GetEventScript(a, EVENT_SCRIPT_CREATURE_ON_DEATH));
    SetEventScript(a, EVENT_SCRIPT_CREATURE_ON_DEATH, "sum_death");
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), a);
}
void Report(object master, string phase)
{
    object a = GetLocalObject(master, "OLD");
    string key = GetTag(master) + "." + phase;
    PLogI(key + ".valid", GetIsObjectValid(a));
    PLogI(key + ".listed", GetIsObjectValid(GetAssociate(ASSOCIATE_TYPE_SUMMONED, master)));
    if (GetIsObjectValid(a))
    {
        PLogI(key + ".master", GetMaster(a) == master);
        PLogI(key + ".type", GetAssociateType(a));
        PLogI(key + ".samefaction", GetFactionEqual(a, master));
    }
}
void main()
{
    int i;
    for (i = 0; i < 2; i++)
    {
        object master = CreateObject(OBJECT_TYPE_CREATURE, "sum_wiz20", PCentre(IntToFloat(i)*30.0,0.0), FALSE, "case"+IntToString(i));
        DelayCommand(1.0, AssignCommand(master, Summon()));
        DelayCommand(3.0, Die(master, i));
        DelayCommand(4.0, Report(master, "early"));
        DelayCommand(8.0, Report(master, "late"));
    }
    DelayCommand(10.0, PDone());
}
