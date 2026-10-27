// A module material for a part whose mesh bitmap names no texture: hand.mtr, named after the bitmap of the female
// elf's right hand 186 ("hand", which no texture is called), draws it with the test shader of scene shaderplt. Each shot shows one thing the shader is handed:
// texture 0 as sampled, its alpha, and whether it is bound (red) with its size (green, blue).
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

void Mode(object oPC, int nMode)
{
    SetMaterialShaderUniformInt(oPC, "hand", "owMode", nMode);
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    Bare(oPC);
    SetCreatureBodyPart(CREATURE_PART_RIGHT_HAND, 186, oPC);
    DelayCommand(3.0, VCamera(oPC, 270.0, 1.6, 80.0));
    DelayCommand(6.0, VShot("m0_texture"));
    DelayCommand(7.0, Mode(oPC, 1));
    DelayCommand(9.0, VShot("m1_alpha"));
    DelayCommand(10.0, Mode(oPC, 4));
    DelayCommand(12.0, VShot("m4_bound"));
    DelayCommand(13.0, VDone());
}
