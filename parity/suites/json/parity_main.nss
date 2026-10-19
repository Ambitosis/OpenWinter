// EE json: parsing, dumping, accessors, mutation (copying and in place), pointers, patches and helpers.
#include "parity_inc"

void J(string k, json j) { PLog(k, JsonDump(j)); }
void JT(string k, json j) { PLog(k, IntToString(JsonGetType(j)) + " " + JsonDump(j) + " err=[" + JsonGetError(j) + "]"); }

void Parsing()
{
    string s = "{\"b\":1,\"a\":2,\"c\":{\"z\":1,\"y\":2}}";
    J("parse.order", JsonParse(s));
    J("parse.int", JsonParse("42"));
    J("parse.neg", JsonParse("-0"));
    J("parse.float", JsonParse("1.5"));
    J("parse.exp", JsonParse("1e3"));
    J("parse.bigint", JsonParse("12345678901234567890"));
    J("parse.big2", JsonParse("9223372036854775807"));
    J("parse.float.prec", JsonParse("0.1"));
    J("parse.string", JsonParse("\"a\\u00e9\\n\""));
    J("parse.true", JsonParse("true"));
    J("parse.null", JsonParse("null"));
    J("parse.ws", JsonParse("  [1 , 2 ]  "));
    J("parse.dupkey", JsonParse("{\"a\":1,\"a\":2}"));
    JT("parse.empty", JsonParse(""));
    JT("parse.bad", JsonParse("{x"));
    JT("parse.trailing", JsonParse("[1,]"));
    JT("parse.junk", JsonParse("1 2"));
    JT("parse.single", JsonParse("'a'"));
    J("parse.nested", JsonParse("[[[[[]]]]]"));
}

void Values()
{
    J("make.int", JsonInt(-7));
    J("make.float", JsonFloat(1.1));
    J("make.float.int", JsonFloat(3.0));
    J("make.float.big", JsonFloat(100000000000000000000.0));
    J("make.float.tiny", JsonFloat(0.0000001));
    J("make.float.nan", JsonFloat(StringToFloat("nan")));
    J("make.float.inf", JsonFloat(StringToFloat("inf")));
    J("make.float.neg0", JsonFloat(-0.0));
    J("make.bool", JsonBool(5));
    J("make.string", JsonString("q\"\\/\t"));
    J("make.null", JsonNull());
    JT("make.nullerr", JsonNull("boom"));
    J("make.object", JsonObject());
    J("make.array", JsonArray());
    PLogI("get.int.float", JsonGetInt(JsonFloat(2.9)));
    PLogI("get.int.negfloat", JsonGetInt(JsonFloat(-2.9)));
    PLogI("get.int.string", JsonGetInt(JsonString("12")));
    PLogI("get.int.bool", JsonGetInt(JsonBool(TRUE)));
    PLogI("get.int.big", JsonGetInt(JsonParse("4294967297")));
    PLogI("get.int.null", JsonGetInt(JsonNull()));
    PLogF("get.float.int", JsonGetFloat(JsonInt(3)));
    PLogF("get.float.string", JsonGetFloat(JsonString("1.5")));
    PLogF("get.float.bool", JsonGetFloat(JsonBool(TRUE)));
    PLog("get.string.int", "[" + JsonGetString(JsonInt(3)) + "]");
    PLog("get.string.null", "[" + JsonGetString(JsonNull()) + "]");
    PLog("get.string.str", "[" + JsonGetString(JsonParse("\"x\\u00e9\"")) + "]");
    PLogI("len.object", JsonGetLength(JsonParse("{\"a\":1,\"b\":2}")));
    PLogI("len.array", JsonGetLength(JsonParse("[1,2,3]")));
    PLogI("len.string", JsonGetLength(JsonString("abc")));
    PLogI("len.null", JsonGetLength(JsonNull()));
    PLogI("type.int", JsonGetType(JsonInt(1)));
    PLogI("type.float", JsonGetType(JsonFloat(1.0)));
    PLogI("type.parsedfloat", JsonGetType(JsonParse("1.0")));
    PLogI("type.bool", JsonGetType(JsonBool(0)));
    PLogI("eq.same", JsonParse("{\"a\":[1,2]}") == JsonParse("{ \"a\" : [1, 2] }"));
    PLogI("eq.order", JsonParse("{\"a\":1,\"b\":2}") == JsonParse("{\"b\":2,\"a\":1}"));
    PLogI("eq.intfloat", JsonInt(1) == JsonFloat(1.0));
    PLogI("eq.null", JsonNull() == JsonNull("x"));
}

