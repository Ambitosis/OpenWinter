#include "parity_itemev"
void main()
{
    PEvAdd("uneq:" + PEvName(GetPCItemLastUnequipped()) + "<" + PEvName(GetPCItemLastUnequippedBy()) + "s" + IntToString(GetPCItemLastUnequippedSlot()));
}
