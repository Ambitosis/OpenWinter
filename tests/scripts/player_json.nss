// Player package: json patch/diff, array transforms, set ops and in-place mutation.
void main()
{
    object m = GetModule();
    json a = JsonParse("{\"a\":1,\"b\":[1,2,3],\"c\":{\"d\":\"x\"}}");
    json b = JsonParse("{\"a\":2,\"b\":[1,2],\"c\":{\"d\":\"x\",\"e\":true},\"f\":null}");
    json diff = JsonDiff(a, b);
    SetLocalString(m, "diff", JsonDump(diff));
    SetLocalString(m, "patched", JsonDump(JsonPatch(a, diff)));
    SetLocalString(m, "b", JsonDump(b));

    json p = JsonParse("[{\"op\":\"add\",\"path\":\"/x\",\"value\":5},{\"op\":\"move\",\"from\":\"/a\",\"path\":\"/z\"},{\"op\":\"test\",\"path\":\"/z\",\"value\":1}]");
    SetLocalString(m, "patch2", JsonDump(JsonPatch(a, p)));
    json bad = JsonPatch(a, JsonParse("[{\"op\":\"test\",\"path\":\"/a\",\"value\":99}]"));
    SetLocalInt(m, "patch_bad_type", JsonGetType(bad));
    SetLocalString(m, "patch_bad_err", JsonGetError(bad));

    json arr = JsonParse("[3,\"100\",1,99,null,true,1]");
    SetLocalString(m, "sorted", JsonDump(JsonArrayTransform(arr, JSON_ARRAY_SORT_ASCENDING)));
    SetLocalString(m, "unique", JsonDump(JsonArrayTransform(JsonParse("[1,1.0,\"1\",2,1]"), JSON_ARRAY_UNIQUE)));
    SetLocalString(m, "reverse", JsonDump(JsonArrayTransform(JsonParse("[1,2,3]"), JSON_ARRAY_REVERSE)));
    SetLocalString(m, "coalesce", JsonDump(JsonArrayTransform(JsonParse("[null,null,\"\",4]"), JSON_ARRAY_COALESCE)));

    json r = JsonParse("[0,1,2,3,4]");
    SetLocalString(m, "range", JsonDump(JsonArrayGetRange(r, 1, -1)) + JsonDump(JsonArrayGetRange(r, 0, 999)) + JsonDump(JsonArrayGetRange(r, 1, 0)) + JsonDump(JsonArrayGetRange(r, 1, 1)));

    json s1 = JsonParse("[1,2,3]");
    json s2 = JsonParse("[2,3,4]");
    SetLocalString(m, "setops", JsonDump(JsonSetOp(s1, JSON_SET_UNION, s2)) + JsonDump(JsonSetOp(s1, JSON_SET_INTERSECT, s2))
        + JsonDump(JsonSetOp(s1, JSON_SET_DIFFERENCE, s2)) + JsonDump(JsonSetOp(s1, JSON_SET_SYMMETRIC_DIFFERENCE, s2))
        + JsonDump(JsonSetOp(JsonParse("[2,3]"), JSON_SET_SUBSET, s1)));

    json o = JsonObject();
    JsonObjectSetInplace(o, "k", JsonInt(7));
    JsonObjectSetInplace(o, "gone", JsonInt(1));
    JsonObjectDelInplace(o, "gone");
    json l = JsonArray();
    JsonArrayInsertInplace(l, JsonInt(1));
    JsonArrayInsertInplace(l, JsonInt(0), 0);
    JsonArrayInsertInplace(l, JsonInt(9));
    JsonArraySetInplace(l, 2, JsonInt(2));
    JsonArrayInsertInplace(l, JsonInt(3));
    JsonArrayDelInplace(l, 3);
    SetLocalString(m, "inplace", JsonDump(o) + JsonDump(l));
}
