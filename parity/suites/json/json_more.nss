// More json edge cases: number formatting, escapes, pretty printing, error texts, ranges and in-place failures.
#include "parity_inc"

// Newlines in a dump, made visible.
string NL(string s)
{
    string r = "";
    int i, n = GetStringLength(s);
    for (i = 0; i < n; i++)
    {
        string ch = GetSubString(s, i, 1);
        r += ch == "\n" ? "\\n" : ch;
    }
    return r;
}

void J(string k, json j) { PLog(k, NL(JsonDump(j))); }
void JT(string k, json j) { PLog(k, IntToString(JsonGetType(j)) + " " + NL(JsonDump(j)) + " err=[" + JsonGetError(j) + "]"); }

void Numbers()
{
    string s = "1.0,-1.5e-7,123456789012,1e400,-1e400,0.30000000000000004,100,1E2,-0.0,0.5,1e15,1e16,1e17,123456789012345678,0.0001,0.00001,-9223372036854775808,-9223372036854775809,18446744073709551615,18446744073709551616,1.5E+3,2e-3";
    int n = 0, e;
    while (n < GetStringLength(s))
    {
        e = FindSubString(s, ",", n);
        if (e < 0) e = GetStringLength(s);
        string t = GetSubString(s, n, e - n);
        json j = JsonParse(t);
        JT("num." + t, j);
        PLogI("num." + t + ".int", JsonGetInt(j));
        PLog("num." + t + ".float", FloatToString(JsonGetFloat(j), 0, 3));
        n = e + 1;
    }
    J("int.min", JsonInt(-2147483647 - 1));
    J("float.third", JsonFloat(1.0 / 3.0));
    J("float.max", JsonFloat(340000000000000000000000000000000000000.0));
    J("float.12345", JsonFloat(12345.0));
    J("float.1e15", JsonFloat(1000000000000000.0));
    J("float.1e16", JsonFloat(10000000000000000.0));
    J("float.small", JsonFloat(0.001));
    J("float.smaller", JsonFloat(0.0001));
}

void Strings()
{
    J("str.ctrl", JsonParse("\"a\\u0001b\\u001f\\u007f\""));
    J("str.escapes", JsonParse("\"\\\"\\\\\\/\\b\\f\\n\\r\\t\""));
    J("str.unicode", JsonParse("\"\\u20ac\\u4e2d\\ud83d\\ude00\""));
    PLog("str.euro.get", JsonGetString(JsonParse("\"\\u20ac\"")));
    J("str.make", JsonString("tab\there"));
    JT("str.badescape", JsonParse("\"\\x\""));
    JT("str.badu", JsonParse("\"\\u12\""));
    JT("str.unterminated", JsonParse("\"abc"));
    JT("str.newline", JsonParse("\"a\nb\""));
    JT("bad.true", JsonParse("tru"));
    JT("bad.minus", JsonParse("-"));
    JT("bad.leadingzero", JsonParse("01"));
    JT("bad.dot", JsonParse("1."));
    JT("bad.exp", JsonParse("1e"));
    JT("bad.colon", JsonParse("{\"a\" 1}"));
    JT("bad.objcomma", JsonParse("{\"a\":1 \"b\":2}"));
    JT("bad.arrcomma", JsonParse("[1 2]"));
    JT("bad.unclosed", JsonParse("[1,2"));
    JT("bad.unclosedobj", JsonParse("{\"a\":1"));
    JT("bad.multiline", JsonParse("[1,\n2,\n x]"));
    JT("bad.ws", JsonParse("  [1, x]"));
    JT("bad.key", JsonParse("{1:2}"));
    JT("bad.close", JsonParse("]"));
}

