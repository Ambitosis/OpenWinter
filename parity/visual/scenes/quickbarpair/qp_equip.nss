#include "qp_state"
void main()
{
    object pc=GetPCItemLastEquippedBy();
    if(!GetIsPC(pc))return;
    int order=GetLocalInt(pc,"qp_order")+1;SetLocalInt(pc,"qp_order",order);
    QState(pc,"event."+IntToString(order)+".equip."+QItem(GetPCItemLastEquipped()));
}