void Containers()
{
    json o = JsonParse("{\"a\":1,\"b\":[1,2,3]}");
    J("obj.get", JsonObjectGet(o, "b"));
    JT("obj.get.missing", JsonObjectGet(o, "zz"));
    JT("obj.get.notobject", JsonObjectGet(JsonArray(), "a"));
    J("obj.set", JsonObjectSet(o, "c", JsonString("x")));
    J("obj.set.replace", JsonObjectSet(o, "a", JsonNull()));
    JT("obj.set.notobject", JsonObjectSet(JsonArray(), "a", JsonInt(1)));
    J("obj.del", JsonObjectDel(o, "a"));
    J("obj.del.missing", JsonObjectDel(o, "zz"));
    J("obj.keys", JsonObjectKeys(JsonParse("{\"z\":1,\"a\":2,\"m\":3}")));
    J("obj.orig.unchanged", o);
    json a = JsonParse("[10,20,30]");
    J("arr.get.1", JsonArrayGet(a, 1));
    JT("arr.get.9", JsonArrayGet(a, 9));
    JT("arr.get.-1", JsonArrayGet(a, -1));
    J("arr.set.0", JsonArraySet(a, 0, JsonInt(5)));
    JT("arr.set.3", JsonArraySet(a, 3, JsonInt(5)));
    JT("arr.set.-1", JsonArraySet(a, -1, JsonInt(5)));
    J("arr.insert.end", JsonArrayInsert(a, JsonInt(7)));
    J("arr.insert.0", JsonArrayInsert(a, JsonInt(7), 0));
    J("arr.insert.3", JsonArrayInsert(a, JsonInt(7), 3));
    JT("arr.insert.9", JsonArrayInsert(a, JsonInt(7), 9));
    JT("arr.insert.-2", JsonArrayInsert(a, JsonInt(7), -2));
    J("arr.del.1", JsonArrayDel(a, 1));
    JT("arr.del.9", JsonArrayDel(a, 9));
    JT("arr.insert.notarray", JsonArrayInsert(JsonObject(), JsonInt(1)));
    J("arr.orig.unchanged", a);
    J("range.0.1", JsonArrayGetRange(a, 0, 1));
    J("range.1.-1", JsonArrayGetRange(a, 1, -1));
    J("range.-2.-1", JsonArrayGetRange(a, -2, -1));
    JT("range.2.0", JsonArrayGetRange(a, 2, 0));
    JT("range.0.9", JsonArrayGetRange(a, 0, 9));
    json b = JsonParse("[3,1,\"b\",2,\"a\",null,true,1.5]");
    int t;
    for (t = 0; t <= 7; t++) if (t != JSON_ARRAY_SHUFFLE) J("transform." + IntToString(t), JsonArrayTransform(b, t));
    J("find.1", JsonFind(b, JsonInt(1)));
    J("find.1.nth1", JsonFind(b, JsonInt(1), 1));
    J("find.missing", JsonFind(b, JsonInt(99)));
    J("find.obj", JsonFind(JsonParse("{\"x\":1,\"y\":2}"), JsonInt(2)));
    J("find.gt", JsonFind(JsonParse("[1,5,3]"), JsonInt(2), 0, JSON_FIND_GT));
    J("setop.union", JsonSetOp(JsonParse("[1,2,2,3]"), JSON_SET_UNION, JsonParse("[3,4]")));
    J("setop.inter", JsonSetOp(JsonParse("[1,2,2,3]"), JSON_SET_INTERSECT, JsonParse("[3,2]")));
    J("setop.diff", JsonSetOp(JsonParse("[1,2,2,3]"), JSON_SET_DIFFERENCE, JsonParse("[3]")));
    J("setop.symdiff", JsonSetOp(JsonParse("[1,2,3]"), JSON_SET_SYMMETRIC_DIFFERENCE, JsonParse("[3,4]")));
    J("setop.subset", JsonSetOp(JsonParse("[1,2]"), JSON_SET_SUBSET, JsonParse("[1,2,3]")));
}

