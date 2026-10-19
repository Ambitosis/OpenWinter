// Objects that are copied, saved and loaded: what their item properties read back (Param1 and Param1Value,
// 255 or -1) and whether their local object references still point anywhere.
// Sources: a creature wearing template items with properties added by script, and a ring on its own.
// Copies: CopyObject, SqlGetObject, RetrieveCampaignObject, JsonToObject, CopyItem; items on the ground
// (placed in the area file, CreateObject, CopyObject, CopyItem without a target, dropped).
#include "parity_itemev"

// Every item of a creature (slots, then inventory) with its properties.
string Props(object o)
{
    if (!GetIsObjectValid(o)) return "invalid";
    if (GetObjectType(o) == OBJECT_TYPE_ITEM) return PIpList(o);
    string s = "";
    int n;
    for (n = 0; n < NUM_INVENTORY_SLOTS; n++)
    {
        object it = GetItemInSlot(n, o);
        if (GetIsObjectValid(it) && n != INVENTORY_SLOT_CARMOUR) s += IntToString(n) + "=" + GetResRef(it) + "{" + PIpList(it) + "} ";
    }
    object it = GetFirstItemInInventory(o);
    while (GetIsObjectValid(it)) { s += GetResRef(it) + "{" + PIpList(it) + "} "; it = GetNextItemInInventory(o); }
    return s;
}

// What a local object reads back as: invalid, the object it was set to, the copy itself, or something else.
string Ref(object o, string sName, object oWas)
{
    object v = GetLocalObject(o, sName);
    if (!GetIsObjectValid(v)) return sName + "=inv";
    if (v == oWas) return sName + "=same";
    if (v == o) return sName + "=self";
    return sName + "=other:" + PEvName(v);
}

void Refs(string k, object o)
{
    object x = GetLocalObject(GetModule(), "X");
    object ring = GetLocalObject(GetModule(), "RING");
    if (!GetIsObjectValid(o)) { PLog(k + ".refs", "invalid"); return; }
    if (GetObjectType(o) == OBJECT_TYPE_ITEM)
        PLog(k + ".refs", Ref(o, "owner", x) + " " + Ref(o, "other", GetLocalObject(GetModule(), "Y")) + " " + Ref(o, "self", ring)
            + " i=" + IntToString(GetLocalInt(o, "i")) + " raw=" + IntToString(GetLocalObject(o, "owner") == OBJECT_INVALID));
    else
        PLog(k + ".refs", Ref(o, "self", x) + " " + Ref(o, "other", GetLocalObject(GetModule(), "Y")) + " " + Ref(o, "mod", GetModule())
            + " " + Ref(o, "area", GetObjectByTag("PARITY_AREA")) + " " + Ref(o, "ring", ring) + " " + Ref(o, "inv", OBJECT_INVALID)
            + " i=" + IntToString(GetLocalInt(o, "i")) + " raw=" + IntToString(GetLocalObject(o, "self") == OBJECT_INVALID));
}

void PlcRefs(string k, object o, object p)
{
    PLog(k + ".refs", Ref(o, "self", p) + " " + Ref(o, "other", GetLocalObject(GetModule(), "Y")) + " i=" + IntToString(GetLocalInt(o, "i")));
}

void Report(string k, object o)
{
    PLog(k + ".props", Props(o));
    Refs(k, o);
    // Read again at the end.
    object m = GetModule();
    int n = GetLocalInt(m, "reports");
    SetLocalInt(m, "reports", n + 1);
    SetLocalString(m, "rk" + IntToString(n), k);
    SetLocalObject(m, "ro" + IntToString(n), o);
}

void Late()
{
    object m = GetModule();
    int n;
    for (n = 0; n < GetLocalInt(m, "reports"); n++)
        PLog(GetLocalString(m, "rk" + IntToString(n)) + ".late", Props(GetLocalObject(m, "ro" + IntToString(n))));
}

