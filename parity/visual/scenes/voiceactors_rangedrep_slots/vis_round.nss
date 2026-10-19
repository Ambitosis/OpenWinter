void main() { WriteTimestampedLogEntry("VISUAL|voice_round|"+IntToString(GetLocalInt(OBJECT_SELF,"actor"))+"|ammo|"+IntToString(GetItemStackSize(GetItemInSlot(INVENTORY_SLOT_ARROWS,OBJECT_SELF)))); }
