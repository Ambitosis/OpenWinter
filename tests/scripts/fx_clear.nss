// World presentation test: clear weather, the area's own fog back, day lighting.
void main()
{
    object oArea = GetArea(OBJECT_SELF);
    SetWeather(oArea, WEATHER_CLEAR);
    SetFogAmount(FOG_TYPE_ALL, 0, oArea);
    SetFogColor(FOG_TYPE_SUN, 0x918768, oArea);
    SetFogColor(FOG_TYPE_MOON, 0x141429, oArea);
    SetSkyBox(SKYBOX_NONE, oArea);
    NightToDay(OBJECT_SELF, 0.0);
}
