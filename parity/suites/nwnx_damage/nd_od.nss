// OnDamaged of the scripted-damage targets: what was dealt.
#include "parity_inc"
void main()
{
    object o = OBJECT_SELF;
    int n = GetLocalInt(o, "ODN") + 1;
    SetLocalInt(o, "ODN", n);
    PLog(GetTag(o) + ".od" + IntToString(n), "by" + GetTag(GetLastDamager())
        + " total" + IntToString(GetTotalDamageDealt()) + " bw" + IntToString(GetDamageDealtByType(DAMAGE_TYPE_BASE_WEAPON))
        + " s" + IntToString(GetDamageDealtByType(DAMAGE_TYPE_SLASHING)) + " p" + IntToString(GetDamageDealtByType(DAMAGE_TYPE_PIERCING))
        + " f" + IntToString(GetDamageDealtByType(DAMAGE_TYPE_FIRE)) + " m" + IntToString(GetDamageDealtByType(DAMAGE_TYPE_MAGICAL))
        + " a" + IntToString(GetDamageDealtByType(DAMAGE_TYPE_ACID)) + " hp" + IntToString(GetCurrentHitPoints(o)));
}
