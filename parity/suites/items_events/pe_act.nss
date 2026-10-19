#include "parity_itemev"
void main()
{
    PEvAdd("act:" + PEvName(GetItemActivated()) + ">" + PEvName(GetItemActivator()) + "t" + PEvName(GetItemActivatedTarget())
        + "l" + IntToString(GetIsObjectValid(GetAreaFromLocation(GetItemActivatedTargetLocation()))));
}
