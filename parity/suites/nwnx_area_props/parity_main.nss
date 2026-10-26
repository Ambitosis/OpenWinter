// nwnx_area_props: NWNX_Area getters on an area with non-default ARE/GIT fields, the setters read back, and the
// engine readings they change (GetIsAreaInterior, GetAreaNoRestFlag ...).
#include "parity_inc"
#include "nwnx_area"

void Wind(string sKey, object oArea)
{
    struct NWNX_Area_AreaWind w = NWNX_Area_GetAreaWind(oArea);
    PLog(sKey, FloatToString(w.vDirection.x, 0, 3) + "," + FloatToString(w.vDirection.y, 0, 3) + "," + FloatToString(w.vDirection.z, 0, 3)
        + " m=" + FloatToString(w.fMagnitude, 0, 3) + " y=" + FloatToString(w.fYaw, 0, 3) + " p=" + FloatToString(w.fPitch, 0, 3));
}

void Props(string p, object a)
{
    PLogI(p + ".players", NWNX_Area_GetNumberOfPlayersInArea(a));
    PLogI(p + ".pvp", NWNX_Area_GetPVPSetting(a));
    PLogI(p + ".spot", NWNX_Area_GetAreaSpotModifier(a));
    PLogI(p + ".listen", NWNX_Area_GetAreaListenModifier(a));
    PLogI(p + ".norest", NWNX_Area_GetNoRestingAllowed(a));
    PLogI(p + ".windpower", NWNX_Area_GetWindPower(a));
    PLogI(p + ".rain", NWNX_Area_GetWeatherChance(a, NWNX_AREA_WEATHER_CHANCE_RAIN));
    PLogI(p + ".snow", NWNX_Area_GetWeatherChance(a, NWNX_AREA_WEATHER_CHANCE_SNOW));
    PLogI(p + ".lightning", NWNX_Area_GetWeatherChance(a, NWNX_AREA_WEATHER_CHANCE_LIGHTNING));
    PLogI(p + ".weather3", NWNX_Area_GetWeatherChance(a, 3));
    PLogF(p + ".fogclip", NWNX_Area_GetFogClipDistance(a));
    PLogI(p + ".shadow", NWNX_Area_GetShadowOpacity(a));
    PLogI(p + ".daynight", NWNX_Area_GetDayNightCycle(a));
    PLogI(p + ".moonamb", NWNX_Area_GetSunMoonColors(a, 0));
    PLogI(p + ".moondif", NWNX_Area_GetSunMoonColors(a, 1));
    PLogI(p + ".sunamb", NWNX_Area_GetSunMoonColors(a, 2));
    PLogI(p + ".sundif", NWNX_Area_GetSunMoonColors(a, 3));
    PLogI(p + ".color4", NWNX_Area_GetSunMoonColors(a, 4));
    PLogI(p + ".ambday", NWNX_Area_GetAmbientSoundDay(a));
    PLogI(p + ".ambnight", NWNX_Area_GetAmbientSoundNight(a));
    PLogI(p + ".ambdayvol", NWNX_Area_GetAmbientSoundDayVolume(a));
    PLogI(p + ".ambnightvol", NWNX_Area_GetAmbientSoundNightVolume(a));
    PLogI(p + ".music", NWNX_Area_GetMusicIsPlaying(a));
    PLogI(p + ".battlemusic", NWNX_Area_GetMusicIsPlaying(a, TRUE));
    PLogI(p + ".flags", NWNX_Area_GetAreaFlags(a));
    Wind(p + ".wind", a);
}

void Entries(string p, int bLeftB = TRUE)
{
    object a = GetObjectByTag("PARITY_AREA");
    object b = GetObjectByTag("PARITY2");
    PLogI(p + ".enter.count", GetLocalInt(a, "entered"));
    PLogI(p + ".exit.count", GetLocalInt(a, "left"));
    PLog(p + ".enter.last", GetTag(NWNX_Area_GetLastEntered(a)));
    PLog(p + ".exit.last", GetTag(NWNX_Area_GetLastLeft(a)));
    PLog(p + ".enter.last2", GetTag(NWNX_Area_GetLastEntered(b)));
    if (bLeftB) PLog(p + ".exit.last2", GetTag(NWNX_Area_GetLastLeft(b)));
}