void Pretty()
{
    json j = JsonParse("{\"b\":[1,{\"c\":[]}],\"a\":{},\"d\":\"x\"}");
    PLog("pretty.-1", NL(JsonDump(j, -1)));
    PLog("pretty.0", NL(JsonDump(j, 0)));
    PLog("pretty.2", NL(JsonDump(j, 2)));
    PLog("pretty.4.array", NL(JsonDump(JsonParse("[1,[2]]"), 4)));
    PLog("pretty.scalar", NL(JsonDump(JsonInt(1), 2)));
    PLog("pretty.-5", NL(JsonDump(j, -5)));
}

void Edges()
{
    json a = JsonParse("[10,20,30]");
    JT("arr.insert.3", JsonArrayInsert(a, JsonInt(7), 3));
    JT("arr.insert.-1", JsonArrayInsert(a, JsonInt(7), -1));
    JT("obj.del.notobject", JsonObjectDel(JsonArray(), "a"));
    JT("arr.del.notarray", JsonArrayDel(JsonObject(), 0));
    JT("arr.get.notarray", JsonArrayGet(JsonObject(), 0));
    JT("arr.set.notarray", JsonArraySet(JsonObject(), 0, JsonInt(1)));
    JT("obj.keys.notobject", JsonObjectKeys(JsonArray()));
    string r = "-1.-1,0.-2,1.1,3.5,5.9,-5.0,0.-3,2.2,-1.0,1.-2,0.0,1.0,2.1,0.-4,0.-9";
    int n = 0, e;
    while (n < GetStringLength(r))
    {
        e = FindSubString(r, ",", n);
        if (e < 0) e = GetStringLength(r);
        string t = GetSubString(r, n, e - n);
        int dot = FindSubString(t, ".", 1);
        JT("range." + t, JsonArrayGetRange(a, StringToInt(GetStringLeft(t, dot)), StringToInt(GetSubString(t, dot + 1, 9))));
        n = e + 1;
    }
    JT("range.notarray", JsonArrayGetRange(JsonObject(), 0, 1));
    JT("range.empty", JsonArrayGetRange(JsonArray(), 0, 0));
    J("unique", JsonArrayTransform(JsonParse("[1,2,1,3,2,1.0,\"a\",\"a\"]"), JSON_ARRAY_UNIQUE));
    J("sort.mixed", JsonArrayTransform(JsonParse("[{\"a\":1},[1],\"s\",2,1.5,false,null,[0],{}]"), JSON_ARRAY_SORT_ASCENDING));
    J("coalesce.allnull", JsonArrayTransform(JsonParse("[null,null]"), JSON_ARRAY_COALESCE));
    JT("transform.notarray", JsonArrayTransform(JsonObject(), JSON_ARRAY_REVERSE));
    J("subset.rev", JsonSetOp(JsonParse("[1,2,3]"), JSON_SET_SUBSET, JsonParse("[1,2]")));
    J("subset.same", JsonSetOp(JsonParse("[1,2]"), JSON_SET_SUBSET, JsonParse("[2,1]")));
    J("setop.unsorted", JsonSetOp(JsonParse("[3,1,2]"), JSON_SET_UNION, JsonParse("[\"x\",0]")));
    JT("setop.notarray", JsonSetOp(JsonObject(), JSON_SET_UNION, JsonArray()));
    J("find.objnth", JsonFind(JsonParse("{\"b\":1,\"a\":1,\"c\":2}"), JsonInt(1), 1));
    J("find.lt", JsonFind(JsonParse("[5,3,1,0]"), JsonInt(3), 0, JSON_FIND_LT));
    J("find.lte.nth1", JsonFind(JsonParse("[5,3,1,0]"), JsonInt(3), 1, JSON_FIND_LTE));
    J("find.gte", JsonFind(JsonParse("[1,\"a\",5]"), JsonInt(5), 0, JSON_FIND_GTE));
    J("find.neg", JsonFind(JsonParse("[1,1]"), JsonInt(1), -1));
    JT("find.scalar", JsonFind(JsonInt(1), JsonInt(1)));
    JT("ptr.tilde.bad", JsonPointer(JsonParse("{\"a\":1}"), "/~2"));
    JT("ptr.leadingzero", JsonPointer(JsonParse("[1,2]"), "/01"));
    JT("ptr.notnumber", JsonPointer(JsonParse("[1,2]"), "/x"));
    JT("ptr.scalar", JsonPointer(JsonInt(1), "/a"));
    JT("patch.notarray", JsonPatch(JsonObject(), JsonObject()));
    JT("patch.noop", JsonPatch(JsonObject(), JsonParse("[{\"path\":\"/a\"}]")));
    JT("patch.nopath", JsonPatch(JsonObject(), JsonParse("[{\"op\":\"add\",\"value\":1}]")));
    JT("patch.novalue", JsonPatch(JsonObject(), JsonParse("[{\"op\":\"add\",\"path\":\"/a\"}]")));
    JT("patch.arrayindex", JsonPatch(JsonParse("[1]"), JsonParse("[{\"op\":\"remove\",\"path\":\"/5\"}]")));
    JT("patch.addindex", JsonPatch(JsonParse("[1]"), JsonParse("[{\"op\":\"add\",\"path\":\"/5\",\"value\":1}]")));
    JT("patch.addparent", JsonPatch(JsonObject(), JsonParse("[{\"op\":\"add\",\"path\":\"/x/y\",\"value\":1}]")));
    JT("patch.replaceroot", JsonPatch(JsonObject(), JsonParse("[{\"op\":\"replace\",\"path\":\"\",\"value\":[1]}]")));
    JT("patch.moveinto", JsonPatch(JsonParse("{\"a\":{\"b\":1}}"), JsonParse("[{\"op\":\"move\",\"from\":\"/a\",\"path\":\"/a/c\"}]")));
    J("diff.types", JsonDiff(JsonParse("{\"a\":[1,2,3]}"), JsonParse("{\"a\":{\"x\":1}}")));
    J("diff.arrays", JsonDiff(JsonParse("[1,2,3,4]"), JsonParse("[1,5]")));
    J("diff.grow", JsonDiff(JsonParse("[1]"), JsonParse("[1,2,3]")));
    J("diff.keys", JsonDiff(JsonParse("{\"a/b\":1,\"c~\":2}"), JsonParse("{}")));
    J("diff.root", JsonDiff(JsonInt(1), JsonString("x")));
}

