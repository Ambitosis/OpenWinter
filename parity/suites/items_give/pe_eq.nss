#include "parity_itemev"
void main()
{
    PEvAdd("eq:" + PEvName(GetPCItemLastEquipped()) + ">" + PEvName(GetPCItemLastEquippedBy()) + "s" + IntToString(GetPCItemLastEquippedSlot()));
}
