// ObjectToJson / JsonToObject, templates, UUIDs and character export.
void main()
{
    object oMod = GetModule();
    object oPC = GetLocalObject(oMod, "pc");
    object oItem = GetLocalObject(oMod, "item");
    object oArea = GetLocalObject(oMod, "area");
    location lHere = Location(oArea, Vector(3.0, 4.0, 0.0), 0.0);

    string u = GetObjectUUID(oItem);
    SetLocalString(oMod, "uuid", u);
    SetLocalInt(oMod, "uuid_stable", GetObjectUUID(oItem) == u);
    SetLocalInt(oMod, "byuuid", GetObjectByUUID(u) == oItem);
    SetLocalString(oMod, "uuid_module", GetObjectUUID(oMod));
    SetLocalInt(oMod, "byuuid_bad", GetIsObjectValid(GetObjectByUUID("not-a-uuid")));

    json j = ObjectToJson(oItem);
    SetLocalString(oMod, "item_json", JsonDump(j));
    SetLocalString(oMod, "data_type", JsonGetString(JsonObjectGet(j, "__data_type")));
    object oCopy = JsonToObject(j, lHere, oPC);
    SetLocalObject(oMod, "json_copy", oCopy);
    // The copy collides with the original's UUID, so it gets a fresh one.
    SetLocalInt(oMod, "copy_uuid_differs", GetObjectUUID(oCopy) != u);
    SetLocalInt(oMod, "bad_json", GetIsObjectValid(JsonToObject(JsonParse("{\"x\":1}"), lHere)));
    SetLocalString(oMod, "module_json_err", JsonGetError(ObjectToJson(oMod)));

    ForceRefreshObjectUUID(oItem);
    SetLocalInt(oMod, "refreshed", GetObjectUUID(oItem) != u);
    SetLocalInt(oMod, "old_uuid_gone", GetObjectByUUID(u) == OBJECT_INVALID);

    json t = TemplateToJson("nw_wswls001", RESTYPE_UTI);
    SetLocalString(oMod, "template_type", JsonGetString(JsonObjectGet(t, "__data_type")));
    t = JsonObjectSet(t, "Tag", JsonObjectSet(JsonObjectGet(t, "Tag"), "value", JsonString("TEMPLATED")));
    SetLocalInt(oMod, "to_template", JsonToTemplate(t, "zz_tmpl_item", RESTYPE_UTI));
    object oT = CreateObject(OBJECT_TYPE_ITEM, "zz_tmpl_item", lHere);
    SetLocalString(oMod, "templated_tag", GetTag(oT));
    SetLocalString(oMod, "missing_template_err", JsonGetError(TemplateToJson("no_such_thing", RESTYPE_UTI)));

    ExportSingleCharacter(oPC);
    DoSinglePlayerAutoSave();
}
