// World presentation test: the stormy grassland sky with a low sun from the west.
void main()
{
    object oArea = GetArea(OBJECT_SELF);
    SetSkyBox(SKYBOX_GRASS_STORM, oArea);
    SetAreaLightDirection(AREA_LIGHT_DIRECTION_SUN, Vector(-1.0, 0.2, 0.35), oArea);
}
