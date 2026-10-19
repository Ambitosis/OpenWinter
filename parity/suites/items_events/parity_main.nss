// Which module item events fire for creatures that are not players, and when.
// - Template items of a creature placed in the area (before OnModuleLoad), made by CreateObject, CopyObject,
//   SqlGetObject, RetrieveCampaignObject and JsonToObject: acquire events, their order against OnSpawn,
//   and what GetModuleItemAcquired* return in them.
// - Creating, equipping, unequipping, destroying, giving, dropping, picking up and copying items on NPCs,
//   and NPCs using Unique Power items (OnActivateItem comes from the spell script).
// The event scripts (pe_*) append to a log on the module; PEvLog writes it out.
#include "parity_itemev"

object Spawn(string sRes, string sTag, location l)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, l, FALSE, sTag);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

void Marker(string s) { PEvAdd("[" + s + "]"); }

// ---------------------------------------------------------------- spawning and loading creatures

void Loaded(string k, object o)
{
    PEvAdd("[" + k + " valid=" + IntToString(GetIsObjectValid(o)) + " tag=" + PEvName(o) + "]");
    DelayCommand(0.0, Marker(k + "+0"));
    DelayCommand(0.3, PEvLog("spawn." + k));
    DelayCommand(0.31, PLog("spawn." + k + ".items", PEvItems(o)));
}

void Create()
{
    Marker("create");
    object o = Spawn("pe_lin", "LIN_C", PCentre(-6.0, 6.0));
    Loaded("create", o);
}

// A bag with a ring in it, so the copies and loads show whether a bag's contents get acquire events.
void Bag()
{
    object bag = CreateItemOnObject("nw_it_contain001", GetObjectByTag("LIN_C"), 1, "BAGC");
    CreateItemOnObject("nw_it_mring001", bag, 1, "BAGCR");
    DelayCommand(0.1, PEvLog("bagc"));
}

void Copy()
{
    Marker("copy");
    object o = CopyObject(GetObjectByTag("LIN_C"), PCentre(-4.0, 6.0), OBJECT_INVALID, "LIN_K");
    Loaded("copy", o);
}

void Sql()
{
    object src = GetObjectByTag("LIN_C");
    SetTag(src, "LIN_S");
    sqlquery q = SqlPrepareQueryObject(GetModule(), "CREATE TABLE IF NOT EXISTS pe (o BLOB)");
    SqlStep(q);
    q = SqlPrepareQueryObject(GetModule(), "INSERT INTO pe (o) VALUES (@o)");
    SqlBindObject(q, "@o", src);
    SqlStep(q);
    SetTag(src, "LIN_C");
    Marker("sql");
    q = SqlPrepareQueryObject(GetModule(), "SELECT o FROM pe");
    object o = SqlStep(q) ? SqlGetObject(q, 0, PCentre(-2.0, 6.0)) : OBJECT_INVALID;
    Loaded("sql", o);
}

void Campaign()
{
    object src = GetObjectByTag("LIN_C");
    SetTag(src, "LIN_D");
    StoreCampaignObject("pe_db", "o", src);
    SetTag(src, "LIN_C");
    Marker("camp");
    object o = RetrieveCampaignObject("pe_db", "o", PCentre(0.0, 6.0));
    Loaded("camp", o);
}

// The tags of a creature's JSON item list, in order.
string JsonTags(json j, string sList)
{
    json l = JsonObjectGet(JsonObjectGet(j, sList), "value");
    string s = "";
    int i;
    for (i = 0; i < JsonGetLength(l); i++)
        s += JsonGetString(JsonObjectGet(JsonObjectGet(JsonArrayGet(l, i), "Tag"), "value")) + " ";
    return s;
}

void Json()
{
    object src = GetObjectByTag("LIN_C");
    SetTag(src, "LIN_J");
    json j = ObjectToJson(src);
    SetTag(src, "LIN_C");
    PLog("json.itemlist", JsonTags(j, "ItemList"));
    PLog("json.equiplist", JsonTags(j, "Equip_ItemList"));
    Marker("json");
    object o = JsonToObject(j, PCentre(2.0, 6.0));
    Loaded("json", o);
}

