// Killing blows: OnPhysicalAttacked comes with the swing, before the damage (so the target still has all
// its hit points, and it fires for the blow that kills); afterwards the attacker has no attack target
// and no action but stays in combat, and its round ends early when swings were left: 4.5 s into a round
// of two swings, 3.65 or 4.65 s into one of three, by the swing that killed. nw_drowfight010 with +20
// attack and Str 29 deals 12-21 a hit to a 12-hit-point nw_bandit001, immune to critical hits, given 0, 12
// or 30 temporary hit points, so it dies to the first, second or third hit. (Times run a little long on
// nwserver under load, hence the tolerance.)
#include "pcombat_inc"

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

void Look(object a, string g)
{
    // Every killer has no target and no action left, but is still in combat.
    PAdd(g + ".after.combat", !GetIsObjectValid(GetAttackTarget(a)) && GetCurrentAction(a) == ACTION_INVALID && GetIsInCombat(a));
}

void Setup()
{
    int n, k, r;
    for (r = 0; r < 2; r++)
    for (n = 1; n <= 6; n++)
        for (k = 1; k <= 3; k++)
        {
            if (k > n) continue;
            object a = PPair("kill", "nw_drowfight010", "nw_bandit001");
            SetLocalString(a, "K", IntToString(n) + "apr.kill" + IntToString(k));
            SetBaseAttackBonus(n, a);
            SetLocalInt(a, "N", n);
            Perm(a, EffectAttackIncrease(20));
            Perm(a, EffectAbilityIncrease(ABILITY_STRENGTH, 12));
            object t = GetLocalObject(a, "T");
            SetImmortal(t, FALSE);
            Perm(t, EffectImmunity(IMMUNITY_TYPE_CRITICAL_HIT));
            if (k == 2) Perm(t, EffectTemporaryHitpoints(12));
            if (k == 3) Perm(t, EffectTemporaryHitpoints(30));
            SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_DEATH, "pc_death");
            DelayCommand(7.0, Look(a, "kill"));
        }
    PStartAll(1.0);
}

void Report()
{
    PLogI("kill.died", PGet("kill.died"));
    // Every blow was announced with the target's hit points before it landed.
    PLogI("kill.announced.before.damage", PGet("kill.hpseen") >= 12 * PGet("kill.announced"));
    // (A target that took a few misses may not be dead yet when this is checked.)
    PLogRate("kill.after.combat", PGet("kill.after.combat"), PGet("kill.died"), 0.05);
    PLogI("kill.round.target", PGet("kill.eortarget"));
    string s = "swing1.of2 swing1.of3 swing2.of3 last.swing single.attack ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string c = GetStringLeft(s, sp);
        PLogMean("kill.round." + c, IntToFloat(PGet("kill." + c + ".sum")) / 20.0, PGet("kill." + c + ".n"), 0.05, 0.25);
        s = GetSubString(s, sp + 1, 1000);
    }
    PDone();
}

void main()
{
    PClockStart(40.0);
    DelayCommand(1.0, Setup());
    DelayCommand(10.0, Report());
}
