void QNote(string s) { WriteTimestampedLogEntry("VISUAL|note|"+s); }
string QItem(object item)
{
    if(!GetIsObjectValid(item))return "empty";
    return GetResRef(item)+":"+GetTag(item);
}
void QState(object pc,string phase)
{
    object right=GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,pc);
    object left=GetItemInSlot(INVENTORY_SLOT_LEFTHAND,pc);
    object pairRight=GetLocalObject(pc,"qp_right"),pairLeft=GetLocalObject(pc,"qp_left");
    QNote(phase+"|right="+QItem(right)+"|left="+QItem(left)
        +"|pairRight="+IntToString(right==pairRight)+"|pairLeft="+IntToString(left==pairLeft)
        +"|ownsRight="+IntToString(GetItemPossessor(pairRight)==pc)
        +"|ownsLeft="+IntToString(GetItemPossessor(pairLeft)==pc)
        +"|action="+IntToString(GetCurrentAction(pc)));
}
