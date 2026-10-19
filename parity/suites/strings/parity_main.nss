// Strings: substrings, searching, case, conversions and FloatToString formatting.
#include "parity_inc"

// Values are bracketed so leading and trailing spaces survive the log.
void S(string k, string v) { PLog(k, "[" + v + "]"); }

void Sub()
{
    string s = "abcdef";
    S("sub.2.3", GetSubString(s, 2, 3));
    S("sub.0.6", GetSubString(s, 0, 6));
    S("sub.0.99", GetSubString(s, 0, 99));
    S("sub.4.10", GetSubString(s, 4, 10));
    S("sub.5.1", GetSubString(s, 5, 1));
    S("sub.6.1", GetSubString(s, 6, 1));
    S("sub.7.1", GetSubString(s, 7, 1));
    S("sub.2.0", GetSubString(s, 2, 0));
    S("sub.2.-1", GetSubString(s, 2, -1));
    S("sub.-1.3", GetSubString(s, -1, 3));
    S("sub.-2.4", GetSubString(s, -2, 4));
    S("sub.-10.3", GetSubString(s, -10, 3));
    S("sub.-10.20", GetSubString(s, -10, 20));
    S("sub.empty", GetSubString("", 0, 1));
    S("sub.big", GetSubString(s, 1, 2147483647));
    S("sub.minint", GetSubString(s, -2147483647 - 1, 3));

    int n;
    for (n = -2; n <= 8; n++)
    {
        S("left." + IntToString(n), GetStringLeft(s, n));
        S("right." + IntToString(n), GetStringRight(s, n));
        S("insert." + IntToString(n), InsertString(s, "XY", n));
    }
    S("left.empty", GetStringLeft("", 3));
    S("right.empty", GetStringRight("", 3));
    S("insert.empty", InsertString("", "XY", 0));
    S("insert.empty1", InsertString("", "XY", 1));
    S("insert.nothing", InsertString(s, "", 2));

    string h = "abcabc";
    PLogI("find.c", FindSubString(h, "c"));
    PLogI("find.bc", FindSubString(h, "bc"));
    PLogI("find.none", FindSubString(h, "x"));
    PLogI("find.case", FindSubString(h, "B"));
    PLogI("find.long", FindSubString("ab", "abc"));
    for (n = -2; n <= 8; n++)
    {
        PLogI("find.c." + IntToString(n), FindSubString(h, "c", n));
        PLogI("find.empty." + IntToString(n), FindSubString(h, "", n));
    }
    PLogI("find.emptyboth", FindSubString("", ""));
    PLogI("find.emptyhay", FindSubString("", "a"));
    PLogI("find.self", FindSubString(h, h));

    PLogI("len.empty", GetStringLength(""));
    PLogI("len.abc", GetStringLength("abc"));
    S("upper", GetStringUpperCase("Hello, World! 123 _x_ [z] {a} `q` @A"));
    S("lower", GetStringLowerCase("Hello, World! 123 _X_ [Z] {A} `Q` @a"));
    S("upper.empty", GetStringUpperCase(""));
    S("escape.quote", "a\"b");
    PLogI("escape.len", GetStringLength("a\\b\"c\nd"));
}

string Item(string l, int n)
{
    int start = 0, e;
    while (n > 0)
    {
        e = FindSubString(l, ",", start);
        if (e < 0) return "<end>";
        start = e + 1;
        n--;
    }
    e = FindSubString(l, ",", start);
    if (e < 0) e = GetStringLength(l);
    return GetSubString(l, start, e - start);
}

