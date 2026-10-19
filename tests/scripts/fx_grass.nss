// World presentation test: taller, denser, yellower grass and a strong wind.
void main()
{
    object oArea = GetArea(OBJECT_SELF);
    SetAreaGrassOverride(oArea, 3, "grass", 8.0, 2.0, Vector(0.6, 0.5, 0.2), Vector(0.9, 0.8, 0.3));
    SetAreaWind(oArea, Vector(1.0, 1.0, 0.0), 2.0, 150.0, 5.0);
}