void Pointers()
{
    json d = JsonParse("{\"a\":{\"b\":[1,{\"c\":\"x\"}]},\"~k\":1,\"s/l\":2,\"\":3}");
    J("ptr.root", JsonPointer(d, ""));
    J("ptr.a.b.1.c", JsonPointer(d, "/a/b/1/c"));
    J("ptr.tilde", JsonPointer(d, "/~0k"));
    J("ptr.slash", JsonPointer(d, "/s~1l"));
    J("ptr.emptykey", JsonPointer(d, "/"));
    JT("ptr.missing", JsonPointer(d, "/zz"));
    JT("ptr.badindex", JsonPointer(d, "/a/b/9"));
    JT("ptr.noslash", JsonPointer(d, "a"));
    JT("ptr.dash", JsonPointer(d, "/a/b/-"));
    J("patch.add", JsonPatch(d, JsonParse("[{\"op\":\"add\",\"path\":\"/n\",\"value\":5}]")));
    J("patch.addarr", JsonPatch(d, JsonParse("[{\"op\":\"add\",\"path\":\"/a/b/-\",\"value\":5}]")));
    J("patch.remove", JsonPatch(d, JsonParse("[{\"op\":\"remove\",\"path\":\"/a\"}]")));
    J("patch.replace", JsonPatch(d, JsonParse("[{\"op\":\"replace\",\"path\":\"/~0k\",\"value\":9}]")));
    J("patch.move", JsonPatch(d, JsonParse("[{\"op\":\"move\",\"from\":\"/a\",\"path\":\"/m\"}]")));
    J("patch.copy", JsonPatch(d, JsonParse("[{\"op\":\"copy\",\"from\":\"/a/b/0\",\"path\":\"/k\"}]")));
    J("patch.test.ok", JsonPatch(d, JsonParse("[{\"op\":\"test\",\"path\":\"/~0k\",\"value\":1}]")));
    JT("patch.test.fail", JsonPatch(d, JsonParse("[{\"op\":\"test\",\"path\":\"/~0k\",\"value\":2}]")));
    JT("patch.badop", JsonPatch(d, JsonParse("[{\"op\":\"zap\",\"path\":\"/a\"}]")));
    JT("patch.missingpath", JsonPatch(d, JsonParse("[{\"op\":\"remove\",\"path\":\"/nope\"}]")));
    J("diff", JsonDiff(JsonParse("{\"a\":1,\"b\":[1,2]}"), JsonParse("{\"a\":2,\"b\":[1],\"c\":3}")));
    J("diff.same", JsonDiff(JsonParse("[1]"), JsonParse("[1]")));
    J("merge", JsonMerge(JsonParse("{\"a\":1,\"b\":{\"c\":1,\"d\":2}}"), JsonParse("{\"a\":null,\"b\":{\"c\":5},\"e\":[1]}")));
    J("merge.nonobject", JsonMerge(JsonParse("{\"a\":1}"), JsonParse("[1]")));
}

void Inplace()
{
    json a = JsonObject();
    json b = a;
    JsonObjectSetInplace(b, "x", JsonInt(1));
    J("ref.alias", a);
    json c = JsonObjectSet(a, "y", JsonInt(2));
    JsonObjectSetInplace(c, "z", JsonInt(3));
    J("ref.copy.a", a);
    J("ref.copy.c", c);
    json arr = JsonArray();
    JsonArrayInsertInplace(arr, JsonInt(1));
    JsonArrayInsertInplace(arr, JsonInt(0), 0);
    JsonArraySetInplace(arr, 1, JsonString("s"));
    JsonArrayInsertInplace(arr, a);
    J("inplace.arr", arr);
    JsonObjectSetInplace(a, "w", JsonInt(4));
    J("inplace.nested.shared", arr);
    JsonArrayDelInplace(arr, 0);
    JsonObjectDelInplace(a, "x");
    J("inplace.after.del", arr);
    J("inplace.a", a);
    json g = JsonObjectGet(JsonParse("{\"k\":{\"v\":1}}"), "k");
    JsonObjectSetInplace(g, "v", JsonInt(2));
    J("inplace.get", g);
    json p = JsonParse("{\"k\":[1]}");
    json q = JsonObjectGet(p, "k");
    JsonArrayInsertInplace(q, JsonInt(2));
    J("inplace.get.parent", p);
    JsonArraySetInplace(arr, 99, JsonInt(1));
    J("inplace.badindex", arr);
}

void main()
{
    DelayCommand(1.0, Parsing());
    DelayCommand(1.1, Values());
    DelayCommand(1.2, Containers());
    DelayCommand(1.3, Pointers());
    DelayCommand(1.4, Inplace());
    DelayCommand(1.5, ExecuteScript("json_more"));
    DelayCommand(3.0, PDone());
}