// Items alone: copied, loaded and made from JSON straight into a creature's inventory.
void ItemLoads()
{
    object a = GetObjectByTag("LIN_C");
    object it = CreateItemOnObject("nw_it_mring001", a, 1, "IL_SRC");
    PEvFlush();
    Marker("itemcopy");
    CopyObject(it, GetLocation(a), a, "IL_COPY");
    Marker("itemsql");
    sqlquery q = SqlPrepareQueryObject(GetModule(), "CREATE TABLE IF NOT EXISTS pi (o BLOB)");
    SqlStep(q);
    q = SqlPrepareQueryObject(GetModule(), "INSERT INTO pi (o) VALUES (@o)");
    SqlBindObject(q, "@o", it);
    SqlStep(q);
    q = SqlPrepareQueryObject(GetModule(), "SELECT o FROM pi");
    if (SqlStep(q)) SqlGetObject(q, 0, GetLocation(a), a);
    Marker("itemcamp");
    StoreCampaignObject("pe_db", "i", it);
    RetrieveCampaignObject("pe_db", "i", GetLocation(a), a);
    Marker("itemjson");
    JsonToObject(ObjectToJson(it), GetLocation(a), a);
    Marker("itemcopyitem");
    CopyItem(it, a, TRUE);
    Marker("end");
    DelayCommand(0.3, PEvLog("itemload"));
}

// ---------------------------------------------------------------- NPCs handling items

object Item(string sRes, object o, string sTag) { return CreateItemOnObject(sRes, o, 1, sTag); }

void Act(object o, string sKey, int bSelf)
{
    object it = GetObjectByTag(sKey);
    itemproperty ip = GetFirstItemProperty(it);
    AssignCommand(o, ActionUseItemOnObject(it, ip, bSelf ? o : GetObjectByTag("NPC_B")));
}

object Unique(object o, string sTag, int nSpell)
{
    object it = Item("nw_it_gem001", o, sTag);
    itemproperty ip = GetFirstItemProperty(it);
    while (GetIsItemPropertyValid(ip)) { RemoveItemProperty(it, ip); ip = GetNextItemProperty(it); }
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyCastSpell(nSpell, IP_CONST_CASTSPELL_NUMUSES_UNLIMITED_USE), it);
    PLogI("unique." + sTag + ".ident", GetIdentified(it));
    SetIdentified(it, TRUE);
    return it;
}

