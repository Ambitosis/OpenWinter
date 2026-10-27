// Module shaders: the ground drawn with the scene's own shader (ow_bands.shd, through ttr01_grass02.mtr),
// changed by SetShaderUniformFloat / Int / Vec. Bank 1 of each kind is used for something different, so
// the float, int and vec banks must be kept apart.
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    SetAreaDefaultGrassDisabled(GetArea(oPC), TRUE);
    DelayCommand(3.0, VCamera(oPC, 90.0, 12.0, 1.0));
    DelayCommand(5.0, VShot("plain"));
    DelayCommand(6.0, SetShaderUniformVec(oPC, SHADER_UNIFORM_1, 1.0, 0.15, 0.1, 1.0));
    DelayCommand(6.0, SetShaderUniformFloat(oPC, SHADER_UNIFORM_1, 1.0));
    DelayCommand(8.0, VShot("bands"));
    DelayCommand(9.0, SetShaderUniformInt(oPC, SHADER_UNIFORM_1, 1));
    DelayCommand(9.0, SetShaderUniformVec(oPC, SHADER_UNIFORM_1, 0.1, 0.35, 1.0, 1.0));
    DelayCommand(11.0, VShot("checker"));
    DelayCommand(12.0, SetShaderUniformFloat(oPC, SHADER_UNIFORM_1, 0.5));
    DelayCommand(14.0, VShot("half"));
    DelayCommand(15.0, VCamera(oPC, 90.0, 12.0, 60.0));
    DelayCommand(17.0, VShot("side"));
    DelayCommand(19.0, VDone());
}
