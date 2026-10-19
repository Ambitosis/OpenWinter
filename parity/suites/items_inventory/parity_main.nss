// CreateItemOnObject: stacking, overflowing stacks, full inventories, containers; weights of stacks,
// containers and creatures; CopyItem into inventories.
#include "parity_items"

// Every item an object holds, as "resref:stack" in iteration order (containers' contents in brackets).
string Contents(object oHolder)
{
    string s = "";
    object o = GetFirstItemInInventory(oHolder);
    while (GetIsObjectValid(o))
    {
        s += GetResRef(o) + ":" + IntToString(GetItemStackSize(o));
        if (GetHasInventory(o)) s += "[" + Contents(o) + "]";
        s += " ";
        o = GetNextItemInInventory(oHolder);
    }
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

object Chest(string sTag, float fDx)
{
    object o = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(fDx, 8.0), FALSE, sTag);
    return o;
}

void Stacking()
{
    object c = Chest("STACK", -6.0);
    object a = CreateItemOnObject("nw_wamar001", c, 10);
    PLog("stack.a", Where(a) + " " + IntToString(GetItemStackSize(a)));
    object b = CreateItemOnObject("nw_wamar001", c, 20);
    PLog("stack.b", Where(b) + " " + IntToString(GetItemStackSize(b)) + " same=" + IntToString(a == b));
    PLog("stack.ab", Contents(c));
    object d = CreateItemOnObject("nw_wamar001", c, 95);
    PLog("stack.d", Where(d) + " " + IntToString(GetItemStackSize(d)) + " a=" + IntToString(d == a));
    PLog("stack.abd", Contents(c));
    object e = CreateItemOnObject("nw_wamar001", c, 250);
    PLog("stack.e", Where(e) + " " + IntToString(GetItemStackSize(e)));
    PLog("stack.abde", Contents(c));
    object f = CreateItemOnObject("nw_wamar001", c, 0);
    PLog("stack.zero", Where(f) + " " + IntToString(GetItemStackSize(f)));
    object g = CreateItemOnObject("nw_wamar001", c, -5);
    PLog("stack.neg", Where(g) + " " + IntToString(GetItemStackSize(g)));
    object h = CreateItemOnObject("nw_wswls001", c, 5);
    PLog("stack.sword5", Where(h) + " " + IntToString(GetItemStackSize(h)));
    PLog("stack.all", Contents(c));
    PLogI("stack.weight", GetWeight(c));

    // Stacks merge only with matching items: tag, properties, identified, stolen, plot.
    object c2 = Chest("MERGE", -3.0);
    object p1 = CreateItemOnObject("nw_it_mpotion001", c2, 2);
    object p2 = CreateItemOnObject("nw_it_mpotion001", c2, 2, "OTHERTAG");
    PLog("merge.tag", IntToString(p1 == p2) + " " + Contents(c2));
    SetStolenFlag(p1, TRUE);
    object p3 = CreateItemOnObject("nw_it_mpotion001", c2, 1);
    PLog("merge.stolen", IntToString(p3 == p1) + " " + Contents(c2));
    object p4 = CreateItemOnObject("nw_it_mpotion002", c2, 1);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyCastSpell(IP_CONST_CASTSPELL_LIGHT_5, IP_CONST_CASTSPELL_NUMUSES_SINGLE_USE), p4);
    object p5 = CreateItemOnObject("nw_it_mpotion002", c2, 1);
    PLog("merge.props", IntToString(p5 == p4) + " " + Contents(c2));
    SetIdentified(p5, FALSE);
    object p6 = CreateItemOnObject("nw_it_mpotion002", c2, 1);
    PLog("merge.unid", IntToString(p6 == p5) + " " + Contents(c2));
    object p7 = CreateItemOnObject("nw_it_mpotion003", c2, 1);
    SetPlotFlag(p7, TRUE);
    object p8 = CreateItemOnObject("nw_it_mpotion003", c2, 1);
    PLog("merge.plot", IntToString(p8 == p7) + " " + Contents(c2));
    SetLocalInt(p8, "x", 1);
    object p9 = CreateItemOnObject("nw_it_mpotion003", c2, 1);
    PLog("merge.local", IntToString(p9 == p8) + " " + Contents(c2));
    object r1 = CreateItemOnObject("nw_it_mpotion004", c2, 1);
    SetName(r1, "Renamed");
    object r2 = CreateItemOnObject("nw_it_mpotion004", c2, 1);
    PLog("merge.name", IntToString(r2 == r1) + " " + GetName(r2));
    object s1 = CreateItemOnObject("nw_it_mpotion005", c2, 1);
    SetItemCursedFlag(s1, TRUE);
    object s2 = CreateItemOnObject("nw_it_mpotion005", c2, 1);
    PLog("merge.cursed", IntToString(s2 == s1));
    object t1 = CreateItemOnObject("nw_it_mpotion006", c2, 1);
    SetDroppableFlag(t1, FALSE);
    object t2 = CreateItemOnObject("nw_it_mpotion006", c2, 1);
    PLog("merge.droppable", IntToString(t2 == t1));
    object u1 = CreateItemOnObject("nw_it_mpotion007", c2, 1);
    SetItemCharges(u1, 3);
    object u2 = CreateItemOnObject("nw_it_mpotion007", c2, 1);
    PLog("merge.charges", IntToString(u2 == u1));
    object v1 = CreateItemOnObject("nw_it_mpotion008", c2, 1);
    itemproperty ipv = GetFirstItemProperty(v1);
    RemoveItemProperty(v1, ipv);
    object v2 = CreateItemOnObject("nw_it_mpotion008", c2, 1);
    PLog("merge.removedprop", IntToString(v2 == v1) + " " + PIpList(v1));
    object w1 = CreateItemOnObject("nw_it_mpotion009", c2, 1);
    AddItemProperty(DURATION_TYPE_TEMPORARY, ItemPropertyCastSpell(IP_CONST_CASTSPELL_LIGHT_5, IP_CONST_CASTSPELL_NUMUSES_SINGLE_USE), w1, 60.0);
    object w2 = CreateItemOnObject("nw_it_mpotion009", c2, 1);
    PLog("merge.tempprop", IntToString(w2 == w1));
    // Copies merge too.
    object q = CopyItem(p1, c2);
    PLog("merge.copy", IntToString(q == p1) + " " + Where(q) + " " + Contents(c2));
}

