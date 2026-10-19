// Parties, henchmen and faction iteration. Objects come from module locals.
int CountMembers(object oMember, int bPCOnly)
{
    int n = 0;
    object o = GetFirstFactionMember(oMember, bPCOnly);
    while (GetIsObjectValid(o)) { n++; o = GetNextFactionMember(oMember, bPCOnly); }
    return n;
}

void main()
{
    object m = GetModule();
    object pc1 = GetLocalObject(m, "pc1");
    object pc2 = GetLocalObject(m, "pc2");
    object hench = GetLocalObject(m, "hench");
    object hench2 = GetLocalObject(m, "hench2");

    SetLocalInt(m, "eq_before", GetFactionEqual(pc1, pc2));
    AddToParty(pc2, pc1);
    SetLocalInt(m, "eq_after", GetFactionEqual(pc1, pc2));
    SetLocalObject(m, "leader", GetFactionLeader(pc2));

    AddHenchman(pc1, hench);
    AddHenchman(pc1, hench2); // over the limit of one
    SetLocalObject(m, "h1", GetHenchman(pc1));
    SetLocalObject(m, "h2", GetHenchman(pc1, 2));
    SetLocalObject(m, "assoc", GetAssociate(ASSOCIATE_TYPE_HENCHMAN, pc1));
    SetLocalObject(m, "master", GetMaster(hench));
    SetLocalInt(m, "htype", GetAssociateType(hench));
    SetLocalInt(m, "heq", GetFactionEqual(hench, pc2));
    SetLocalInt(m, "pcs", CountMembers(pc1, TRUE));
    SetLocalInt(m, "all", CountMembers(pc1, FALSE));
    SetLocalInt(m, "gold", GetFactionGold(pc1));
    SetLocalInt(m, "avglevel", GetFactionAverageLevel(pc1));
    SetLocalObject(m, "strongest", GetFactionStrongestMember(pc1, FALSE));
    SetLocalObject(m, "damaged", GetFactionMostDamagedMember(pc1, FALSE));

    SetMaxHenchmen(2);
    AddHenchman(pc1, hench2);
    SetLocalObject(m, "h2b", GetHenchman(pc1, 2));
    SetLocalInt(m, "max", GetMaxHenchmen());

    RemoveHenchman(pc1, hench);
    SetLocalInt(m, "heq_removed", GetFactionEqual(hench, pc1));
    SetLocalObject(m, "master_removed", GetMaster(hench));

    RemoveFromParty(pc2);
    SetLocalInt(m, "eq_removed", GetFactionEqual(pc1, pc2));
    SetLocalObject(m, "leader2", GetFactionLeader(pc2));
}
