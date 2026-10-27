// A part whose mesh bitmap names no texture: right hand 186 of the female elf (pfe0_handr186, bitmap "hand", which no
// texture is called; a PLT is named after the part, pfh0_handr186). The bare player wears it on her right hand, her
// left hand is the plain one; close views of her hands.
#include "visual_inc"

void Bare(object oPC)
{
    int nSlot;
    for (nSlot = 0; nSlot < NUM_INVENTORY_SLOTS; nSlot++)
    {
        object oItem = GetItemInSlot(nSlot, oPC);
        if (GetIsObjectValid(oItem)) DestroyObject(oItem);
    }
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    Bare(oPC);
    SetCreatureBodyPart(CREATURE_PART_RIGHT_HAND, 186, oPC);
    DelayCommand(3.0, VCamera(oPC, 270.0, 1.6, 80.0));
    DelayCommand(6.0, VShot("front"));
    DelayCommand(7.0, VCamera(oPC, 0.0, 1.6, 80.0));
    DelayCommand(10.0, VShot("side"));
    DelayCommand(11.0, VDone());
}