void Fill(object oTarget, string sResRef, string k, int nMax)
{
    int i;
    int nIn = 0;
    string sFirstOut = "";
    for (i = 0; i < nMax; i++)
    {
        object o = CreateItemOnObject(sResRef, oTarget);
        string w = Where(o);
        if (w == "possessor:" + GetTag(oTarget)) nIn++;
        else if (sFirstOut == "") sFirstOut = IntToString(i) + ":" + w;
    }
    PLogI(k + ".in", nIn);
    PLog(k + ".firstout", sFirstOut);
    PLogI(k + ".count", PCountItems(oTarget));
    PLogI(k + ".fits", GetBaseItemFitsInInventory(BASE_ITEM_RING, oTarget));
    PLogI(k + ".fits.tower", GetBaseItemFitsInInventory(BASE_ITEM_TOWERSHIELD, oTarget));
}

void Full()
{
    object c = Chest("FULLRING", 0.0);
    PLogI("full.empty.fits", GetBaseItemFitsInInventory(BASE_ITEM_TOWERSHIELD, c));
    Fill(c, "nw_it_mring001", "full.chest.ring", 400);
    object t = Chest("FULLTOWER", 3.0);
    Fill(t, "nw_ashto001", "full.chest.tower", 60);
    Fill(GetObjectByTag("FULLCRE"), "nw_ashto001", "full.cre.tower", 60);
    Fill(GetObjectByTag("FULLCRE2"), "nw_it_mring001", "full.cre.ring", 400);
}

