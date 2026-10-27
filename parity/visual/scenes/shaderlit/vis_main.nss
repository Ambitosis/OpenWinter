// Module shaders and the engine's lighting: the ground drawn through the scene's shader (ow_lit.shd), with the
// surface colour, specularity and roughness the lighting reads overridden in turn by script uniforms.
// shaderlitplain is the same area without the shader.
#include "visual_inc"

void Set(object oPC, int nTint, int nSpec, float fSpec, int nRough, float fRough)
{
    SetShaderUniformInt(oPC, SHADER_UNIFORM_1, nTint);
    SetShaderUniformInt(oPC, SHADER_UNIFORM_2, nSpec);
    SetShaderUniformInt(oPC, SHADER_UNIFORM_3, nRough);
    SetShaderUniformFloat(oPC, SHADER_UNIFORM_1, fSpec);
    SetShaderUniformFloat(oPC, SHADER_UNIFORM_2, fRough);
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    SetAreaDefaultGrassDisabled(GetArea(oPC), TRUE);
    SetShaderUniformVec(oPC, SHADER_UNIFORM_1, 0.1, 0.35, 1.0, 1.0);
    DelayCommand(3.0, VCamera(oPC, 90.0, 12.0, 1.0));
    DelayCommand(5.0, VShot("std"));
    DelayCommand(6.0, Set(oPC, 1, 0, 0.0, 0, 0.0));
    DelayCommand(8.0, VShot("tint"));
    DelayCommand(9.0, Set(oPC, 1, 1, 0.0, 0, 0.0));
    DelayCommand(11.0, VShot("tint_nospec"));
    DelayCommand(12.0, Set(oPC, 0, 1, 0.0, 0, 0.0));
    DelayCommand(14.0, VShot("nospec"));
    DelayCommand(15.0, Set(oPC, 0, 1, 1.0, 0, 0.0));
    DelayCommand(17.0, VShot("spec1"));
    DelayCommand(18.0, Set(oPC, 0, 0, 0.0, 1, 0.2));
    DelayCommand(20.0, VShot("rough02"));
    DelayCommand(21.0, Set(oPC, 0, 0, 0.0, 1, 1.0));
    DelayCommand(23.0, VShot("rough1"));
    DelayCommand(24.0, Set(oPC, 0, 0, 0.0, 0, 0.0));
    DelayCommand(24.0, VCamera(oPC, 90.0, 12.0, 60.0));
    DelayCommand(26.0, VShot("std_side"));
    DelayCommand(27.0, Set(oPC, 0, 1, 0.0, 0, 0.0));
    DelayCommand(29.0, VShot("nospec_side"));
    DelayCommand(30.0, Set(oPC, 1, 0, 0.0, 0, 0.0));
    DelayCommand(32.0, VShot("tint_side"));
    DelayCommand(34.0, VDone());
}
