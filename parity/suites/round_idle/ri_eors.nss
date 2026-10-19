// Attacker OnEndCombatRound: what it sees, then the stock end-of-round AI, then what it is doing 0.3 s later.
#include "ri_inc"
void RiAfter(object a)
{
    RiNote(a, "after act=" + IntToString(GetCurrentAction(a)) + " tgt=" + IntToString(GetAttackTarget(a) == GetLocalObject(a, "T"))
        + " att=" + IntToString(GetAttemptedAttackTarget() == GetLocalObject(a, "T")));
}
void main()
{
    object a = OBJECT_SELF;
    object t = GetLocalObject(a, "T");
    RiMark(a, "e");
    object n = GetNearestCreature(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY, a, 1, CREATURE_TYPE_PERCEPTION, PERCEPTION_SEEN);
    RiNote(a, "tgt=" + IntToString(GetIsObjectValid(GetAttackTarget(a))) + " att=" + IntToString(GetIsObjectValid(GetAttemptedAttackTarget()))
        + " act=" + IntToString(GetCurrentAction(a)) + " combat=" + IntToString(GetIsInCombat(a))
        + " seen=" + IntToString(GetObjectSeen(t, a)) + " heard=" + IntToString(GetObjectHeard(t, a))
        + " enemy=" + IntToString(GetIsEnemy(t, a)) + " rep=" + IntToString(GetReputation(a, t))
        + " nearestSeenEnemy=" + (n == t ? "T" : GetIsObjectValid(n) ? "other" : "none")
        + " lastHostile=" + IntToString(GetIsObjectValid(GetLastHostileActor(a))));
    ExecuteScript("nw_c2_default3", a);
    DelayCommand(0.3, RiAfter(a));
}