void Containers()
{
    object m = GetObjectByTag("BAGCRE");
    PLogI("bag.cre.weight0", GetWeight(m));
    object bag = CreateItemOnObject("nw_it_contain001", m);
    object bag2 = CreateItemOnObject("nw_it_contain006", m);
    PLogI("bag.cre.weight1", GetWeight(m));
    object x = CreateItemOnObject("nw_ashto001", bag);
    PLog("bag.tower", Where(x));
    PLogI("bag.weight", GetWeight(bag));
    object y = CreateItemOnObject("nw_ashto001", bag2);
    PLogI("bag2.weight", GetWeight(bag2));
    object z = CreateItemOnObject("nw_wamar001", bag, 50);
    PLog("bag.arrows", Where(z) + " " + IntToString(GetItemStackSize(z)));
    object inner = CreateItemOnObject("nw_it_contain001", bag);
    PLog("bag.inbag", Where(inner));
    PLog("bag.contents", Contents(bag));
    PLog("cre.contents", Contents(m));
    PLogI("bag.cre.weight2", GetWeight(m));
    // A stack in the creature merges into the bag's stack? Arrows into the creature itself.
    object w = CreateItemOnObject("nw_wamar001", m, 10);
    PLog("bag.arrows.cre", Where(w) + " " + IntToString(GetItemStackSize(w)) + " same=" + IntToString(w == z));
    // Containers with reduced weight: one of each.
    int i;
    for (i = 1; i <= 6; i++)
    {
        object b = CreateItemOnObject("nw_it_contain00" + IntToString(i), m);
        CreateItemOnObject("nw_ashto001", b);
        CreateItemOnObject("nw_wswls001", b);
        PLogI("bagweight." + IntToString(i), GetWeight(b));
    }
    PLogI("bag.cre.weight3", GetWeight(m));
    PLogI("bag.fits.bag", GetBaseItemFitsInInventory(BASE_ITEM_LARGEBOX, bag));
    PLogI("bag.fits.ring", GetBaseItemFitsInInventory(BASE_ITEM_RING, bag));
    // Fill a bag.
    object c = GetObjectByTag("BAGFILL");
    object fb = CreateItemOnObject("nw_it_contain001", c);
    SetTag(fb, "FILLBAG");
    Fill(fb, "nw_ashto001", "full.bag.tower", 20);
    Fill(fb, "nw_it_mring001", "full.bag.ring", 200);
}

void Weights()
{
    object m = GetObjectByTag("WCRE");
    PLogI("w.cre", GetWeight(m));
    object a = CreateItemOnObject("nw_wamar001", m, 99);
    PLogI("w.arrows99", GetWeight(a));
    object t = CreateItemOnObject("nw_wthdt001", m, 50);
    PLogI("w.darts50", GetWeight(t));
    object p = CreateItemOnObject("nw_it_mpotion001", m, 10);
    PLogI("w.pot10", GetWeight(p));
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyWeightIncrease(IP_CONST_WEIGHTINCREASE_5_LBS), p);
    PLog("w.pot10.inc.props", PIpList(p));
    PLogI("w.pot10.inc", GetWeight(p));
    object s = CreateItemOnObject("nw_wswls001", m);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyWeightReduction(IP_CONST_REDUCEDWEIGHT_60_PERCENT), s);
    PLogI("w.ls.red", GetWeight(s));
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyWeightReduction(IP_CONST_REDUCEDWEIGHT_20_PERCENT), s);
    PLogI("w.ls.red2", GetWeight(s));
    object s3 = CreateItemOnObject("nw_wswls001", m);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyWeightReduction(IP_CONST_REDUCEDWEIGHT_20_PERCENT), s3);
    PLogI("w.ls.red3a", GetWeight(s3));
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyWeightReduction(IP_CONST_REDUCEDWEIGHT_60_PERCENT), s3);
    PLogI("w.ls.red3b", GetWeight(s3));
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyWeightReduction(IP_CONST_REDUCEDWEIGHT_80_PERCENT), s3);
    PLogI("w.ls.red3c", GetWeight(s3));
    object s4 = CreateItemOnObject("nw_ashto001", m);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyWeightIncrease(IP_CONST_WEIGHTINCREASE_50_LBS), s4);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyWeightReduction(IP_CONST_REDUCEDWEIGHT_10_PERCENT), s4);
    PLogI("w.tower.incred", GetWeight(s4));
    object s5 = CreateItemOnObject("nw_ashto001", m);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyWeightReduction(IP_CONST_REDUCEDWEIGHT_10_PERCENT), s5);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyWeightIncrease(IP_CONST_WEIGHTINCREASE_50_LBS), s5);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyWeightIncrease(IP_CONST_WEIGHTINCREASE_5_LBS), s5);
    PLogI("w.tower.redinc", GetWeight(s5));
    object s6 = CreateItemOnObject("nw_wthdt001", m, 50);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyWeightReduction(IP_CONST_REDUCEDWEIGHT_60_PERCENT), s6);
    PLogI("w.darts.red", GetWeight(s6));
    object s2 = CreateItemOnObject("nw_wswls001", m);
    AddItemProperty(DURATION_TYPE_TEMPORARY, ItemPropertyWeightReduction(IP_CONST_REDUCEDWEIGHT_10_PERCENT), s2, 60.0);
    PLogI("w.ls.redtemp", GetWeight(s2));
    PLogI("w.cre.after", GetWeight(m));
    PLogI("w.none", GetWeight(OBJECT_INVALID));
    PLogI("w.chest", GetWeight(GetObjectByTag("STACK")));
}

