// OnDamaged: how a weapon hit reports its damage by type.
void main()
{
    object oModule = GetModule();
    SetLocalInt(oModule, "dmg_total", GetTotalDamageDealt());
    SetLocalInt(oModule, "dmg_base", GetDamageDealtByType(DAMAGE_TYPE_BASE_WEAPON));
    SetLocalInt(oModule, "dmg_slashing", GetDamageDealtByType(DAMAGE_TYPE_SLASHING));
    SetLocalInt(oModule, "dmg_fire", GetDamageDealtByType(DAMAGE_TYPE_FIRE));
}