void Inplace()
{
    json a = JsonParse("[1,2]");
    JsonArraySetInplace(a, 5, JsonInt(1));
    JT("inplace.set.bad", a);
    a = JsonParse("[1,2]");
    JsonArrayInsertInplace(a, JsonInt(1), 5);
    JT("inplace.insert.bad", a);
    a = JsonParse("[1,2]");
    JsonArrayInsertInplace(a, JsonInt(9), 2);
    JT("inplace.insert.end", a);
    a = JsonParse("[1,2]");
    JsonArrayDelInplace(a, 5);
    JT("inplace.del.bad", a);
    json o = JsonArray();
    JsonObjectSetInplace(o, "k", JsonInt(1));
    JT("inplace.objset.notobject", o);
    o = JsonArray();
    JsonObjectDelInplace(o, "k");
    JT("inplace.objdel.notobject", o);
    o = JsonParse("{\"k\":1}");
    JsonObjectDelInplace(o, "zz");
    JT("inplace.objdel.missing", o);
    json p = JsonObject();
    JsonObjectSetInplace(p, "self", p);
    J("inplace.self", p);
    json q = JsonArray();
    JsonArrayInsertInplace(q, q);
    JsonArrayInsertInplace(q, q);
    J("inplace.selfarr", q);
}

void main()
{
    DelayCommand(0.1, Numbers());
    DelayCommand(0.2, Strings());
    DelayCommand(0.3, Pretty());
    DelayCommand(0.4, Edges());
    DelayCommand(0.5, Inplace());
}