// Inventories loaded from blueprints: iteration order and equipment.
void Loaded()
{
    string s = "nw_hen_bod_01 nw_hen_dae_05 nw_hen_gal_11 nw_hen_gri_03 nw_hen_lin_07 nw_hen_sha_09";
    int n = 0;
    while (s != "")
    {
        int i = FindSubString(s, " ");
        string r = i < 0 ? s : GetSubString(s, 0, i);
        s = i < 0 ? "" : GetSubString(s, i + 1, 1000);
        object m = CreateObject(OBJECT_TYPE_CREATURE, r, PCentre(-9.0 + IntToFloat(n), 9.0));
        n++;
        PLog("loaded." + r + ".contents", Contents(m));
        string e = "";
        int slot;
        for (slot = 0; slot < NUM_INVENTORY_SLOTS; slot++)
        {
            object o = GetItemInSlot(slot, m);
            if (GetIsObjectValid(o)) e += IntToString(slot) + ":" + GetResRef(o) + " ";
        }
        PLog("loaded." + r + ".equipped", e);
        int nAmmo;
        for (nAmmo = INVENTORY_SLOT_ARROWS; nAmmo <= INVENTORY_SLOT_BOLTS; nAmmo++)
        {
            object a = GetItemInSlot(nAmmo, m);
            if (!GetIsObjectValid(a)) continue;
            string k = "loaded." + r + ".ammo" + IntToString(nAmmo) + ".";
            PLogI(k + "stack", GetItemStackSize(a));
            PLogI(k + "droppable", GetDroppableFlag(a));
            PLogI(k + "plot", GetPlotFlag(a));
            PLogI(k + "ident", GetIdentified(a));
            PLog(k + "tag", GetTag(a));
            PLogI(k + "gpv", GetGoldPieceValue(a));
        }
    }
    // A launcher equipped later.
    object b = CreateObject(OBJECT_TYPE_CREATURE, "nw_guard", PCentre(6.0, 9.0), FALSE, "LATEBOW");
    SetAILevel(b, AI_LEVEL_NORMAL);
    object b2 = CreateObject(OBJECT_TYPE_CREATURE, "nw_guard", PCentre(7.0, 9.0), FALSE, "LATESLING");
    SetAILevel(b2, AI_LEVEL_NORMAL);
}

