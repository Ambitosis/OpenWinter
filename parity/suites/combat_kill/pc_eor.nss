// Attacker OnEndCombatRound: how long the round that killed the target lasted, by which swing killed it
// (attacks less than half a second apart share a swing: 4 attacks are swings 1,1,2,3; 5 are 1,1,2,2,3).
#include "pcombat_inc"
void main()
{
    object a = OBJECT_SELF;
    string g = GetLocalString(a, "G");
    if (GetLocalInt(a, "EORN") == 0 && GetLocalInt(a, "KILLIDX") > 0)
    {
        int n = GetLocalInt(a, "N"), k = GetLocalInt(a, "KILLIDX");
        int swings = n < 3 ? n : 3;
        int swing = n <= 3 ? k : (k <= 2 ? 1 : (n == 4 ? k - 1 : (k + 1) / 2));
        string c = n == 1 ? "single.attack" : swing >= swings ? "last.swing" : "swing" + IntToString(swing) + ".of" + IntToString(swings);
        PAdd(g + "." + c + ".sum", PClock() - GetLocalInt(a, "START"));
        PAdd(g + "." + c + ".n");
        PAdd(g + ".eortarget", GetIsObjectValid(GetAttackTarget()));
    }
    SetLocalInt(a, "EORN", 1);
    PcOnEndRound();
}
