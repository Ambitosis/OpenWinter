// CopyItem, CopyItemAndModify, CopyObject on items, and GetItemAppearance.
#include "parity_items"

string Appearance(object o)
{
    if (!GetIsObjectValid(o)) return "invalid";
    string s = "s" + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_SIMPLE_MODEL, 0));
    int i;
    s += " wm";
    for (i = 0; i < 3; i++) s += IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_WEAPON_MODEL, i)) + ",";
    s += " wc";
    for (i = 0; i < 3; i++) s += IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_WEAPON_COLOR, i)) + ",";
    s += " am";
    for (i = 0; i < ITEM_APPR_ARMOR_NUM_MODELS; i++) s += IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_ARMOR_MODEL, i)) + ",";
    s += " ac";
    for (i = 0; i < ITEM_APPR_ARMOR_NUM_COLORS; i++) s += IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_ARMOR_COLOR, i)) + ",";
    s += " pc" + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_ARMOR_COLOR, 6 + 7 * 6 + 2)) + "," + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_ARMOR_COLOR, 6 + 18 * 6 + 5));
    s += " bad" + IntToString(GetItemAppearance(o, 5, 0)) + "," + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_ARMOR_MODEL, 19)) + "," + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_WEAPON_MODEL, 3));
    return s;
}

string Where(object o)
{
    if (!GetIsObjectValid(o)) return "invalid";
    object oP = GetItemPossessor(o);
    if (GetIsObjectValid(oP)) return "possessor:" + GetTag(oP);
    if (GetIsObjectValid(GetArea(o))) return "ground";
    return "nowhere";
}

string Info(object o)
{
    if (!GetIsObjectValid(o)) return "invalid";
    return Where(o) + " tag=" + GetTag(o) + " name=" + GetName(o) + " res=" + GetResRef(o) + " stack=" + IntToString(GetItemStackSize(o))
        + " gpv=" + IntToString(GetGoldPieceValue(o)) + " id=" + IntToString(GetIdentified(o)) + " plot=" + IntToString(GetPlotFlag(o))
        + " stolen=" + IntToString(GetStolenFlag(o)) + " cursed=" + IntToString(GetItemCursedFlag(o)) + " drop=" + IntToString(GetDroppableFlag(o))
        + " charges=" + IntToString(GetItemCharges(o)) + " var=" + IntToString(GetLocalInt(o, "v")) + " props=" + PIpList(o);
}

void Appearances()
{
    string s = "nw_aarcl004 nw_cloth001 nw_maarcl083 nw_wswls001 nw_wswmls002 nw_ashlw001 nw_arhe001 nw_it_mring001 nw_maarcl051 nw_it_mboots001 nw_wbwln001 nw_it_contain001";
    while (s != "")
    {
        int i = FindSubString(s, " ");
        string r = i < 0 ? s : GetSubString(s, 0, i);
        s = i < 0 ? "" : GetSubString(s, i + 1, 1000);
        PLog("appr." + r, Appearance(CreateObject(OBJECT_TYPE_ITEM, r, PCentre(0.0, -4.0))));
    }
}

void Modify(string k, string sResRef, int nType, int nIndex, int nValue)
{
    object c = GetObjectByTag("COPYCHEST");
    object o = CreateItemOnObject(sResRef, c);
    SetLocalInt(o, "v", 7);
    object m = CopyItemAndModify(o, nType, nIndex, nValue);
    PLog("mod." + k + ".valid", IntToString(GetIsObjectValid(m)) + " orig=" + IntToString(GetIsObjectValid(o)) + " same=" + IntToString(m == o));
    if (GetIsObjectValid(m))
    {
        PLog("mod." + k + ".appr", Appearance(m));
        PLog("mod." + k + ".info", Info(m));
    }
}

void Modifies()
{
    CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(4.0, 4.0), FALSE, "COPYCHEST");
}

