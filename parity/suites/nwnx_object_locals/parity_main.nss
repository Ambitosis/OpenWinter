// NWNX_Object_GetLocalVariable order: which variable each index names after sets, overwrites, deletes and defaults.
// NWNX_Object_DeleteVarRegex: which variables a pattern removes.
#include "parity_inc"
#include "nwnx_object"

string Dump(object o)
{
    int n = NWNX_Object_GetLocalVariableCount(o);
    string s = IntToString(n) + ":";
    int i;
    for (i = 0; i < n; i++)
    {
        struct NWNX_Object_LocalVariable v = NWNX_Object_GetLocalVariable(o, i);
        s += " " + v.key + "/" + IntToString(v.type);
    }
    struct NWNX_Object_LocalVariable bad = NWNX_Object_GetLocalVariable(o, n);
    s += " |" + bad.key + "/" + IntToString(bad.type);
    return s;
}

// Readings whose order depends on the original's hash table (more than a handful of names) are kept uncompared.
void PRaw(string sKey, string s)
{
    WriteTimestampedLogEntry("RAW|" + sKey + "|" + s);
}

object Fresh()
{
    return CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", PCentre());
}

void main()
{
    object o = Fresh();
    PLog("empty", Dump(o));

    SetLocalInt(o, "a", 1); SetLocalInt(o, "b", 2); SetLocalInt(o, "c", 3);
    PLog("ints3", Dump(o));

    o = Fresh();
    SetLocalInt(o, "a", 1); SetLocalString(o, "b", "x"); SetLocalFloat(o, "c", 1.5);
    SetLocalObject(o, "d", GetModule()); SetLocalLocation(o, "e", PCentre()); SetLocalJson(o, "f", JsonInt(3));
    PLog("types", Dump(o));

    o = Fresh();
    SetLocalJson(o, "f", JsonInt(3)); SetLocalLocation(o, "e", PCentre()); SetLocalObject(o, "d", GetModule());
    SetLocalFloat(o, "c", 1.5); SetLocalString(o, "b", "x"); SetLocalInt(o, "a", 1);
    PLog("typesrev", Dump(o));

    o = Fresh();
    SetLocalInt(o, "a", 1); SetLocalInt(o, "b", 2); SetLocalInt(o, "c", 3); SetLocalInt(o, "a", 4);
    PLog("overwrite", Dump(o));

    o = Fresh();
    SetLocalInt(o, "a", 1); SetLocalInt(o, "b", 2); SetLocalInt(o, "c", 3); DeleteLocalInt(o, "b"); SetLocalInt(o, "d", 4);
    PLog("delete", Dump(o));

    o = Fresh();
    SetLocalInt(o, "a", 1); SetLocalInt(o, "b", 2); SetLocalInt(o, "b", 0); SetLocalInt(o, "z", 0); SetLocalString(o, "s", "");
    PLog("defaults", Dump(o));

    o = Fresh();
    SetLocalInt(o, "a", 1); SetLocalString(o, "a", "x"); SetLocalFloat(o, "a", 2.0);
    PLog("samename", Dump(o));

    o = Fresh();
    int i;
    for (i = 0; i < 12; i++) SetLocalInt(o, "v" + IntToString(i), i + 1);
    PRaw("twelve", Dump(o));

    o = Fresh();
    SetLocalInt(o, "zeta", 1); SetLocalInt(o, "alpha", 2); SetLocalInt(o, "Mid", 3); SetLocalInt(o, "beta", 4);
    PLog("names", Dump(o));

    o = Fresh();
    SetLocalInt(o, "a", 1); SetLocalInt(o, "b", 2); SetLocalInt(o, "c", 3); DeleteLocalInt(o, "a"); SetLocalInt(o, "a", 5);
    PLog("readd", Dump(o));

    o = Fresh();
    SetLocalInt(o, "a", 1); SetLocalString(o, "b", "x"); DeleteLocalInt(o, "a"); SetLocalString(o, "c", "y"); SetLocalInt(o, "a", 2);
    PLog("mixdel", Dump(o));


    o = Fresh();
    SetLocalString(o, "a", "x"); SetLocalInt(o, "a", 1);
    PLog("strfirst", Dump(o));

    o = Fresh();
    SetLocalInt(o, "a", 1); SetLocalString(o, "a", "x"); DeleteLocalInt(o, "a");
    PLog("deltype", Dump(o));

    o = Fresh();
    SetLocalInt(o, "a", 1); SetLocalInt(o, "b", 2); SetLocalInt(o, "c", 3); SetLocalInt(o, "b", 0); SetLocalInt(o, "b", 5);
    PLog("zeroback", Dump(o));

    o = Fresh();
    SetLocalInt(o, "a", 1); SetLocalString(o, "a", "x"); SetLocalInt(o, "a", 0);
    PLog("zeromix", Dump(o));

    o = Fresh();
    for (i = 0; i < 20; i++)
    {
        SetLocalInt(o, "v" + IntToString(i), i + 1);
        if (i < 4) PLog("grow" + IntToString(i + 1), Dump(o));
        else PRaw("grow" + IntToString(i + 1), Dump(o));
    }
    o = Fresh();
    for (i = 0; i < 14; i++) SetLocalInt(o, "Var_" + IntToString(i * 7), i + 1);
    PRaw("var7", Dump(o));

    PLogI("invalid", NWNX_Object_GetLocalVariableCount(OBJECT_INVALID));
    struct NWNX_Object_LocalVariable v = NWNX_Object_GetLocalVariable(OBJECT_INVALID, 0);
    PLog("invalid0", v.key + "/" + IntToString(v.type));

    // NWNX_Object_DeleteVarRegex only removes the plugin's own persistent variables.
    o = Fresh();
    SetLocalInt(o, "Test1", 1);
    NWNX_Object_SetInt(o, "Test2", 2, FALSE);
    NWNX_Object_SetString(o, "MyTest3", "x", TRUE);
    NWNX_Object_SetFloat(o, "Other", 1.5, FALSE);
    NWNX_Object_SetInt(o, "Test", 4, FALSE);
    NWNX_Object_DeleteVarRegex(o, ".*Test.*");
    PLogI("regex.local", GetLocalInt(o, "Test1"));
    PLogI("regex.pos", NWNX_Object_GetInt(o, "Test2"));
    PLog("regex.str", NWNX_Object_GetString(o, "MyTest3"));
    PLogF("regex.float", NWNX_Object_GetFloat(o, "Other"));
    NWNX_Object_SetInt(o, "Test5", 5, FALSE);
    NWNX_Object_SetInt(o, "ATest", 6, FALSE);
    NWNX_Object_DeleteVarRegex(o, "Test");
    PLogI("regex.partial", NWNX_Object_GetInt(o, "Test5") * 10 + NWNX_Object_GetInt(o, "ATest"));
    NWNX_Object_DeleteVarRegex(o, "Test[0-9]");
    PLogI("regex.class", NWNX_Object_GetInt(o, "Test5") * 10 + NWNX_Object_GetInt(o, "ATest"));
    NWNX_Object_SetInt(o, "Del", 7, FALSE);
    NWNX_Object_DeleteInt(o, "Del");
    PLogI("pos.deleted", NWNX_Object_GetInt(o, "Del"));
    NWNX_Object_SetString(o, "Same", "s", FALSE);
    NWNX_Object_SetInt(o, "Same", 8, FALSE);
    PLog("pos.same", NWNX_Object_GetString(o, "Same") + "/" + IntToString(NWNX_Object_GetInt(o, "Same")));
    PLog("regex.dump", Dump(o));
    PDone();
}