void Conv()
{
    string a = ",0,-0,+5,-5, 12,  -7  ,12abc,abc,007,3.9,-3.9,--5,+-5,- 5,2147483647,2147483648,-2147483648,-2147483649,99999999999,-99999999999,0x1F,1e3, ,+,-,1 2,4294967296,4294967297,-4294967297";
    int n;
    string t;
    for (n = 0; (t = Item(a, n)) != "<end>"; n++)
    {
        PLogI("stoi.[" + t + "]", StringToInt(t));
        PLog("stof.[" + t + "]", FloatToString(StringToFloat(t), 0, 6));
    }

    string f = "1.5, 1.5,1e3,1.5e-3,1E2,-0,.5,5.,-.5,abc,1.2.3,inf,-inf,nan,INF,infinity,1e40,-1e40,1e-50,0x10,  +2.5x,3.40282347e38,1e-45,e5,.e1,+.5,1e,1e+,0.1,123456789,16777217,1.5f,0x1p3";
    for (n = 0; (t = Item(f, n)) != "<end>"; n++)
    {
        PLog("stof2.[" + t + "]", FloatToString(StringToFloat(t)));
        PLog("stof2.6.[" + t + "]", FloatToString(StringToFloat(t), 0, 6));
    }

    PLog("fmt.negnan", "[" + FloatToString(-StringToFloat("nan")) + "]");
    PLog("fmt.negnan.0.2", "[" + FloatToString(-StringToFloat("nan"), 0, 2) + "]");
    PLog("fmt.stof.negnan", "[" + FloatToString(StringToFloat("-nan"), 0, 2) + "]");
    PLog("fmt.stof.nanparen", "[" + FloatToString(StringToFloat("nan(123)"), 0, 2) + "]");
    PLog("its.0", IntToString(0));
    PLog("its.-1", IntToString(-1));
    PLog("its.max", IntToString(2147483647));
    PLog("its.min", IntToString(-2147483647 - 1));
    PLog("hex.0", IntToHexString(0));
    PLog("hex.-1", IntToHexString(-1));
    PLog("hex.min", IntToHexString(-2147483647 - 1));
    PLog("hex.255", IntToHexString(255));
    PLogI("ftoi.-2.7", FloatToInt(-2.7));
    PLogI("ftoi.2.999", FloatToInt(2.999));
    PLogI("ftoi.1e10", FloatToInt(10000000000.0));
    PLogI("ftoi.-1e10", FloatToInt(-10000000000.0));
    PLogI("ftoi.2^31", FloatToInt(2147483648.0));
    PLogI("ftoi.-2^31", FloatToInt(-2147483648.0));
    PLogI("ftoi.nan", FloatToInt(StringToFloat("nan")));
    PLogI("ftoi.inf", FloatToInt(StringToFloat("1e40")));
    PLog("itof.max", FloatToString(IntToFloat(2147483647), 0, 1));
    PLog("itof.16777217", FloatToString(IntToFloat(16777217), 0, 1));
}

void Fmt(string k, float f)
{
    PLog("fmt." + k + ".default", "[" + FloatToString(f) + "]");
    int w, d;
    for (w = -5; w <= 30; w += 5)
        for (d = -1; d <= 12; d++)
            if (d <= 3 || d == 6 || d == 9 || d == 10 || d == 12)
                PLog("fmt." + k + "." + IntToString(w) + "." + IntToString(d), "[" + FloatToString(f, w, d) + "]");
    PLog("fmt." + k + ".50.2", "[" + FloatToString(f, 50, 2) + "]");
    PLog("fmt." + k + ".100.2", "[" + FloatToString(f, 100, 2) + "]");
    PLog("fmt." + k + ".0.20", "[" + FloatToString(f, 0, 20) + "]");
    PLog("fmt." + k + ".0.40", "[" + FloatToString(f, 0, 40) + "]");
    PLog("fmt." + k + ".200.0", "[" + FloatToString(f, 200, 0) + "]");
}