void Modifies2()
{
    Modify("armor.torso", "nw_aarcl004", ITEM_APPR_TYPE_ARMOR_MODEL, ITEM_APPR_ARMOR_MODEL_TORSO, 3);
    Modify("armor.torso.bad", "nw_aarcl004", ITEM_APPR_TYPE_ARMOR_MODEL, ITEM_APPR_ARMOR_MODEL_TORSO, 250);
    Modify("armor.robe", "nw_aarcl004", ITEM_APPR_TYPE_ARMOR_MODEL, ITEM_APPR_ARMOR_MODEL_ROBE, 4);
    Modify("armor.belt0", "nw_aarcl004", ITEM_APPR_TYPE_ARMOR_MODEL, ITEM_APPR_ARMOR_MODEL_BELT, 0);
    Modify("armor.color", "nw_aarcl004", ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_CLOTH1, 33);
    Modify("armor.color.176", "nw_aarcl004", ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_CLOTH1, 176);
    Modify("armor.color.neg", "nw_aarcl004", ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_METAL2, -1);
    Modify("armor.partcolor", "nw_aarcl004", ITEM_APPR_TYPE_ARMOR_COLOR, 6 + 7 * 6 + 2, 50);
    Modify("armor.partcolor.clear", "nw_aarcl004", ITEM_APPR_TYPE_ARMOR_COLOR, 6 + 7 * 6 + 2, 255);
    Modify("armor.weaponmodel", "nw_aarcl004", ITEM_APPR_TYPE_WEAPON_MODEL, 0, 2);
    Modify("sword.model", "nw_wswls001", ITEM_APPR_TYPE_WEAPON_MODEL, ITEM_APPR_WEAPON_MODEL_TOP, 2);
    Modify("sword.model.bad", "nw_wswls001", ITEM_APPR_TYPE_WEAPON_MODEL, ITEM_APPR_WEAPON_MODEL_TOP, 99);
    Modify("sword.color", "nw_wswls001", ITEM_APPR_TYPE_WEAPON_COLOR, ITEM_APPR_WEAPON_COLOR_MIDDLE, 3);
    Modify("sword.color.5", "nw_wswls001", ITEM_APPR_TYPE_WEAPON_COLOR, ITEM_APPR_WEAPON_COLOR_MIDDLE, 5);
    Modify("sword.simple", "nw_wswls001", ITEM_APPR_TYPE_SIMPLE_MODEL, 0, 2);
    Modify("ring.simple", "nw_it_mring001", ITEM_APPR_TYPE_SIMPLE_MODEL, 0, 3);
    Modify("ring.simple.bad", "nw_it_mring001", ITEM_APPR_TYPE_SIMPLE_MODEL, 0, 250);
    Modify("helmet.model", "nw_arhe001", ITEM_APPR_TYPE_SIMPLE_MODEL, 0, 3);
    Modify("helmet.armormodel", "nw_arhe001", ITEM_APPR_TYPE_ARMOR_MODEL, 0, 3);
    Modify("helmet.color", "nw_arhe001", ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_METAL1, 12);
    Modify("cloak.color", "nw_maarcl051", ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_CLOTH2, 12);
    Modify("badtype", "nw_wswls001", 7, 0, 1);
}

void Copies()
{
    object c = GetObjectByTag("COPYCHEST");
    object m = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(-4.0, 4.0), FALSE, "COPYCRE");
    object o = CreateItemOnObject("nw_wswmls002", c, 1, "SRC");
    SetLocalInt(o, "v", 5);
    SetName(o, "Renamed");
    SetStolenFlag(o, TRUE);
    SetItemCursedFlag(o, TRUE);
    SetDroppableFlag(o, FALSE);
    SetIdentified(o, FALSE);
    SetItemCharges(o, 7);
    AddItemProperty(DURATION_TYPE_TEMPORARY, ItemPropertyHaste(), o, 60.0);
    PLog("copy.src", Info(o));
    PLog("copy.ground", Info(CopyItem(o)));
    PLog("copy.ground.vars", Info(CopyItem(o, OBJECT_INVALID, TRUE)));
    PLog("copy.cre", Info(CopyItem(o, m, TRUE)));
    PLog("copy.chest", Info(CopyItem(o, c)));
    object bag = CreateItemOnObject("nw_it_contain001", m, 1, "COPYBAG");
    PLog("copy.bag", Info(CopyItem(o, bag)));
    object full = CreateItemOnObject("nw_it_contain001", c, 1, "FULLBAG");
    CreateItemOnObject("nw_it_mring001", full);
    PLog("copy.fullbag", Info(CopyItem(full, c)));
    PLog("copy.emptybag", Info(CopyItem(bag, m)));
    PLog("copy.notitem", Info(CopyItem(m, c)));
    PLog("copy.intobagself", Info(CopyItem(bag, bag)));
    object ar = CreateItemOnObject("nw_wamar001", m, 30);
    object ar2 = CopyItem(ar, m);
    PLog("copy.arrows", IntToString(ar == ar2) + " " + Info(ar2));
    object eq = GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, m);
    PLog("copy.equipped", Info(CopyItem(eq, m)));
    PLog("copy.equipped.slot", GetTag(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, m)));
    object co = CopyObject(o, PCentre(2.0, 2.0), OBJECT_INVALID, "NEWTAG");
    PLog("copyobject.ground", Info(co));
    PLog("copyobject.cre", Info(CopyObject(o, GetLocation(m), m)));
    PLog("copyobject.vars", Info(CopyObject(o, GetLocation(m), c, "", TRUE)));
}

