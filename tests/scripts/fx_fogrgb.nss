// Fog and area light colours are 0xRRGGBB to scripts; the area keeps them as 0xBBGGRR.
void main()
{
    object oArea = GetArea(OBJECT_SELF);
    SetFogColor(FOG_TYPE_SUN, 0x102030, oArea);
    SetAreaLightColor(AREA_LIGHT_COLOR_SUN_DIFFUSE, 0x405060, oArea);
    SetLocalInt(GetModule(), "fog_rgb", GetFogColor(FOG_TYPE_SUN, oArea));
    SetLocalInt(GetModule(), "light_rgb", GetAreaLightColor(AREA_LIGHT_COLOR_SUN_DIFFUSE, oArea));
}
