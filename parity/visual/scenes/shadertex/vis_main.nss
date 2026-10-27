// ReplaceObjectTexture on meshes drawn with a module shader. Four barrels (plc_h07: wood plc_woodb256, rock
// plc_rck, a black cap plc_black00), every bitmap named by one of the scene's MTRs (ow_texs.shd, each tinted):
//   1  plc_woodb256 replaced by ttr01_grass02 (a texture without an MTR)
//   2  plc_woodb256 replaced by plc_rck (a bitmap with its own MTR)
//   3  plc_rck replaced by ttr01_grass02 (also the texture0 that plc_black00.mtr names)
//   0  untouched
#include "visual_inc"

object Barrel(object oPC, float fDx, string sTag)
{
    vector v = GetPosition(oPC);
    return CreateObject(OBJECT_TYPE_PLACEABLE, "plc_barrel", Location(GetArea(oPC), Vector(v.x + fDx, v.y + 1.5, v.z), 270.0), FALSE, sTag);
}

void Apply(int bOn)
{
    ReplaceObjectTexture(GetObjectByTag("ow_b1"), "plc_woodb256", bOn ? "ttr01_grass02" : "");
    ReplaceObjectTexture(GetObjectByTag("ow_b2"), "plc_woodb256", bOn ? "plc_rck" : "");
    ReplaceObjectTexture(GetObjectByTag("ow_b3"), "plc_rck", bOn ? "ttr01_grass02" : "");
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    SetAreaDefaultGrassDisabled(GetArea(oPC), TRUE);
    Barrel(oPC, -1.8, "ow_b1");
    Barrel(oPC, -0.6, "ow_b2");
    Barrel(oPC, 0.6, "ow_b3");
    Barrel(oPC, 1.8, "ow_b0");
    DelayCommand(3.0, VCamera(oPC, 90.0, 5.0, 55.0));
    DelayCommand(5.0, VShot("before"));
    DelayCommand(6.0, Apply(TRUE));
    DelayCommand(8.0, VShot("replaced"));
    DelayCommand(9.0, Apply(FALSE));
    DelayCommand(11.0, VShot("restored"));
    DelayCommand(13.0, VDone());
}
