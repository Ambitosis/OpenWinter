// Module water program: the scene's own fswater (unlit) on a river tile, switched by SetShaderUniformInt bank 1:
// the engine's noise texture, the view behind the water by its depth, the water's texture, and flat red; then the
// scene's post-process pass shows the depth buffer over the water (does the water write depth?).
#include "visual_inc"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    SetAreaDefaultGrassDisabled(GetArea(oPC), TRUE);
    CreateObject(OBJECT_TYPE_PLACEABLE, "plc_statue1", Location(GetArea(oPC), Vector(18.0, 15.0, 0.0), 0.0));
    DelayCommand(3.0, VCamera(oPC, 270.0, 14.0, 40.0));
    DelayCommand(5.0, VShot("a_noise"));
    DelayCommand(6.0, SetShaderUniformInt(oPC, SHADER_UNIFORM_1, 1));
    DelayCommand(8.0, VShot("b_behind"));
    DelayCommand(9.0, SetShaderUniformInt(oPC, SHADER_UNIFORM_1, 2));
    DelayCommand(11.0, VShot("c_texture"));
    DelayCommand(12.0, SetShaderUniformInt(oPC, SHADER_UNIFORM_1, 3));
    DelayCommand(14.0, VShot("d_which"));
    DelayCommand(15.0, VCamera(oPC, 270.0, 18.0, 75.0));
    DelayCommand(15.0, SetShaderUniformInt(oPC, SHADER_UNIFORM_1, 1));
    DelayCommand(18.0, VShot("e_behind_high"));
    // The depth buffer over the water (drawn with its texture), from high and low.
    DelayCommand(19.0, SetShaderUniformInt(oPC, SHADER_UNIFORM_1, 2));
    DelayCommand(19.0, SetShaderUniformInt(oPC, SHADER_UNIFORM_2, 1));
    DelayCommand(21.0, VShot("f_depth_high"));
    DelayCommand(22.0, VCamera(oPC, 270.0, 14.0, 40.0));
    DelayCommand(25.0, VShot("g_depth_low"));
    DelayCommand(27.0, VDone());
}