// CopyItemAndModify across value ranges: which copies exist and what they read back.
void Sweep(string k, string sResRef, int nType, int nIndex, int nFrom, int nTo, int nStep)
{
    object c = GetObjectByTag("COPYCHEST");
    object o = CreateItemOnObject(sResRef, c);
    string s = "";
    int v;
    for (v = nFrom; v <= nTo; v += nStep)
    {
        object m = CopyItemAndModify(o, nType, nIndex, v);
        s += IntToString(v) + ":" + (GetIsObjectValid(m) ? IntToString(GetItemAppearance(m, nType, nIndex)) : "x") + " ";
        DestroyObject(m);
    }
    PLog("sweep." + k, s);
}

void Sweeps()
{
    Sweep("torso", "nw_aarcl004", ITEM_APPR_TYPE_ARMOR_MODEL, ITEM_APPR_ARMOR_MODEL_TORSO, 0, 80, 1);
    Sweep("torso.hi", "nw_aarcl004", ITEM_APPR_TYPE_ARMOR_MODEL, ITEM_APPR_ARMOR_MODEL_TORSO, 180, 260, 4);
    Sweep("robe", "nw_aarcl004", ITEM_APPR_TYPE_ARMOR_MODEL, ITEM_APPR_ARMOR_MODEL_ROBE, 0, 60, 1);
    Sweep("rhand", "nw_aarcl004", ITEM_APPR_TYPE_ARMOR_MODEL, ITEM_APPR_ARMOR_MODEL_RHAND, 0, 30, 1);
    Sweep("wtop", "nw_wswls001", ITEM_APPR_TYPE_WEAPON_MODEL, ITEM_APPR_WEAPON_MODEL_TOP, -1, 30, 1);
    Sweep("wbottom", "nw_wswls001", ITEM_APPR_TYPE_WEAPON_MODEL, ITEM_APPR_WEAPON_MODEL_BOTTOM, 0, 30, 1);
    Sweep("wcolor", "nw_wswls001", ITEM_APPR_TYPE_WEAPON_COLOR, ITEM_APPR_WEAPON_COLOR_TOP, -2, 12, 1);
    Sweep("acolor", "nw_aarcl004", ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_CLOTH1, -3, 3, 1);
    Sweep("acolor.hi", "nw_aarcl004", ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_CLOTH1, 170, 260, 5);
    Sweep("pcolor", "nw_aarcl004", ITEM_APPR_TYPE_ARMOR_COLOR, 6 + 7 * 6 + 2, 170, 260, 5);
    Sweep("pcolor.neg", "nw_aarcl004", ITEM_APPR_TYPE_ARMOR_COLOR, 6 + 7 * 6 + 2, -3, 1, 1);
    Sweep("pcolor.idx", "nw_aarcl004", ITEM_APPR_TYPE_ARMOR_COLOR, 6 + 19 * 6, 5, 5, 1);
    Sweep("acolor.idx6", "nw_aarcl004", ITEM_APPR_TYPE_ARMOR_COLOR, 6, 5, 5, 1);
    Sweep("ring", "nw_it_mring001", ITEM_APPR_TYPE_SIMPLE_MODEL, 0, -1, 260, 13);
    Sweep("ring.big", "nw_it_mring001", ITEM_APPR_TYPE_SIMPLE_MODEL, 0, 255, 300, 45);
    Sweep("helmet", "nw_arhe001", ITEM_APPR_TYPE_SIMPLE_MODEL, 0, -1, 40, 1);
    Sweep("helmet.am", "nw_arhe001", ITEM_APPR_TYPE_ARMOR_MODEL, 5, 0, 12, 1);
    Sweep("cloak", "nw_maarcl051", ITEM_APPR_TYPE_SIMPLE_MODEL, 0, 0, 3, 1);
    Sweep("sword.armor", "nw_wswls001", ITEM_APPR_TYPE_ARMOR_MODEL, 0, 1, 1, 1);
    Sweep("sword.acolor", "nw_wswls001", ITEM_APPR_TYPE_ARMOR_COLOR, 0, 1, 1, 1);
    Sweep("ring.wmodel", "nw_it_mring001", ITEM_APPR_TYPE_WEAPON_MODEL, 0, 1, 1, 1);
    Sweep("ring.acolor", "nw_it_mring001", ITEM_APPR_TYPE_ARMOR_COLOR, 0, 1, 1, 1);
    Sweep("armor.simple", "nw_aarcl004", ITEM_APPR_TYPE_SIMPLE_MODEL, 0, 1, 3, 1);
    Sweep("shield", "nw_ashlw001", ITEM_APPR_TYPE_SIMPLE_MODEL, 0, 10, 14, 1);
    Sweep("shield.wm", "nw_ashlw001", ITEM_APPR_TYPE_WEAPON_MODEL, 0, 1, 3, 1);
    Sweep("boots.wm", "nw_it_mboots001", ITEM_APPR_TYPE_WEAPON_MODEL, 0, 1, 3, 1);
    Sweep("bow.wm", "nw_wbwln001", ITEM_APPR_TYPE_WEAPON_MODEL, 1, 1, 3, 1);
}

