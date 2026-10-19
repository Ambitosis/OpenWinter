// Module and area information: names, tags, sizes, tile lighting, area flags, fog, sky, lights and area lifecycle.
#include "parity_inc"

string V(vector v) { return FloatToString(v.x, 0, 9) + "," + FloatToString(v.y, 0, 9) + "," + FloatToString(v.z, 0, 9); }

void Area(string k, object a)
{
    PLogI(k + ".valid", GetIsObjectValid(a));
    PLog(k + ".tag", GetTag(a));
    PLog(k + ".name", GetName(a));
    PLog(k + ".resref", GetResRef(a));
    PLogI(k + ".height", GetAreaSize(AREA_HEIGHT, a));
    PLogI(k + ".width", GetAreaSize(AREA_WIDTH, a));
    PLogI(k + ".size.bad", GetAreaSize(7, a));
    PLogI(k + ".interior", GetIsAreaInterior(a));
    PLogI(k + ".natural", GetIsAreaNatural(a));
    PLogI(k + ".aboveground", GetIsAreaAboveGround(a));
    PLogI(k + ".weather", GetWeather(a));
    PLog(k + ".tileset", GetTilesetResRef(a));
    PLogI(k + ".skybox", GetSkyBox(a));
    PLogI(k + ".fog.sun.color", GetFogColor(FOG_TYPE_SUN, a));
    PLogI(k + ".fog.moon.color", GetFogColor(FOG_TYPE_MOON, a));
    PLogI(k + ".fog.all.color", GetFogColor(FOG_TYPE_ALL, a));
    PLogI(k + ".fog.sun.amount", GetFogAmount(FOG_TYPE_SUN, a));
    PLogI(k + ".fog.moon.amount", GetFogAmount(FOG_TYPE_MOON, a));
    int i;
    for (i = 0; i <= 4; i++) PLogI(k + ".light." + IntToString(i), GetAreaLightColor(i, a));
    PLog(k + ".lightdir.sun", V(GetAreaLightDirection(AREA_LIGHT_DIRECTION_SUN, a)));
    PLog(k + ".lightdir.moon", V(GetAreaLightDirection(AREA_LIGHT_DIRECTION_MOON, a)));
    PLogI(k + ".norest", GetAreaNoRestFlag(a));
    PLogI(k + ".type", GetObjectType(a));
}

void Tiles(object a)
{
    float x, y;
    for (x = -1.0; x <= 45.0; x += 9.5)
    {
        location l = Location(a, Vector(x, 5.0, 0.0), 0.0);
        string k = "tile." + FloatToString(x, 0, 1);
        PLogI(k + ".main1", GetTileMainLight1Color(l));
        PLogI(k + ".main2", GetTileMainLight2Color(l));
        PLogI(k + ".src1", GetTileSourceLight1Color(l));
        PLogI(k + ".src2", GetTileSourceLight2Color(l));
        PLogI(k + ".id", GetTileID(l));
        PLogI(k + ".orient", GetTileOrientation(l));
        PLogI(k + ".height", GetTileHeight(l));
    }
    location lx = Location(OBJECT_INVALID, Vector(5.0, 5.0, 0.0), 0.0);
    PLogI("tile.invalid.main1", GetTileMainLight1Color(lx));
    PLogI("tile.invalid.id", GetTileID(lx));
    SetTileMainLightColor(Location(a, Vector(5.0, 5.0, 0.0), 0.0), 3, 7);
    PLogI("tile.set.main1", GetTileMainLight1Color(Location(a, Vector(5.0, 5.0, 0.0), 0.0)));
    PLogI("tile.set.main2", GetTileMainLight2Color(Location(a, Vector(5.0, 5.0, 0.0), 0.0)));
    SetTileSourceLightColor(Location(a, Vector(5.0, 5.0, 0.0), 0.0), 2, 15);
    PLogI("tile.set.src1", GetTileSourceLight1Color(Location(a, Vector(5.0, 5.0, 0.0), 0.0)));
    PLogI("tile.set.src2", GetTileSourceLight2Color(Location(a, Vector(5.0, 5.0, 0.0), 0.0)));
    PLogI("tile.explored", GetTileExplored(OBJECT_INVALID, a, 0, 0));
}