// After the spawn scripts have run (they clear the action queue).
void LateEquip()
{
    object b = GetObjectByTag("LATEBOW");
    SetAILevel(b, AI_LEVEL_NORMAL);
    object bow = CreateItemOnObject("nw_wbwln001", b);
    AssignCommand(b, ActionEquipItem(bow, INVENTORY_SLOT_RIGHTHAND));
    object b2 = GetObjectByTag("LATESLING");
    SetAILevel(b2, AI_LEVEL_NORMAL);
    object sling = CreateItemOnObject("nw_wbwsl001", b2);
    AssignCommand(b2, ActionEquipItem(sling, INVENTORY_SLOT_RIGHTHAND));
}

void LateAmmo()
{
    object b = GetObjectByTag("LATEBOW");
    PLog("late.bow.right", GetResRef(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, b)));
    PLog("late.bow.arrows", GetResRef(GetItemInSlot(INVENTORY_SLOT_ARROWS, b)));
    object b2 = GetObjectByTag("LATESLING");
    PLog("late.sling.right", GetResRef(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, b2)));
    PLog("late.sling.bullets", GetResRef(GetItemInSlot(INVENTORY_SLOT_BULLETS, b2)));
}

// Items beyond a full backpack: possessed but not listed, and they take room that opens up.
void Overflow()
{
    object m = GetObjectByTag("OVERFLOW");
    int i;
    for (i = 0; i < 30; i++) CreateItemOnObject("nw_ashto001", m);
    object o1 = CreateItemOnObject("nw_ashto001", m);
    object o2 = CreateItemOnObject("nw_it_mring001", m);
    PLog("over.o1", Where(o1) + " area=" + IntToString(GetIsObjectValid(GetArea(o1))));
    PLog("over.o2", Where(o2));
    PLogI("over.count", PCountItems(m));
    PLog("over.bytag", ObjectToString(GetItemPossessedBy(m, "NW_IT_MRING001")) == ObjectToString(o2) ? "found" : "not");
    object o3 = CreateItemOnObject("nw_wamar001", m, 20);
    object o4 = CreateItemOnObject("nw_wamar001", m, 20);
    PLog("over.arrows", Where(o3) + " same=" + IntToString(o3 == o4) + " " + IntToString(GetItemStackSize(o3)));
    DestroyObject(GetFirstItemInInventory(m));
}

void Overflow2()
{
    object m = GetObjectByTag("OVERFLOW");
    PLogI("over.count2", PCountItems(m));
    PLog("over.contents2", GetSubString(Contents(m), 0, 80));
}

// Creatures for the tests, without the random treasure their spawn scripts may give.
void Spawn(string sTag, float fDx, float fDy)
{
    CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(fDx, fDy), FALSE, sTag);
}

void Strip(string sTag)
{
    object m = GetObjectByTag(sTag);
    object o = GetFirstItemInInventory(m);
    while (GetIsObjectValid(o)) { DestroyObject(o); o = GetNextItemInInventory(m); }
}

// Spawn scripts run after CreateObject returns, so their treasure goes after a moment.
void StripAll()
{
    Strip("FULLCRE"); Strip("FULLCRE2"); Strip("BAGCRE"); Strip("BAGFILL"); Strip("WCRE"); Strip("OVERFLOW");
}

void SpawnAll()
{
    Spawn("FULLCRE", 0.0, -6.0);
    Spawn("FULLCRE2", 3.0, -6.0);
    Spawn("BAGCRE", 6.0, -6.0);
    Spawn("BAGFILL", 9.0, -6.0);
    Spawn("WCRE", -6.0, -6.0);
    Spawn("OVERFLOW", 0.0, -9.0);
}

void Finish() { PDone(); }

void main()
{
    DelayCommand(0.3, SpawnAll());
    DelayCommand(0.7, StripAll());
    DelayCommand(1.0, Stacking());
    DelayCommand(1.5, Full());
    DelayCommand(2.0, Containers());
    DelayCommand(2.5, Weights());
    DelayCommand(2.6, Loaded());
    DelayCommand(2.7, Overflow());
    DelayCommand(3.2, Overflow2());
    DelayCommand(4.0, LateEquip());
    DelayCommand(6.0, LateAmmo());
    DelayCommand(6.5, Finish());
}
