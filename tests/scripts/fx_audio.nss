// World presentation test: other day music and ambience, then a bell at the caller.
void main()
{
    object oArea = GetArea(OBJECT_SELF);
    MusicBackgroundChangeDay(oArea, 4);
    MusicBackgroundChangeNight(oArea, 4);
    MusicBackgroundPlay(oArea);
    AmbientSoundChangeDay(oArea, 2);
    AmbientSoundSetDayVolume(oArea, 80);
    AmbientSoundPlay(oArea);
    PlaySound("as_cv_bell1");
}