void Step(int n)
{
    object a = GetObjectByTag("NPC_A");
    object b = GetObjectByTag("NPC_B");
    object chest = GetObjectByTag("PE_CHEST");
    if (n == 1) { Item("nw_it_mring001", a, "RING1"); Item("nw_wswdg001", a, "DAG1"); Item("nw_wamar001", a, "ARR1"); }
    else if (n == 2) AssignCommand(a, ActionEquipItem(GetObjectByTag("RING1"), INVENTORY_SLOT_RIGHTRING));
    else if (n == 3) AssignCommand(a, ActionUnequipItem(GetObjectByTag("RING1")));
    else if (n == 4) AssignCommand(a, ActionEquipItem(GetObjectByTag("DAG1"), INVENTORY_SLOT_RIGHTHAND)); // replaces the longsword
    else if (n == 5) DestroyObject(GetObjectByTag("RING1"));
    else if (n == 6) { Item("nw_it_mring001", a, "RING2"); AssignCommand(a, ActionGiveItem(GetObjectByTag("RING2"), b)); }
    else if (n == 7) AssignCommand(b, ActionPutDownItem(GetObjectByTag("RING2")));
    else if (n == 8) AssignCommand(a, ActionPickUpItem(GetObjectByTag("RING2")));
    else if (n == 9) { CopyItem(GetObjectByTag("RING2"), b, TRUE); CopyObject(GetObjectByTag("RING2"), GetLocation(b), b, "RING2C"); }
    else if (n == 10) DestroyObject(GetObjectByTag("DAG1"));                                      // equipped
    else if (n == 11) AssignCommand(b, ActionTakeItem(GetObjectByTag("RING2"), a));
    else if (n == 12) AssignCommand(b, ActionGiveItem(GetObjectByTag("RING2"), chest));
    else if (n == 13) AssignCommand(a, ActionTakeItem(GetObjectByTag("RING2"), chest));
    else if (n == 14) { object bag = Item("nw_it_contain001", a, "BAG1"); CreateItemOnObject("nw_it_mring002", bag, 1, "BAGRING"); }
    else if (n == 15) AssignCommand(a, ActionGiveItem(GetObjectByTag("BAGRING"), b));            // out of a bag
    else if (n == 16) AssignCommand(a, ActionEquipItem(GetObjectByTag("ARR1"), INVENTORY_SLOT_ARROWS));
    else if (n == 17) { SetItemStackSize(GetObjectByTag("ARR1"), 5); Item("nw_wamar001", a, "ARR1"); } // merges into the stack
    else if (n == 18) { Unique(a, "ACT1", IP_CONST_CASTSPELL_UNIQUE_POWER); Unique(a, "ACT2", IP_CONST_CASTSPELL_UNIQUE_POWER_SELF_ONLY); }
    else if (n == 19) Act(a, "ACT1", FALSE);
    else if (n == 20) Act(a, "ACT2", TRUE);
    else if (n == 21) AssignCommand(a, ActionUseItemAtLocation(GetObjectByTag("ACT1"), GetFirstItemProperty(GetObjectByTag("ACT1")), GetLocation(b)));
    else if (n == 22) AssignCommand(a, ActionGiveItem(GetItemInSlot(INVENTORY_SLOT_CHEST, a), b)); // equipped armour
    else if (n == 23) DestroyObject(GetObjectByTag("NPC_C"));                                   // carrying things
    else if (n == 24) AssignCommand(a, ClearAllActions());
    else if (n == 25)
    {
        // Did the NPC use the item at all? A Light property on B, then the event signalled by hand.
        object it = Unique(a, "ACT3", IP_CONST_CASTSPELL_LIGHT_5);
        AssignCommand(a, ActionUseItemOnObject(it, GetFirstItemProperty(it), b));
        DelayCommand(0.1, PLogI("act.light.action", GetCurrentAction(a)));
        DelayCommand(1.0, PLogI("act.light.action1", GetCurrentAction(a)));
    }
    else if (n == 26)
    {
        PLogI("act.light", GetHasSpellEffect(SPELL_LIGHT, b));
        PLogI("act.light.self", GetHasSpellEffect(SPELL_LIGHT, a));
        AssignCommand(a, SignalEvent(GetModule(), EventActivateItem(GetObjectByTag("ACT1"), GetLocation(b), b)));
    }
    else if (n == 27) SignalEvent(GetModule(), EventActivateItem(GetObjectByTag("ACT1"), GetLocation(b), b));
    else if (n == 28) { Item("nw_it_creitem005", a, "HIDE1"); AssignCommand(a, ActionEquipItem(GetObjectByTag("HIDE1"), INVENTORY_SLOT_CARMOUR)); }
    else if (n == 29) { PLog("hide.slot", PEvName(GetItemInSlot(INVENTORY_SLOT_CARMOUR, a))); DestroyObject(GetObjectByTag("HIDE1")); }   // a creature hide
}

void Run(int n)
{
    Step(n);
    float fWait = (n >= 19 && n <= 21) || n == 25 ? 6.0 : 0.8;
    DelayCommand(fWait - 0.1, PEvLog("npc." + IntToString(n)));
    if (n < 29) DelayCommand(fWait, Run(n + 1));
    else
    {
        DelayCommand(1.0, PLog("npc.a.items", PEvItems(GetObjectByTag("NPC_A"))));
        DelayCommand(1.2, PDone());
    }
}

void Npcs()
{
    PEvFlush();
    Marker("npcs");
    object a = Spawn("nw_bandit001", "NPC_A", PCentre(4.0, -4.0));
    object b = Spawn("nw_bandit001", "NPC_B", PCentre(5.0, -4.0));
    object c = Spawn("nw_bandit001", "NPC_C", PCentre(6.0, -4.0));
    CreateItemOnObject("nw_it_mring001", c, 1, "C_RING");
    int i;
    for (i = 5000; i <= 5012; i++) { SetEventScript(a, i, ""); SetEventScript(b, i, ""); SetEventScript(c, i, ""); }
    CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(4.5, -3.0), FALSE, "PE_CHEST");
    DelayCommand(0.5, PEvLog("npc.spawn"));
    DelayCommand(0.6, Run(1));
}

void main()
{
    PEvLog("load.before");
    PLog("load.git.items", PEvItems(GetObjectByTag("LIN_G")));
    Marker("load");
    DelayCommand(0.0, Marker("load+0"));
    DelayCommand(0.5, PEvLog("load.after"));
    DelayCommand(1.0, Create());
    DelayCommand(1.6, Bag());
    DelayCommand(2.0, Copy());
    DelayCommand(3.0, Sql());
    DelayCommand(4.0, Campaign());
    DelayCommand(5.0, Json());
    DelayCommand(6.0, ItemLoads());
    DelayCommand(7.0, Npcs());
}