// Items through ObjectToJson/JsonToObject: property fields and order (the original writes cast spells first).
void JsonRoundTrip()
{
    object o = CreateObject(OBJECT_TYPE_ITEM, "nw_wswls001", PCentre(0.0, -6.0));
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyKeen(), o);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyCastSpell(IP_CONST_CASTSPELL_HASTE_10, IP_CONST_CASTSPELL_NUMUSES_3_USES_PER_DAY), o);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyVisualEffect(ITEM_VISUAL_FIRE), o);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyDamageBonusVsAlign(IP_CONST_ALIGNMENTGROUP_EVIL, IP_CONST_DAMAGETYPE_FIRE, IP_CONST_DAMAGEBONUS_1d6), o);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyCastSpell(IP_CONST_CASTSPELL_LIGHT_5, IP_CONST_CASTSPELL_NUMUSES_UNLIMITED_USE), o);
    PLog("json.before", PIpList(o));
    json j = ObjectToJson(o);
    json jp = JsonArrayGet(JsonObjectGet(JsonObjectGet(j, "PropertiesList"), "value"), 1);
    string f = "";
    string sKeys = "ChanceAppear CostTable CostValue CustomTag Param1 Param1Value PropertyName Subtype Useable UsesPerDay";
    while (sKeys != "")
    {
        int i = FindSubString(sKeys, " ");
        string key = i < 0 ? sKeys : GetSubString(sKeys, 0, i);
        sKeys = i < 0 ? "" : GetSubString(sKeys, i + 1, 1000);
        json v = JsonObjectGet(jp, key);
        f += key + "=" + JsonGetString(JsonObjectGet(v, "type")) + ":" + JsonDump(JsonObjectGet(v, "value")) + " ";
    }
    PLog("json.prop1", f);
    object o2 = JsonToObject(j, PCentre(0.0, -6.0));
    PLog("json.after", PIpList(o2));
    string s = "";
    itemproperty ip = GetFirstItemProperty(o2);
    while (GetIsItemPropertyValid(ip)) { s += IntToString(GetItemPropertyUsesPerDayRemaining(o2, ip)) + " "; ip = GetNextItemProperty(o2); }
    PLog("json.after.uses", s);
    PLogI("json.after.gpv", GetGoldPieceValue(o2));
}

void Finish() { PDone(); }

void main()
{
    DelayCommand(1.0, Appearances());
    DelayCommand(1.2, Modifies());
    DelayCommand(1.5, Modifies2());
    DelayCommand(1.8, Copies());
    DelayCommand(2.2, Sweeps());
    DelayCommand(2.4, JsonRoundTrip());
    DelayCommand(3.0, Finish());
}
