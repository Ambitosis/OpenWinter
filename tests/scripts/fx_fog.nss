// World presentation test: thick red fog fading in over a second.
void main()
{
    object oArea = GetArea(OBJECT_SELF);
    SetFogColor(FOG_TYPE_ALL, 0xB02818, oArea, 1.0);
    SetFogAmount(FOG_TYPE_ALL, 25, oArea);
}
