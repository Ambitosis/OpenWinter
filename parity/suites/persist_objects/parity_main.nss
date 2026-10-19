// Object serialization measured on the original: what ObjectToJson writes for every object type (with and without
// the object state), what JsonToObject, Store/RetrieveCampaignObject and SqlBindObject/SqlGetObject restore, how the
// campaign database stores an object, and TemplateToJson of the combined area format (CAF).
// Full key lists are RAW notes (not compared); the curated readings are compared.
#include "parity_inc"

sqlquery Q(string s) { return SqlPrepareQueryObject(GetModule(), s); }

// The keys of a json object, sorted, comma separated.
string Keys(json j)
{
    if (JsonGetType(j) != JSON_TYPE_OBJECT) return "<" + IntToString(JsonGetType(j)) + ">";
    sqlquery q = Q("SELECT group_concat(value, ',') FROM (SELECT value FROM json_each(@j) ORDER BY value)");
    SqlBindJson(q, "@j", JsonObjectKeys(j));
    return SqlStep(q) ? SqlGetString(q, 0) : "";
}

void Raw(string k, string v) { WriteTimestampedLogEntry("RAW|" + k + "|" + v); }

int Has(json j, string k) { return JsonGetType(JsonObjectGet(j, k)) != JSON_TYPE_NULL; }

// A GFF list field's entry count (-1 when absent). Effect entries the engine rebuilds on load (SkipOnLoad: from
// equipment) are not counted.
int ListCount(json j, string k)
{
    json f = JsonObjectGet(j, k);
    if (JsonGetType(f) == JSON_TYPE_NULL) return -1;
    json l = JsonObjectGet(f, "value");
    int n = 0, i;
    for (i = 0; i < JsonGetLength(l); i++)
        if (JsonGetInt(JsonObjectGet(JsonObjectGet(JsonArrayGet(l, i), "SkipOnLoad"), "value")) == 0) n++;
    return n;
}

string Desc(object o)
{
    if (!GetIsObjectValid(o)) return "invalid";
    int t = GetObjectType(o);
    string s = "t=" + IntToString(t) + " tag=" + GetTag(o) + " pi=" + IntToString(GetLocalInt(o, "pi"))
        + " ps=" + GetLocalString(o, "ps") + " pj=" + JsonDump(GetLocalJson(o, "pj"))
        + " pl=" + (GetAreaFromLocation(GetLocalLocation(o, "pl")) == GetObjectByTag("PARITY_AREA") ? "area" : "none");
    int n = 0;
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { n++; e = GetNextEffect(o); }
    // (Items: the original also reports an effect on a ring; not part of this suite.)
    if (t != OBJECT_TYPE_ITEM) s += " fx=" + IntToString(n);
    if (t == OBJECT_TYPE_CREATURE) s += " act=" + IntToString(GetCurrentAction(o));
    if (t == OBJECT_TYPE_DOOR || t == OBJECT_TYPE_TRIGGER) s += " link=" + GetTag(GetTransitionTarget(o));
    if (t == OBJECT_TYPE_DOOR) s += " open=" + IntToString(GetIsOpen(o)) + " lock=" + IntToString(GetLocked(o));
    if (t == OBJECT_TYPE_STORE) s += " gold=" + IntToString(GetStoreGold(o));
    if (t == OBJECT_TYPE_ITEM || t == OBJECT_TYPE_PLACEABLE || t == OBJECT_TYPE_STORE || t == OBJECT_TYPE_CREATURE)
    {
        int i = 0;
        object it = GetFirstItemInInventory(o);
        while (GetIsObjectValid(it)) { i++; it = GetNextItemInInventory(o); }
        s += " inv=" + IntToString(i);
    }
    if (t != OBJECT_TYPE_ITEM && t != 0)
    {
        object a = GetArea(o);
        s += " area=" + (a == GetObjectByTag("PARITY_AREA") ? "same" : GetIsObjectValid(a) ? "other" : "none");
    }
    return s;
}