void Later()
{
    Entries("t3", FALSE);
    object a = GetObjectByTag("PARITY_AREA");
    object b = GetObjectByTag("PARITY2");
    object g1 = GetObjectByTag("G1"), g2 = GetObjectByTag("G2");
    AssignCommand(g1, JumpToLocation(Location(a, Vector(5.0, 5.0, 0.0), 0.0)));
    AssignCommand(g2, JumpToLocation(Location(b, Vector(25.0, 5.0, 0.0), 0.0)));
    DelayCommand(2.0, Entries("t5"));
    DelayCommand(2.5, DestroyObject(g1));
    DelayCommand(4.0, Entries("t7"));
    DelayCommand(4.5, PDone());
}

void main()
{
    object a = GetObjectByTag("PARITY_AREA");
    object b = GetObjectByTag("PARITY2");
    Props("a", a);
    Props("b", b);
    Props("inv", OBJECT_INVALID);
    Props("mod", GetModule());
    PLog("enter.last0", GetTag(NWNX_Area_GetLastEntered(a)));
    // Before anything has left an area its GetLastLeft is some object of the module (another area, the module),
    // differing from module to module; it isn't compared.
    PLog("enter.last0b", GetTag(NWNX_Area_GetLastEntered(b)));

    // Setters, read back, and what the engine's own functions see.
    NWNX_Area_SetPVPSetting(a, 0);
    PLogI("set.pvp", NWNX_Area_GetPVPSetting(a));
    NWNX_Area_SetPVPSetting(a, 7);
    PLogI("set.pvp7", NWNX_Area_GetPVPSetting(a));
    NWNX_Area_SetPVPSetting(a, 2);
    NWNX_Area_SetPVPSetting(a, 4);
    PLogI("set.pvp4", NWNX_Area_GetPVPSetting(a));
    NWNX_Area_SetPVPSetting(a, -1);
    PLogI("set.pvpneg", NWNX_Area_GetPVPSetting(a));
    NWNX_Area_SetPVPSetting(a, 3);
    PLogI("set.pvp3", NWNX_Area_GetPVPSetting(a));
    NWNX_Area_SetAreaSpotModifier(a, 11);
    NWNX_Area_SetAreaListenModifier(a, -12);
    PLogI("set.spot", NWNX_Area_GetAreaSpotModifier(a));
    PLogI("set.listen", NWNX_Area_GetAreaListenModifier(a));
    NWNX_Area_SetNoRestingAllowed(a, FALSE);
    PLogI("set.norest", NWNX_Area_GetNoRestingAllowed(a));
    PLogI("set.norest.native", GetAreaNoRestFlag(a));
    NWNX_Area_SetNoRestingAllowed(a, 5);
    PLogI("set.norest5", NWNX_Area_GetNoRestingAllowed(a));
    SetAreaNoRestFlag(FALSE, a);
    PLogI("set.norest.fromnative", NWNX_Area_GetNoRestingAllowed(a));
    NWNX_Area_SetWindPower(a, 2);
    PLogI("set.windpower", NWNX_Area_GetWindPower(a));
    Wind("set.windpower.wind", a);
    NWNX_Area_SetWindPower(a, 5);
    PLogI("set.windpower5", NWNX_Area_GetWindPower(a));
    NWNX_Area_SetWindPower(a, -1);
    PLogI("set.windpowerneg", NWNX_Area_GetWindPower(a));
    SetAreaWind(a, Vector(1.0, 2.0, 3.0), 4.0, 5.0, 6.0);
    Wind("set.wind", a);
    PLogI("set.wind.power", NWNX_Area_GetWindPower(a));
    NWNX_Area_SetWindPower(a, 1);
    Wind("set.wind.afterpower", a);
    PLogI("set.wind.afterpower.power", NWNX_Area_GetWindPower(a));
    SetAreaWind(a, Vector(1.0, 2.0, 3.0), 4.0, 5.0, 6.0);
    Wind("b.wind.after", b);
    NWNX_Area_SetWeatherChance(a, NWNX_AREA_WEATHER_CHANCE_RAIN, 55);
    NWNX_Area_SetWeatherChance(a, NWNX_AREA_WEATHER_CHANCE_SNOW, 150);
    NWNX_Area_SetWeatherChance(a, NWNX_AREA_WEATHER_CHANCE_LIGHTNING, -4);
    PLogI("set.rain", NWNX_Area_GetWeatherChance(a, 0));
    PLogI("set.snow", NWNX_Area_GetWeatherChance(a, 1));
    PLogI("set.lightning", NWNX_Area_GetWeatherChance(a, 2));
    NWNX_Area_SetWeatherChance(a, 1, 100);
    NWNX_Area_SetWeatherChance(a, 2, 0);
    NWNX_Area_SetWeatherChance(a, 3, 50);
    PLogI("set.snow100", NWNX_Area_GetWeatherChance(a, 1));
    PLogI("set.lightning0", NWNX_Area_GetWeatherChance(a, 2));
    PLogI("set.weather3", NWNX_Area_GetWeatherChance(a, 3));
    NWNX_Area_SetFogClipDistance(a, 33.25);
    PLogF("set.fogclip", NWNX_Area_GetFogClipDistance(a));
    NWNX_Area_SetFogClipDistance(a, -3.0);
    PLogF("set.fogclipneg", NWNX_Area_GetFogClipDistance(a));
    NWNX_Area_SetFogClipDistance(a, 0.0);
    PLogF("set.fogclip0", NWNX_Area_GetFogClipDistance(a));
    NWNX_Area_SetFogClipDistance(a, 33.25);
    NWNX_Area_SetShadowOpacity(a, 77);
    PLogI("set.shadow", NWNX_Area_GetShadowOpacity(a));
    NWNX_Area_SetShadowOpacity(a, 140);
    PLogI("set.shadow140", NWNX_Area_GetShadowOpacity(a));
    NWNX_Area_SetShadowOpacity(a, -5);
    PLogI("set.shadowneg", NWNX_Area_GetShadowOpacity(a));
    NWNX_Area_SetShadowOpacity(a, 100);
    PLogI("set.shadow100", NWNX_Area_GetShadowOpacity(a));
    NWNX_Area_SetShadowOpacity(a, 0);
    PLogI("set.shadow0", NWNX_Area_GetShadowOpacity(a));
    NWNX_Area_SetShadowOpacity(a, 77);
    NWNX_Area_SetDayNightCycle(a, NWNX_AREA_DAYNIGHTCYCLE_ALWAYS_DARK);
    PLogI("set.daynight", NWNX_Area_GetDayNightCycle(a));
    NWNX_Area_SetDayNightCycle(a, 9);
    PLogI("set.daynight9", NWNX_Area_GetDayNightCycle(a));
    NWNX_Area_SetSunMoonColors(a, 2, 0x112233);
    NWNX_Area_SetSunMoonColors(a, 5, 0x445566);
    PLogI("set.sunamb", NWNX_Area_GetSunMoonColors(a, 2));
    PLogI("set.color5", NWNX_Area_GetSunMoonColors(a, 5));
    NWNX_Area_SetSunMoonColors(a, 3, -5);
    PLogI("set.sundifneg", NWNX_Area_GetSunMoonColors(a, 3));
    NWNX_Area_SetSunMoonColors(a, 3, 0x1FFFFFF);
    PLogI("set.sundifbig", NWNX_Area_GetSunMoonColors(a, 3));
    NWNX_Area_SetSunMoonColors(a, 3, 0x1000001);
    PLogI("set.sundifover", NWNX_Area_GetSunMoonColors(a, 3));
    NWNX_Area_SetSunMoonColors(a, -1, 7);
    PLogI("set.colorneg", NWNX_Area_GetSunMoonColors(a, -1));
    NWNX_Area_SetAreaFlags(a, 3);
    PLogI("set.flags", NWNX_Area_GetAreaFlags(a));
    PLogI("set.flags.interior", GetIsAreaInterior(a));
    PLogI("set.flags.natural", GetIsAreaNatural(a));
    PLogI("set.flags.above", GetIsAreaAboveGround(a));
    MusicBackgroundStop(a);
    PLogI("set.music.stopped", NWNX_Area_GetMusicIsPlaying(a));
    MusicBattlePlay(a);
    PLogI("set.battle.playing", NWNX_Area_GetMusicIsPlaying(a, TRUE));
    AmbientSoundChangeDay(a, 12);
    AmbientSoundSetDayVolume(a, 20);
    PLogI("set.ambday", NWNX_Area_GetAmbientSoundDay(a));
    PLogI("set.ambdayvol", NWNX_Area_GetAmbientSoundDayVolume(a));
    PLogI("set.ambnight", NWNX_Area_GetAmbientSoundNight(a));
    Props("after", a);

    // Area entry and exit bookkeeping for creatures (there are no players).
    object g1 = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(), FALSE, "G1");
    object g2 = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(2.0), FALSE, "G2");
    PLog("enter.last.now", GetTag(NWNX_Area_GetLastEntered(a)));
    AssignCommand(g1, JumpToLocation(Location(b, Vector(5.0, 5.0, 0.0), 0.0)));
    DelayCommand(3.0, Later());
}