void Limits()
{
    string s = "x";
    int i;
    for (i = 0; i < 24; i++)
    {
        s = s + s;
        PLogI("limit.concat." + IntToString(i), GetStringLength(s));
    }
    SetLocalString(GetModule(), "big", s);
    PLogI("limit.local", GetStringLength(GetLocalString(GetModule(), "big")));
    PLogI("limit.sub", GetStringLength(GetSubString(s, 1000, 5000000)));
    PLogI("limit.find", FindSubString(s + "y", "y"));
    PLogI("limit.upper", GetStringLength(GetStringUpperCase(GetStringLeft(s, 100000))));
    PLogI("limit.stoi", StringToInt(GetStringLeft("12345678901234567890", 20)));
    object w = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", PCentre());
    PLogI("limit.wp", GetIsObjectValid(w));
    SetName(w, GetStringLeft(s, 70000));
    PLogI("limit.name", GetStringLength(GetName(w)));
    SetTag(w, GetStringLeft(s, 70000));
    PLogI("limit.tag", GetStringLength(GetTag(w)));
    SetTag(w, GetStringLeft(s, 64));
    PLogI("limit.tag64", GetStringLength(GetTag(w)));
    SetName(w, "abc");
    PLog("limit.wpname", GetName(w));
    object p = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre());
    SetName(p, GetStringLeft(s, 70000));
    PLogI("limit.plcname", GetStringLength(GetName(p)));
    SetName(p, "");
    PLog("limit.plcname.reset", GetName(p));
    object c = CreateObject(OBJECT_TYPE_CREATURE, "nw_dog", PCentre());
    SetName(c, GetStringLeft(s, 70000));
    PLogI("limit.crename", GetStringLength(GetName(c)));
    SetName(GetModule(), "modname");
    PLog("limit.modname", GetName(GetModule()));
    SetTag(GetModule(), "modtag");
    PLog("limit.modtag", GetTag(GetModule()));
    object a = GetArea(w);
    SetName(a, "areaname");
    PLog("limit.areaname", GetName(a));
    SetTag(a, "areatag");
    PLog("limit.areatag", GetTag(a));
    SetTag(a, "PARITY_AREA");
}

void Done() { PDone(); }

void main()
{
    DelayCommand(1.0, Sub());
    DelayCommand(1.1, Conv());
    DelayCommand(1.2, ExecuteScript("str_case"));
    DelayCommand(1.3, Fmt("zero", 0.0));
    DelayCommand(1.3, Fmt("negzero", -0.0));
    DelayCommand(1.3, Fmt("one", 1.0));
    DelayCommand(1.3, Fmt("mone", -1.0));
    DelayCommand(1.4, Fmt("pi", 3.14159265));
    DelayCommand(1.4, Fmt("mpi", -3.14159265));
    DelayCommand(1.4, Fmt("half", 0.5));
    DelayCommand(1.4, Fmt("1.5", 1.5));
    DelayCommand(1.5, Fmt("2.5", 2.5));
    DelayCommand(1.5, Fmt("m2.5", -2.5));
    DelayCommand(1.5, Fmt("micro", 0.000001));
    DelayCommand(1.5, Fmt("tiny", 0.0000000001));
    DelayCommand(1.6, Fmt("mtiny", -0.0000000001));
    DelayCommand(1.6, Fmt("big", 123456789.0));
    DelayCommand(1.6, Fmt("huge", 100000000000000000000.0));
    DelayCommand(1.6, Fmt("mhuge", -100000000000000000000.0));
    DelayCommand(1.7, Fmt("max", 340282346638528859811704183484516925440.0));
    DelayCommand(1.7, Fmt("0.1", 0.1));
    DelayCommand(1.7, Fmt("2.675", 2.675));
    DelayCommand(1.7, Fmt("1.005", 1.005));
    DelayCommand(1.8, Fmt("0.05", 0.05));
    DelayCommand(1.8, Fmt("0.95", 0.95));
    DelayCommand(1.8, Fmt("9.995", 9.995));
    DelayCommand(1.8, Fmt("inf", StringToFloat("1e40")));
    DelayCommand(1.8, Fmt("nan", StringToFloat("nan")));
    DelayCommand(2.0, Limits());
    DelayCommand(3.0, Done());
}