void Module()
{
    object m = GetModule();
    PLog("mod.name", GetModuleName());
    PLog("mod.getname", GetName(m));
    PLog("mod.tag", GetTag(m));
    PLog("mod.resref", GetResRef(m));
    PLogI("mod.xpscale", GetModuleXPScale());
    PLogI("mod.type", GetObjectType(m));
    location s = GetStartingLocation();
    PLog("mod.start.area", GetTag(GetAreaFromLocation(s)));
    PLog("mod.start.pos", V(GetPositionFromLocation(s)));
    PLog("mod.start.facing", FloatToString(GetFacingFromLocation(s), 0, 3));
    SetModuleXPScale(55);
    PLogI("mod.xpscale.set", GetModuleXPScale());
    SetModuleXPScale(250);
    PLogI("mod.xpscale.big", GetModuleXPScale());
    SetModuleXPScale(-5);
    PLogI("mod.xpscale.neg", GetModuleXPScale());
    PLog("mod.area.invalid.tileset", "[" + GetTilesetResRef(OBJECT_INVALID) + "]");
    PLogI("mod.area.invalid.size", GetAreaSize(AREA_WIDTH, OBJECT_INVALID));
    PLogI("mod.area.module.size", GetAreaSize(AREA_WIDTH, m));
    PLogI("mod.area.module.interior", GetIsAreaInterior(m));
    PLogI("mod.area.module.natural", GetIsAreaNatural(m));
    PLogI("mod.area.module.weather", GetWeather(m));
    PLogI("mod.area.module.skybox", GetSkyBox(m));
    PLogI("mod.area.module.light", GetAreaLightColor(AREA_LIGHT_COLOR_SUN_AMBIENT, m));
}

void Lifecycle()
{
    object a = GetObjectByTag("PARITY_AREA");
    object b = CreateArea("parity", "NEWTAG", "New Name");
    Area("created", b);
    object c = CopyArea(a, "COPYTAG");
    Area("copied", c);
    object d = CreateArea("parity");
    PLog("created.default.tag", GetTag(d));
    PLog("created.default.name", GetName(d));
    PLogI("created.missing", GetIsObjectValid(CreateArea("nosucharea")));
    int n = 0;
    object x = GetFirstArea();
    while (GetIsObjectValid(x)) { n++; x = GetNextArea(); }
    PLogI("areas.after.create", n);
    PLogI("destroy.result", DestroyArea(d));
    PLogI("destroy.invalid", DestroyArea(OBJECT_INVALID));
    PLogI("destroy.notarea", DestroyArea(GetModule()));
    object w = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", Location(b, Vector(5.0, 5.0, 0.0), 0.0));
    PLogI("destroy.withobjects", DestroyArea(b));
    PLogI("destroyed.valid", GetIsObjectValid(d));
    n = 0;
    x = GetFirstArea();
    while (GetIsObjectValid(x)) { n++; x = GetNextArea(); }
    PLogI("areas.right.after.destroy", n);
    PLog("destroyed.tag", GetTag(d));
    // Setters on the source area.
    SetSkyBox(SKYBOX_GRASS_CLEAR, a);
    PLogI("set.skybox", GetSkyBox(a));
    SetFogColor(FOG_TYPE_SUN, FOG_COLOR_RED, a);
    PLogI("set.fog.sun", GetFogColor(FOG_TYPE_SUN, a));
    PLogI("set.fog.moon", GetFogColor(FOG_TYPE_MOON, a));
    SetFogAmount(FOG_TYPE_ALL, 50, a);
    PLogI("set.fogamount.sun", GetFogAmount(FOG_TYPE_SUN, a));
    PLogI("set.fogamount.moon", GetFogAmount(FOG_TYPE_MOON, a));
    SetWeather(a, WEATHER_RAIN);
    PLogI("set.weather", GetWeather(a));
    SetAreaLightColor(AREA_LIGHT_COLOR_SUN_AMBIENT, 0x112233, a);
    PLogI("set.light", GetAreaLightColor(AREA_LIGHT_COLOR_SUN_AMBIENT, a));
    SetAreaLightDirection(AREA_LIGHT_DIRECTION_SUN, Vector(1.0, 2.0, 3.0), a);
    PLog("set.lightdir", V(GetAreaLightDirection(AREA_LIGHT_DIRECTION_SUN, a)));
    PLog("set.lightdir.moon", V(GetAreaLightDirection(AREA_LIGHT_DIRECTION_MOON, a)));
    PLog("lightdir.badtype", V(GetAreaLightDirection(5, a)));
    PLogI("skybox.invalid", GetSkyBox(OBJECT_INVALID));
    SetAreaNoRestFlag(TRUE, a);
    PLogI("set.norest", GetAreaNoRestFlag(a));
}

void Later()
{
    int n = 0;
    object x = GetFirstArea();
    while (GetIsObjectValid(x)) { n++; x = GetNextArea(); }
    PLogI("areas.later", n);
    PLogI("destroyed.later.valid", GetIsObjectValid(GetObjectByTag("NEWTAG")));
}

void main()
{
    DelayCommand(1.0, Area("area", GetObjectByTag("PARITY_AREA")));
    DelayCommand(1.1, Tiles(GetObjectByTag("PARITY_AREA")));
    DelayCommand(1.2, Module());
    DelayCommand(1.3, Lifecycle());
    DelayCommand(2.0, Later());
    DelayCommand(2.5, PDone());
}
