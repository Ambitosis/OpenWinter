// Script-visible side of music, ambient sound and sound objects: the music getters before and after
// the setters, what they accept, and whether placed sound objects are objects to scripts at all.
#include "parity_inc"

string TypeOf(object o) { return IntToString(GetObjectType(o)); }

void Tracks(string sKey, object oArea)
{
    PLog(sKey, IntToString(MusicBackgroundGetDayTrack(oArea)) + "," + IntToString(MusicBackgroundGetNightTrack(oArea))
        + "," + IntToString(MusicBackgroundGetBattleTrack(oArea)));
}

// Tags of every object an area iteration returns whose tag starts with sPrefix.
string AreaList(object oArea, int nFilter, string sPrefix)
{
    string s = "";
    object o = GetFirstObjectInArea(oArea, nFilter);
    while (GetIsObjectValid(o))
    {
        if (GetStringLeft(GetTag(o), 2) == sPrefix) s += GetTag(o) + ":" + TypeOf(o) + " ";
        o = GetNextObjectInArea(oArea, nFilter);
    }
    return s;
}

string ShapeList(location l, float fSize, int nFilter)
{
    string s = "";
    object o = GetFirstObjectInShape(SHAPE_SPHERE, fSize, l, FALSE, nFilter);
    while (GetIsObjectValid(o))
    {
        s += GetTag(o) + " ";
        o = GetNextObjectInShape(SHAPE_SPHERE, fSize, l, FALSE, nFilter);
    }
    return s;
}

void Later(object oSnd, object oOff)
{
    PLogI("destroyed.valid", GetIsObjectValid(oSnd));
    PLogI("destroyed.bytag", GetIsObjectValid(GetObjectByTag("A_SND")));
    PLogI("ran.assign", GetLocalInt(GetModule(), "ASSIGNED"));
    PLogI("ran.delay", GetLocalInt(GetModule(), "DELAYED"));
    PLogI("ran.exec", GetLocalInt(GetModule(), "EXEC_SELF") == 1);
    PDone();
}

void Mark(string sVar) { SetLocalInt(GetModule(), sVar, 1); }

