// Combat engine functions: touch attacks, whirlwind, last attack bookkeeping.
void main()
{
    object oModule = GetModule();
    object oTarget = GetLocalObject(OBJECT_SELF, "target");
    SetLocalInt(oModule, "touch", TouchAttackMelee(oTarget, FALSE));
    SetActionMode(OBJECT_SELF, ACTION_MODE_POWER_ATTACK, TRUE);
    DoWhirlwindAttack(TRUE, FALSE);
    SetLocalInt(oModule, "type", GetLastAttackType(OBJECT_SELF));
    SetLocalInt(oModule, "mode", GetLastAttackMode(OBJECT_SELF));
    SetLocalInt(oModule, "weapon_ok", GetLastWeaponUsed(OBJECT_SELF) == GetItemInSlot(INVENTORY_SLOT_RIGHTHAND));
    SetLocalInt(oModule, "attacker_ok", GetLastAttacker(oTarget) == OBJECT_SELF);
    SetLocalInt(oModule, "hp", GetCurrentHitPoints(oTarget));
}