object SqlRoundTrip(object o, location l, object oOwner, int bState = FALSE)
{
    sqlquery q = SqlPrepareQueryObject(GetModule(), "CREATE TABLE IF NOT EXISTS sv (k TEXT, o BLOB)");
    SqlStep(q);
    q = SqlPrepareQueryObject(GetModule(), "INSERT INTO sv (k, o) VALUES (@k, @o)");
    SqlBindString(q, "@k", ObjectToString(o) + IntToString(bState));
    SqlBindObject(q, "@o", o, bState);
    SqlStep(q);
    q = SqlPrepareQueryObject(GetModule(), "SELECT o FROM sv WHERE k = @k");
    SqlBindString(q, "@k", ObjectToString(o) + IntToString(bState));
    return SqlStep(q) ? SqlGetObject(q, 0, l, oOwner, bState) : OBJECT_INVALID;
}

void Setup()
{
    object x = CreateObject(OBJECT_TYPE_CREATURE, "sv_bandit", PCentre(-6.0, 0.0), FALSE, "SV_X");
    object y = CreateObject(OBJECT_TYPE_CREATURE, "sv_bandit", PCentre(-6.0, 3.0), FALSE, "SV_Y");
    object ring = CreateItemOnObject("nw_it_mring001", x, 1, "SV_RING");
    SetLocalObject(GetModule(), "SWORD", CreateItemOnObject("nw_wswmls002", x, 1, "SV_SWORD"));
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyACBonus(2), ring);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyOnHitProps(IP_CONST_ONHIT_SLEEP, IP_CONST_ONHIT_SAVEDC_14, IP_CONST_ONHIT_DURATION_5_PERCENT_5_ROUNDS), ring);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyEnhancementBonus(1), GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, x));
    SetLocalObject(GetModule(), "X", x);
    SetLocalObject(GetModule(), "Y", y);
    SetLocalObject(GetModule(), "RING", ring);
    SetLocalObject(x, "self", x);
    SetLocalObject(x, "other", y);
    SetLocalObject(x, "mod", GetModule());
    SetLocalObject(x, "area", GetArea(x));
    SetLocalObject(x, "ring", ring);
    SetLocalObject(x, "inv", OBJECT_INVALID);
    SetLocalInt(x, "i", 7);
    SetLocalObject(ring, "owner", x);
    SetLocalObject(ring, "other", y);
    SetLocalObject(ring, "self", ring);
    SetLocalInt(ring, "i", 9);
}

void Creatures()
{
    object x = GetLocalObject(GetModule(), "X");
    Report("orig", x);
    Report("copy", CopyObject(x, PCentre(-3.0, 0.0), OBJECT_INVALID, "SV_COPY"));
    Report("sql", SqlRoundTrip(x, PCentre(0.0, 0.0), OBJECT_INVALID));
    StoreCampaignObject("sv_db", "x", x);
    Report("camp", RetrieveCampaignObject("sv_db", "x", PCentre(3.0, 0.0)));
    Report("json", JsonToObject(ObjectToJson(x), PCentre(6.0, 0.0)));
    object p = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(-8.0, -6.0), FALSE, "SV_P");
    SetLocalObject(p, "self", p);
    SetLocalObject(p, "other", GetLocalObject(GetModule(), "Y"));
    SetLocalInt(p, "i", 5);
    PlcRefs("plc.copy", CopyObject(p, PCentre(-6.0, -6.0)), p);
    PlcRefs("plc.copy.state", CopyObject(p, PCentre(-6.0, -6.0), OBJECT_INVALID, "", TRUE), p);
    PlcRefs("plc.sql", SqlRoundTrip(p, PCentre(-4.0, -6.0), OBJECT_INVALID), p);
    PlcRefs("plc.sql.state", SqlRoundTrip(p, PCentre(-4.0, -6.0), OBJECT_INVALID, TRUE), p);
    PlcRefs("plc.json", JsonToObject(ObjectToJson(p), PCentre(-2.0, -6.0)), p);
    PlcRefs("plc.json.state", JsonToObject(ObjectToJson(p, TRUE), PCentre(-2.0, -6.0), OBJECT_INVALID, TRUE), p);
    // With the object state.
    Report("copy.state", CopyObject(x, PCentre(-3.0, 6.0), OBJECT_INVALID, "SV_COPY2", TRUE));
    Report("sql.state", SqlRoundTrip(x, PCentre(0.0, 6.0), OBJECT_INVALID, TRUE));
    StoreCampaignObject("sv_db", "xs", x, OBJECT_INVALID, TRUE);
    Report("camp.state", RetrieveCampaignObject("sv_db", "xs", PCentre(3.0, 6.0), OBJECT_INVALID, OBJECT_INVALID, TRUE));
    Report("camp.halfstate", RetrieveCampaignObject("sv_db", "x", PCentre(3.0, 8.0), OBJECT_INVALID, OBJECT_INVALID, TRUE));
    Report("json.state", JsonToObject(ObjectToJson(x, TRUE), PCentre(6.0, 6.0), OBJECT_INVALID, TRUE));
    Report("json.savestate", JsonToObject(ObjectToJson(x, TRUE), PCentre(6.0, 8.0)));
}