void main()
{
    object oArea = GetObjectByTag("PARITY_AREA");
    object oMod = GetModule();

    // The getters read the area's own settings.
    Tracks("tracks.area", oArea);
    Tracks("tracks.invalid", OBJECT_INVALID);
    Tracks("tracks.module", oMod);

    object oSnd = GetObjectByTag("A_SND");
    object oOff = GetObjectByTag("A_SND_OFF");
    Tracks("tracks.sound", oSnd);

    // The setters and what they accept.
    MusicBackgroundChangeDay(oArea, 10);
    MusicBackgroundChangeNight(oArea, 11);
    MusicBattleChange(oArea, 40);
    Tracks("tracks.changed", oArea);
    MusicBackgroundChangeDay(oArea, -3);
    MusicBackgroundChangeNight(oArea, 0);
    MusicBattleChange(oArea, 9999);
    Tracks("tracks.odd", oArea);
    MusicBackgroundChangeDay(oSnd, 12);
    MusicBackgroundChangeDay(OBJECT_INVALID, 13);
    Tracks("tracks.wrongtarget", oArea);
    MusicBackgroundStop(oArea);
    MusicBattlePlay(oArea);
    MusicBackgroundSetDelay(oArea, 1000);
    AmbientSoundStop(oArea);
    AmbientSoundChangeDay(oArea, 7);
    AmbientSoundSetDayVolume(oArea, 300);
    Tracks("tracks.afterplay", oArea);
    MusicBattleStop(oArea);
    MusicBackgroundPlay(oArea);
    AmbientSoundPlay(oArea);

    // Placed sound objects as objects.
    PLogI("sound.valid", GetIsObjectValid(oSnd));
    PLogI("sound.validoff", GetIsObjectValid(oOff));
    PLog("sound.type", TypeOf(oSnd));
    PLog("sound.tag", GetTag(oSnd));
    PLog("sound.name", GetName(oSnd));
    PLog("sound.resref", GetResRef(oSnd));
    PLogI("sound.area", GetArea(oSnd) == oArea);
    vector v = GetPosition(oSnd);
    PLog("sound.pos", FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2) + "," + FloatToString(v.z, 0, 2));
    PLogF("sound.facing", GetFacing(oSnd));
    PLogI("sound.ispc", GetIsPC(oSnd));
    PLogI("sound.plot", GetPlotFlag(oSnd));
    PLogI("sound.hp", GetCurrentHitPoints(oSnd));
    PLogI("sound.commandable", GetCommandable(oSnd));
    SetLocalInt(oSnd, "X", 42);
    PLogI("sound.local", GetLocalInt(oSnd, "X"));
    PLog("sound.all", AreaList(oArea, OBJECT_TYPE_ALL, "A_"));
    PLog("sound.iterate", AreaList(oArea, 32767, "A_"));
    PLog("sound.shape", ShapeList(Location(oArea, Vector(20.0, 20.0, 0.0), 0.0), 10.0, OBJECT_TYPE_ALL));
    PLog("sound.shape.creature", ShapeList(Location(oArea, Vector(20.0, 20.0, 0.0), 0.0), 10.0, OBJECT_TYPE_CREATURE));
    PLog("sound.shape.placeable", ShapeList(Location(oArea, Vector(20.0, 20.0, 0.0), 0.0), 10.0, OBJECT_TYPE_PLACEABLE));
    PLog("sound.shape.waypoint", ShapeList(Location(oArea, Vector(20.0, 20.0, 0.0), 0.0), 10.0, OBJECT_TYPE_WAYPOINT));
    object oWp = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", Location(oArea, Vector(30.0, 30.0, 0.0), 0.0), FALSE, "A_WP");
    PLog("sound.shape.withwp", ShapeList(Location(oArea, Vector(20.0, 20.0, 0.0), 0.0), 20.0, OBJECT_TYPE_ALL));
    PLogI("sound.nearestwp", GetNearestObject(OBJECT_TYPE_WAYPOINT, oSnd) == oWp);
    PLogI("sound.nearestplc", GetIsObjectValid(GetNearestObject(OBJECT_TYPE_PLACEABLE, oSnd)));
    PLogI("sound.nearestcre", GetIsObjectValid(GetNearestObject(OBJECT_TYPE_CREATURE, oSnd)));
    PLogI("sound.nearestfromwp", GetNearestObject(OBJECT_TYPE_ALL, oWp) == oSnd || GetNearestObject(OBJECT_TYPE_ALL, oWp) == oOff);
    PLogI("sound.nearest2", GetNearestObject(OBJECT_TYPE_ALL, oSnd, 2) == oWp);
    PLogI("sound.nearestloc", GetNearestObjectToLocation(OBJECT_TYPE_ALL, Location(oArea, Vector(22.5, 20.0, 0.0), 0.0)) == oSnd);
    PLogI("sound.nearestlocwp", GetNearestObjectToLocation(OBJECT_TYPE_WAYPOINT, Location(oArea, Vector(22.5, 20.0, 0.0), 0.0)) == oWp);
    PLogI("sound.location", GetAreaFromLocation(GetLocation(oSnd)) == oArea);
    PLogI("sound.dead", GetIsDead(oSnd));
    PLogI("sound.maxhp", GetMaxHitPoints(oSnd));
    object oNear = GetNearestObjectByTag("A_SND_OFF", oSnd);
    PLogI("sound.nearestbytag", oNear == oOff);
    PLogF("sound.distance", GetDistanceBetween(oSnd, oOff), 0.01);
    PLogI("sound.nearestall", GetNearestObject(OBJECT_TYPE_ALL, oSnd) == oOff);

    // SoundObjectSetPosition moves the object; volume and play/stop leave it as it was.
    SoundObjectSetPosition(oSnd, Vector(25.0, 26.0, 1.0));
    v = GetPosition(oSnd);
    PLog("sound.moved", FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2) + "," + FloatToString(v.z, 0, 2));
    SoundObjectSetVolume(oSnd, 20);
    SoundObjectStop(oSnd);
    SoundObjectPlay(oOff);
    PLogI("sound.validafter", GetIsObjectValid(oSnd));

    // Commands and scripts on a sound object.
    AssignCommand(oSnd, Mark("ASSIGNED"));
    AssignCommand(oSnd, DelayCommand(0.5, Mark("DELAYED")));
    SetLocalInt(oMod, "EXEC_SELF", 0);
    ExecuteScript("a_exec", oSnd);

    DestroyObject(oSnd);
    DelayCommand(2.0, Later(oSnd, oOff));
}