void Json(string k, object o, int bState)
{
    json j = ObjectToJson(o, bState);
    string p = "json." + k + "." + IntToString(bState);
    PLogI(p + ".type", JsonGetType(j));
    if (JsonGetType(j) != JSON_TYPE_OBJECT) { PLog(p + ".err", JsonGetError(j) == "" ? "none" : "set"); return; }
    PLog(p + ".data", JsonGetString(JsonObjectGet(j, "__data_type")));
    Raw(p + ".keys", Keys(j));
    PLog(p + ".lists", "var=" + IntToString(ListCount(j, "VarTable")) + " fx=" + IntToString(ListCount(j, "EffectList"))
        + " act=" + IntToString(ListCount(j, "ActionList")));
    PLog(p + ".has", "ObjectId=" + IntToString(Has(j, "ObjectId")) + " LinkedTo=" + IntToString(Has(j, "LinkedTo"))
        + " XPosition=" + IntToString(Has(j, "XPosition")) + " X=" + IntToString(Has(j, "X"))
        + " Tag=" + IntToString(Has(j, "Tag")) + " TemplateResRef=" + IntToString(Has(j, "TemplateResRef"))
        + " ItemList=" + IntToString(Has(j, "ItemList")) + " Geometry=" + IntToString(Has(j, "Geometry"))
        + " ARE=" + IntToString(Has(j, "ARE")) + " GIT=" + IntToString(Has(j, "GIT")) + " UUID=" + IntToString(Has(j, "UUID")));
    // VarTable entry types by variable (the entries' order differs between engines).
    json vt = JsonObjectGet(JsonObjectGet(j, "VarTable"), "value");
    string types = "";
    int n;
    for (n = 0; n < 4; n++)
    {
        string want = n == 0 ? "pi" : n == 1 ? "ps" : n == 2 ? "pj" : "pl";
        int i;
        for (i = 0; i < JsonGetLength(vt); i++)
        {
            json e = JsonArrayGet(vt, i);
            if (JsonGetString(JsonObjectGet(JsonObjectGet(e, "Name"), "value")) == want)
                types += " " + want + "=" + IntToString(JsonGetInt(JsonObjectGet(JsonObjectGet(e, "Type"), "value")));
        }
    }
    if (JsonGetLength(vt) > 0)
    {
        PLog(p + ".vartypes", types);
        Raw(p + ".vartable", JsonDump(vt));
    }
    if (ListCount(j, "EffectList") > 0)
        Raw(p + ".effect0", Keys(JsonArrayGet(JsonObjectGet(JsonObjectGet(j, "EffectList"), "value"), 0)));
    if (ListCount(j, "ActionList") > 0)
        Raw(p + ".action0", Keys(JsonArrayGet(JsonObjectGet(JsonObjectGet(j, "ActionList"), "value"), 0)));
    if (Has(j, "ARE")) Raw(p + ".are", Keys(JsonObjectGet(JsonObjectGet(j, "ARE"), "value")));
    if (Has(j, "GIT")) Raw(p + ".git", Keys(JsonObjectGet(JsonObjectGet(j, "GIT"), "value")));
    location l = PCentre(4.0, -4.0);
    PLog(p + ".back", Desc(JsonToObject(j, l, OBJECT_INVALID, bState)));
    PLog(p + ".back.flip", Desc(JsonToObject(j, l, OBJECT_INVALID, !bState)));
}

void Camp(string k, object o, int bState)
{
    string p = "camp." + k + "." + IntToString(bState);
    string v = k + IntToString(bState);
    PLogI(p + ".stored", StoreCampaignObject("po_db", v, o, OBJECT_INVALID, bState));
    sqlquery q = SqlPrepareQueryCampaign("po_db", "SELECT vartype, compressed, typeof(payload), substr(hex(payload), 1, 24) FROM db WHERE varname = @v");
    SqlBindString(q, "@v", v);
    if (SqlStep(q))
        PLog(p + ".row", IntToString(SqlGetInt(q, 0)) + " " + IntToString(SqlGetInt(q, 1)) + " " + SqlGetString(q, 2) + " " + SqlGetString(q, 3));
    else PLog(p + ".row", "none err=" + (SqlGetError(q) == "" ? "" : "set"));
    PLog(p + ".back", Desc(RetrieveCampaignObject("po_db", v, PCentre(-4.0, 4.0), OBJECT_INVALID, OBJECT_INVALID, bState)));
}

void Sql(string k, object o, int bState)
{
    string p = "sql." + k + "." + IntToString(bState);
    sqlquery q = Q("INSERT INTO po (k, o) VALUES (@k, @o)");
    SqlBindString(q, "@k", k + IntToString(bState));
    SqlBindObject(q, "@o", o, bState);
    SqlStep(q);
    PLog(p + ".bind", SqlGetError(q) == "" ? "ok" : "error");
    q = Q("SELECT o, typeof(o), substr(hex(o), 1, 8) FROM po WHERE k = @k");
    SqlBindString(q, "@k", k + IntToString(bState));
    if (SqlStep(q))
    {
        PLog(p + ".col", SqlGetString(q, 1) + " " + SqlGetString(q, 2));
        PLog(p + ".back", Desc(SqlGetObject(q, 0, PCentre(-4.0, -4.0), OBJECT_INVALID, bState)));
    }
    else PLog(p + ".col", "none");
}