void Items()
{
    object x = GetLocalObject(GetModule(), "X");
    object y = GetLocalObject(GetModule(), "Y");
    object ring = GetLocalObject(GetModule(), "RING");
    Report("ring", ring);
    Report("ring.copyobj", CopyObject(ring, GetLocation(y), y, "SV_R1"));
    Report("ring.copyobj.ground", CopyObject(ring, PCentre(0.0, -4.0), OBJECT_INVALID, "SV_R2"));
    Report("ring.copyitem", CopyItem(ring, y, TRUE));
    Report("ring.copyitem.ground", CopyItem(ring, OBJECT_INVALID, TRUE));
    Report("ring.sql", SqlRoundTrip(ring, GetLocation(y), y));
    Report("ring.sql.ground", SqlRoundTrip(ring, PCentre(1.0, -4.0), OBJECT_INVALID));
    StoreCampaignObject("sv_db", "r", ring);
    Report("ring.camp", RetrieveCampaignObject("sv_db", "r", GetLocation(y), y));
    Report("ring.camp.ground", RetrieveCampaignObject("sv_db", "r", PCentre(2.0, -4.0)));
    Report("ring.json", JsonToObject(ObjectToJson(ring), GetLocation(y), y));
    Report("ring.json.ground", JsonToObject(ObjectToJson(ring), PCentre(3.0, -4.0)));
    Report("tmpl.create", CreateItemOnObject("nw_it_mring001", y, 1, "SV_T1"));
    Report("tmpl.ground", CreateObject(OBJECT_TYPE_ITEM, "nw_it_mring001", PCentre(4.0, -4.0), FALSE, "SV_T2"));
    Report("tmpl.git", GetObjectByTag("SV_GIT"));
    Report("tmpl.git.copy", CopyItem(GetObjectByTag("SV_GIT"), y, TRUE));
    object chest = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(5.0, -4.0), FALSE, "SV_CHEST");
    Report("tmpl.chest", CreateItemOnObject("nw_it_mring001", chest, 1, "SV_T3"));
    Report("ring.chest", CopyItem(ring, chest, TRUE));
    // Template items on a creature: as spawned, then copied.
    Report("spawn", y);
    Report("spawn.copy", CopyObject(y, PCentre(-3.0, 3.0), OBJECT_INVALID, "SV_YCOPY"));
    AssignCommand(x, ActionPutDownItem(GetLocalObject(GetModule(), "SWORD")));
}

void Dropped()
{
    object s = GetLocalObject(GetModule(), "SWORD");
    PLog("dropped.where", GetIsObjectValid(GetItemPossessor(s)) ? "held" : "ground");
    Report("dropped", s);
    Report("dropped.copy", CopyItem(s, GetLocalObject(GetModule(), "Y"), TRUE));
    Late();
    PDone();
}

void main()
{
    DelayCommand(1.0, Setup());
    DelayCommand(1.5, Creatures());
    DelayCommand(2.0, Items());
    DelayCommand(5.0, Dropped());
}
