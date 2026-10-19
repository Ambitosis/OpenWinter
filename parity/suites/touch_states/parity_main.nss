// Scripted touch attacks use a distinct defense calculation from weapon combat.
#include "parity_fx"
const int N = 3600;
object Make()
{
    int cell = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", cell + 1);
    object o = PFxSpawn(cell, "nw_bandit001");
    int i;
    for (i = 5000; i <= 5012; i++) SetEventScript(o, i, "");
    return o;
}
void Rates(string key, object a, object t)
{
    int i, melee, ranged;
    for (i=0; i<N; i++)
    {
        if (TouchAttackMelee(t, FALSE, a)>0) melee++;
        if (TouchAttackRanged(t, FALSE, a)>0) ranged++;
    }
    PLogF(key+".melee", IntToFloat(melee)/IntToFloat(N), 0.045);
    PLogF(key+".ranged", IntToFloat(ranged)/IntToFloat(N), 0.045);
    PLogI(key+".dex", GetAbilityScore(t, ABILITY_DEXTERITY));
}
void State(string key, effect e)
{
    object a=Make(), t=Make();
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(8, AC_DEFLECTION_BONUS), t);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, t);
    Rates(key,a,t);
}
void AttackerState(string key, effect e)
{
    object a=Make(), t=Make();
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(4, AC_DEFLECTION_BONUS), t);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, a);
    Rates(key,a,t);
}
void MovingRate(string key, object a, object t, object other)
{
    WriteTimestampedLogEntry(key + " action=" + IntToString(GetCurrentAction(t)) + " attacktarget=" + IntToString(GetIsObjectValid(GetAttackTarget(t))));
    if (key == "flanked_target") PLogI("flanked_target.attacking", GetAttackTarget(t) == other);
    Rates(key,a,t);
}
void MovingTarget()
{
    object a=Make(), t=Make(), other=Make();
    location start = Location(GetArea(a), Vector(20.0,20.0,0.0),0.0);
    AssignCommand(a, JumpToLocation(start));
    AssignCommand(t, JumpToLocation(Location(GetArea(a), Vector(23.0,20.0,0.0),0.0)));
    AssignCommand(other, JumpToLocation(Location(GetArea(a), Vector(27.0,20.0,0.0),0.0)));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(4, AC_DEFLECTION_BONUS),t);
    SetImmortal(other,TRUE);
    ChangeToStandardFaction(t, STANDARD_FACTION_HOSTILE);
    ChangeToStandardFaction(other, STANDARD_FACTION_COMMONER);
    SetIsTemporaryEnemy(t,other);
    SetIsTemporaryEnemy(other,t);
    AssignCommand(t, ActionMoveToLocation(Location(GetArea(a), Vector(27.0,20.0,0.0),0.0), TRUE));
    DelayCommand(0.8, AssignCommand(t, ClearAllActions()));
    DelayCommand(0.9, AssignCommand(t, ActionAttack(other)));
    DelayCommand(0.2,MovingRate("moving_target",a,t,other));
    DelayCommand(4.0,MovingRate("flanked_target",a,t,other));
}
void main()
{
    DelayCommand(0.25, State("base", EffectVisualEffect(VFX_NONE)));
    DelayCommand(0.50, State("dex_up", EffectAbilityIncrease(ABILITY_DEXTERITY,10)));
    DelayCommand(0.75, State("dex_down", EffectAbilityDecrease(ABILITY_DEXTERITY,10)));
    DelayCommand(1.00, State("paralyze", EffectParalyze()));
    DelayCommand(1.25, State("stun", EffectStunned()));
    DelayCommand(1.50, State("sleep", EffectSleep()));
    DelayCommand(1.75, State("entangle", EffectEntangle()));
    DelayCommand(2.00, State("petrify", EffectPetrify()));
    DelayCommand(2.25, State("cutscene_paralyze", EffectCutsceneParalyze()));
    DelayCommand(2.50, State("cutscene_immobile", EffectCutsceneImmobilize()));
    DelayCommand(2.75, State("dodge", EffectACIncrease(6, AC_DODGE_BONUS)));
    DelayCommand(3.00, State("natural", EffectACIncrease(6, AC_NATURAL_BONUS)));
    DelayCommand(3.25, State("target_blind", EffectBlindness()));
    DelayCommand(3.50, State("target_prone", EffectKnockdown()));
    DelayCommand(3.75, AttackerState("attacker_base", EffectVisualEffect(VFX_NONE)));
    DelayCommand(4.00, AttackerState("attacker_blind", EffectBlindness()));
    DelayCommand(4.25, AttackerState("attacker_invisible", EffectInvisibility(INVISIBILITY_TYPE_NORMAL)));
    DelayCommand(4.50, MovingTarget());
    DelayCommand(10.0, PDone());
}
