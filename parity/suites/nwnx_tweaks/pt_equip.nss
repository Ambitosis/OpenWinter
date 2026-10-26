#include "parity_inc"
void main()
{
    PLog("equip.event", GetTag(GetPCItemLastEquippedBy()) + "/" + GetTag(GetPCItemLastEquipped()));
}
