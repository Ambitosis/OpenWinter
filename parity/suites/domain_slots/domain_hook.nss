// Authored observer called by the installed stock public spell hook.
void main()
{
    SetLocalInt(OBJECT_SELF, "CAST_COUNT", GetLocalInt(OBJECT_SELF, "CAST_COUNT") + 1);
    SetLocalInt(OBJECT_SELF, "CAST_LEVEL", GetLastSpellLevel());
    SetLocalInt(OBJECT_SELF, "CAST_CLASS", GetLastSpellCastClass());
    SetLocalInt(OBJECT_SELF, "CAST_META", GetMetaMagicFeat());
    SetLocalInt(OBJECT_SELF, "CAST_DC", GetSpellSaveDC());
    SetLocalInt(OBJECT_SELF, "CAST_CL", GetCasterLevel(OBJECT_SELF));
}
