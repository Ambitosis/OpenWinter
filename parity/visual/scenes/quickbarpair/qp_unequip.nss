#include "qp_state"
void main()
{
    object pc=GetPCItemLastUnequippedBy();
    if(!GetIsPC(pc))return;
    int order=GetLocalInt(pc,"qp_order")+1;SetLocalInt(pc,"qp_order",order);
    QState(pc,"event."+IntToString(order)+".unequip."+QItem(GetPCItemLastUnequipped())
        +".slot."+IntToString(GetPCItemLastUnequippedSlot()));
}
