#include "parity_itemev"
void main()
{
    object it = GetModuleItemLost();
    PEvAdd("unacq:" + PEvName(it) + "<" + PEvName(GetModuleItemLostBy()) + "@" + PEvName(GetItemPossessor(it)) + "v" + IntToString(GetIsObjectValid(it)));
}
