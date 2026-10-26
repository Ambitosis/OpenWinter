// Pair targets' OnDamaged: the usual tallies, and what the event scripts made of the blow.
#include "pcombat_inc"
void main()
{
    PcOnDamaged();
    object a = GetLastDamager();
    string g = GetLocalString(a, "G");
    int total = GetTotalDamageDealt();
    int fire = GetDamageDealtByType(DAMAGE_TYPE_FIRE);
    int bw = GetDamageDealtByType(DAMAGE_TYPE_BASE_WEAPON);
    if (g == "atkmod" && (total != 7 || fire != 7 || bw != 0)) PAdd(g + ".odbad");
    if (g == "miss2hit" && total != 5) PAdd(g + ".odbad");
    if (g == "dmgmod" && (total != 6 || fire != 5 || bw != 1 || GetDamageDealtByType(DAMAGE_TYPE_SLASHING) != -1)) PAdd(g + ".odbad");
}
