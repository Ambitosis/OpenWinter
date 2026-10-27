// Module shaders on palette-textured (PLT) body parts: the player's bare torso and head drawn with the scene's own
// shader (ow_plt.shd, through pfh0_chest001.mtr, the elf torso's bitmap, and pfe0_head009.mtr). Each shot shows one
// thing the shader is handed: the tinted texture, its alpha, the vertex in its mesh's space, the mesh's bounding
// box, texture facts.
#include "visual_inc"

void Mode(object oPC, int nMode)
{
    SetMaterialShaderUniformInt(oPC, "pfh0_chest001", "owMode", nMode);
    SetMaterialShaderUniformInt(oPC, "pfe0_head009", "owMode", nMode);
}

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
    DelayCommand(3.0, VCamera(oPC, 270.0, 2.2, 82.0));
    DelayCommand(5.0, VShot("m0_skin0"));
    DelayCommand(6.0, SetColor(oPC, COLOR_CHANNEL_SKIN, 9));
    DelayCommand(8.0, VShot("m0_skin9"));
    DelayCommand(9.0, Mode(oPC, 1));
    DelayCommand(11.0, VShot("m1_alpha"));
    DelayCommand(12.0, Mode(oPC, 2));
    DelayCommand(14.0, VShot("m2_mesh"));
    DelayCommand(15.0, Mode(oPC, 3));
    DelayCommand(17.0, VShot("m3_bounds"));
    DelayCommand(18.0, Mode(oPC, 4));
    DelayCommand(20.0, VShot("m4_texture"));
    DelayCommand(21.0, ResetMaterialShaderUniforms(oPC, "pfh0_chest001", "owMode"));
    DelayCommand(21.0, ResetMaterialShaderUniforms(oPC, "pfe0_head009", "owMode"));
    DelayCommand(23.0, VShot("reset"));
    DelayCommand(24.0, VCamera(oPC, 0.0, 2.2, 82.0));
    DelayCommand(24.0, Mode(oPC, 2));
    DelayCommand(26.0, VShot("m2_side"));
    DelayCommand(27.0, Mode(oPC, 5));
    DelayCommand(29.0, VShot("m5_sun_side"));
    DelayCommand(30.0, VCamera(oPC, 270.0, 2.2, 82.0));
    DelayCommand(32.0, VShot("m5_sun"));
    DelayCommand(33.0, Mode(oPC, 6));
    DelayCommand(35.0, VShot("m6_suncolour"));
    DelayCommand(36.0, Mode(oPC, 7));
    DelayCommand(38.0, VShot("m7_ambient"));
    DelayCommand(40.0, VDone());
}