void Prepare(object o, string k)
{
    SetLocalInt(o, "pi", 7);
    SetLocalString(o, "ps", "hi");
    SetLocalJson(o, "pj", JsonParse("[1,2]"));
    SetLocalLocation(o, "pl", PCentre(1.0, 1.0));
    SetLocalObject(GetModule(), "obj_" + k, o);
}

void Setup()
{
    object area = GetObjectByTag("PARITY_AREA");
    SqlStep(Q("CREATE TABLE IF NOT EXISTS po (k TEXT, o BLOB)"));
    object crt = CreateObject(OBJECT_TYPE_CREATURE, "po_crt", PCentre(-2.0, 0.0));
    CreateItemOnObject("nw_it_mpotion001", crt);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectACIncrease(2), crt, 300.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_STRENGTH, 2), crt);
    AssignCommand(crt, ActionWait(60.0));
    object itm = CreateObject(OBJECT_TYPE_ITEM, "nw_it_mring001", PCentre(0.0, 2.0), FALSE, "PO_ITM");
    object plc = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(2.0, 0.0), FALSE, "PO_PLC");
    CreateItemOnObject("nw_it_mpotion001", plc);
    object wp = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", PCentre(0.0, -2.0), FALSE, "PO_WP");
    object sto = CreateObject(OBJECT_TYPE_STORE, "nw_storgenral001", PCentre(-2.0, -2.0), FALSE, "PO_STO");
    SetStoreGold(sto, 1234);
    object door = GetObjectByTag("PO_DOOR");
    SetLocked(door, TRUE);
    object trig = GetObjectByTag("PO_TRIG");
    SetTransitionTarget(trig, wp);
    SetTransitionTarget(door, wp);
    Prepare(crt, "crt"); Prepare(itm, "itm"); Prepare(plc, "plc"); Prepare(wp, "wp"); Prepare(sto, "sto");
    Prepare(door, "door"); Prepare(trig, "trig"); Prepare(GetObjectByTag("PO_SND"), "snd"); Prepare(GetObjectByTag("PO_ENC"), "enc");
    Prepare(area, "area");
    PLog("setup", Desc(crt) + " | " + Desc(door) + " | " + Desc(trig) + " | " + Desc(sto));
}

void Measure(string k)
{
    object o = GetLocalObject(GetModule(), "obj_" + k);
    PLog("orig." + k, Desc(o));
    Json(k, o, FALSE);
    Json(k, o, TRUE);
    Camp(k, o, FALSE);
    Camp(k, o, TRUE);
    Sql(k, o, FALSE);
    Sql(k, o, TRUE);
}

void Templates()
{
    json caf = TemplateToJson("parity", RESTYPE_CAF);
    PLogI("tmpl.caf.type", JsonGetType(caf));
    PLog("tmpl.caf.data", JsonGetString(JsonObjectGet(caf, "__data_type")));
    Raw("tmpl.caf.keys", Keys(caf));
    PLog("tmpl.are.data", JsonGetString(JsonObjectGet(TemplateToJson("parity", RESTYPE_ARE), "__data_type")));
    PLog("tmpl.git.data", JsonGetString(JsonObjectGet(TemplateToJson("parity", RESTYPE_GIT), "__data_type")));
    // nwbufcompress: the envelope the original writes (magic, version, algorithm, size).
    sqlquery q = Q("SELECT substr(hex(nwbufcompress('ABCD', zeroblob(4000))), 1, 32), length(nwbufcompress('ABCD', zeroblob(4000)))");
    if (SqlStep(q)) PLog("buf.header", SqlGetString(q, 0) + " len~" + IntToString(SqlGetInt(q, 1) / 100));
    else PLog("buf.header", "error");
}

void Step(int n)
{
    switch (n)
    {
        case 0: Setup(); break;
        case 1: Measure("crt"); break;
        case 2: Measure("itm"); break;
        case 3: Measure("plc"); break;
        case 4: Measure("wp"); break;
        case 5: Measure("sto"); break;
        case 6: Measure("door"); break;
        case 7: Measure("trig"); break;
        case 8: Measure("snd"); break;
        case 9: Measure("enc"); break;
        case 10: Measure("area"); break;
        case 11: Templates(); break;
        case 12: PDone(); return;
    }
    DelayCommand(0.5, Step(n + 1));
}

void main()
{
    DelayCommand(1.0, Step(0));
}
