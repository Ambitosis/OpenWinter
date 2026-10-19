#include "es_inc"
void main()
{
    if (GetLocalInt(OBJECT_SELF, "hb") == 0 || GetTag(OBJECT_SELF) == "ES_" + GetStringUpperCase(GetLocalString(Mod(), "cur")))
        Log("event|heartbeat|" + GetTag(OBJECT_SELF) + "|" + EncState(OBJECT_SELF));
    SetLocalInt(OBJECT_SELF, "hb", 1);
}
