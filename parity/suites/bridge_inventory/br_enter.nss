// Gives the entering player a spread of items: weapons, armour, stacks, charges, unidentified, a bag, gold.
void Give(string sResRef, object oPC, int nStack = 1, int bIdentified = TRUE)
{
    object o = CreateItemOnObject(sResRef, oPC, nStack);
    if (!bIdentified) SetIdentified(o, FALSE);
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC) || GetLocalInt(oPC, "BR_GIVEN")) return;
    SetLocalInt(oPC, "BR_GIVEN", TRUE);
    GiveGoldToCreature(oPC, 1000);
    Give("nw_wswls001", oPC);
    Give("nw_aarcl004", oPC);
    Give("nw_arhe001", oPC);
    Give("nw_it_mpotion001", oPC, 5);
    Give("nw_wmgwn004", oPC);
    Give("nw_wamar001", oPC, 40);
    Give("nw_it_mring001", oPC, 1, FALSE);
    object oBag = CreateItemOnObject("nw_it_contain001", oPC);
    CreateItemOnObject("nw_it_mpotion002", oBag, 2);
}
