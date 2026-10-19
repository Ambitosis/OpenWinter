// GetGoldPieceValue of charged and stacked items: wands, scrolls and potions, with charges, stacks, cast spell
// properties added in different orders, limitation properties, identification and unidentified items.
#include "parity_items"

object Chest() { return GetObjectByTag("VALCHEST"); }

// A chest of its own, so new stacks don't merge into earlier ones.
object NewChest() { return CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(4.0, -4.0)); }

void Strip(object o)
{
    itemproperty ip = GetFirstItemProperty(o);
    while (GetIsItemPropertyValid(ip)) { RemoveItemProperty(o, ip); ip = GetNextItemProperty(o); }
}

string V(object o) { return IntToString(GetGoldPieceValue(o)); }

// Logged later, after the item has been repriced at the end of the script.
void Later(string k, object o)
{
    PLog(k + ".later", V(o) + " c" + IntToString(GetItemCharges(o)) + " s" + IntToString(GetItemStackSize(o)) + " i" + IntToString(GetIdentified(o)));
}

void Keep(string k, object o) { DelayCommand(0.3, Later(k, o)); }

// A stock blueprint, then charges set to several values (read at once and later).
void StockCharges(string sResRef)
{
    int i;
    for (i = 0; i < 7; i++)
    {
        int n = i == 0 ? 0 : i == 1 ? 1 : i == 2 ? 3 : i == 3 ? 10 : i == 4 ? 25 : i == 5 ? 49 : 50;
        object o = CreateItemOnObject(sResRef, Chest());
        string k = "stock." + sResRef + "." + IntToString(n);
        string s = V(o) + " c" + IntToString(GetItemCharges(o));
        SetItemCharges(o, n);
        s += " " + V(o) + " c" + IntToString(GetItemCharges(o));
        PLog(k, s);
        Keep(k, o);
    }
}

// A stripped item with one cast spell property; charges set before or after adding it.
void OneSpell(string tag, string sResRef, int nSpell, int nUse, int nCharges, int bChargesFirst, int nLimit, int nStack)
{
    object o = CreateItemOnObject(sResRef, NewChest(), nStack);
    string k = "one." + tag;
    Strip(o);
    string s = V(o);
    if (bChargesFirst && nCharges >= 0) SetItemCharges(o, nCharges);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyCastSpell(nSpell, nUse), o);
    s += " " + V(o);
    if (nLimit == 1) AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyLimitUseByClass(CLASS_TYPE_CLERIC), o);
    if (nLimit == 2) AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyLimitUseByRace(RACIAL_TYPE_HALFORC), o);
    if (nLimit == 3) AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyLimitUseByAlign(IP_CONST_ALIGNMENTGROUP_EVIL), o);
    s += " " + V(o);
    if (!bChargesFirst && nCharges >= 0) SetItemCharges(o, nCharges);
    s += " " + V(o);
    SetIdentified(o, TRUE);
    s += " " + V(o) + " c" + IntToString(GetItemCharges(o)) + " s" + IntToString(GetItemStackSize(o)) + " p" + IntToString(GetItemPropertyType(GetFirstItemProperty(o)));
    PLog(k, s);
    Keep(k, o);
}

// Two or three cast spell properties on one item.
void Multi(string tag, string sResRef, int a, int ua, int b, int ub, int c, int uc, int nCharges)
{
    object o = CreateItemOnObject(sResRef, Chest());
    Strip(o);
    SetItemCharges(o, nCharges);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyCastSpell(a, ua), o);
    string s = V(o);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyCastSpell(b, ub), o);
    s += " " + V(o);
    if (c >= 0) { AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyCastSpell(c, uc), o); s += " " + V(o); }
    PLog("multi." + tag, s);
    Keep("multi." + tag, o);
}

// A stock blueprint in stacks of 1..5 and 10, identified and not.
void Stacks(string sResRef)
{
    int i;
    for (i = 1; i <= 6; i++)
    {
        int n = i == 6 ? 10 : i;
        object o = CreateItemOnObject(sResRef, NewChest(), n);
        string k = "stack." + sResRef + "." + IntToString(n);
        string s = V(o) + " s" + IntToString(GetItemStackSize(o));
        SetIdentified(o, FALSE);
        s += " u" + V(o);
        SetIdentified(o, TRUE);
        s += " " + V(o);
        SetItemStackSize(o, 1);
        s += " one" + V(o);
        SetItemStackSize(o, n);
        s += " back" + V(o);
        PLog(k, s);
        Keep(k, o);
    }
}

// Unidentified values of stock blueprints.
void Unident(string sResRef)
{
    object o = CreateItemOnObject(sResRef, Chest());
    string s = V(o) + " i" + IntToString(GetIdentified(o));
    SetIdentified(o, FALSE);
    s += " " + V(o);
    SetItemCharges(o, 1);
    s += " " + V(o);
    SetIdentified(o, TRUE);
    s += " " + V(o);
    PLog("unid." + sResRef, s);
    Keep("unid." + sResRef, o);
}

void Part1()
{
    StockCharges("nw_wmgwn004");
    StockCharges("nw_wmgwn006");
    StockCharges("nw_wmgwn013");
    StockCharges("nw_wmgrd002");
    StockCharges("nw_wmgst004");
}

void Part2()
{
    int u;
    for (u = 1; u <= 13; u++)
    {
        OneSpell("wand167.u" + IntToString(u) + ".after", "nw_wmgwn004", 167, u, 3, FALSE, 0, 1);
        OneSpell("wand167.u" + IntToString(u) + ".first", "nw_wmgwn004", 167, u, 3, TRUE, 0, 1);
    }
    OneSpell("wand167.c3.cleric", "nw_wmgwn004", 167, 6, 3, FALSE, 1, 1);
    OneSpell("wand167.c3.race", "nw_wmgwn004", 167, 6, 3, FALSE, 2, 1);
    OneSpell("wand167.c3.align", "nw_wmgwn004", 167, 6, 3, FALSE, 3, 1);
    OneSpell("wand167.c3.cleric.first", "nw_wmgwn004", 167, 6, 3, TRUE, 1, 1);
    OneSpell("wand167.nocharge", "nw_wmgwn004", 167, 6, -1, FALSE, 0, 1);
    OneSpell("wand167.c0", "nw_wmgwn004", 167, 6, 0, FALSE, 0, 1);
    OneSpell("wand167.c1", "nw_wmgwn004", 167, 6, 1, FALSE, 0, 1);
    OneSpell("wand167.c10.first", "nw_wmgwn004", 167, 6, 10, TRUE, 0, 1);
    OneSpell("wand167.c49.first", "nw_wmgwn004", 167, 6, 49, TRUE, 0, 1);
    OneSpell("wand167.c50.first", "nw_wmgwn004", 167, 6, 50, TRUE, 0, 1);
    OneSpell("wand000.c3", "nw_wmgwn004", 0, 6, 3, FALSE, 0, 1);
    OneSpell("wand001.c3", "nw_wmgwn004", 1, 6, 3, FALSE, 0, 1);
    OneSpell("wand001.c10.first", "nw_wmgwn004", 1, 6, 10, TRUE, 0, 1);
    OneSpell("rod167.c3", "nw_wmgrd002", 167, 6, 3, FALSE, 0, 1);
    OneSpell("staff167.c3", "nw_wmgst004", 167, 6, 3, FALSE, 0, 1);
    OneSpell("sword167.c3", "nw_wswls001", 167, 6, 3, FALSE, 0, 1);
    OneSpell("sword167.c3.first", "nw_wswls001", 167, 6, 3, TRUE, 0, 1);
    OneSpell("sword167.perday", "nw_wswls001", 167, 10, -1, FALSE, 0, 1);
    OneSpell("ring167.perday", "nw_it_mring001", 167, 10, -1, FALSE, 0, 1);
    OneSpell("ring167.unlim", "nw_it_mring001", 167, 13, -1, FALSE, 0, 1);
}

void Part3()
{
    OneSpell("scroll167.s1", "nw_it_sparscr104", 167, 1, 3, FALSE, 0, 1);
    OneSpell("scroll167.s3", "nw_it_sparscr104", 167, 1, 3, FALSE, 0, 3);
    OneSpell("scroll167.s3.nocharge", "nw_it_sparscr104", 167, 1, -1, FALSE, 0, 3);
    OneSpell("scroll167.s3.cleric", "nw_it_sparscr104", 167, 1, 3, FALSE, 1, 3);
    OneSpell("scroll167.s3.race", "nw_it_sparscr104", 167, 1, 3, FALSE, 2, 3);
    OneSpell("scroll167.s3.cleric.nocharge", "nw_it_sparscr104", 167, 1, -1, FALSE, 1, 3);
    OneSpell("scroll167.s1.cleric.nocharge", "nw_it_sparscr104", 167, 1, -1, FALSE, 1, 1);
    OneSpell("scroll000.s1", "nw_it_sparscr104", 0, 1, -1, FALSE, 0, 1);
    OneSpell("scroll001.s5", "nw_it_sparscr104", 1, 1, -1, FALSE, 0, 5);
    OneSpell("scroll167.u6.s1", "nw_it_sparscr104", 167, 6, -1, FALSE, 0, 1);
    OneSpell("potion167.s1", "nw_it_mpotion001", 167, 1, -1, FALSE, 0, 1);
    OneSpell("potion167.s3", "nw_it_mpotion001", 167, 1, -1, FALSE, 0, 3);
    OneSpell("potion167.s3.cleric", "nw_it_mpotion001", 167, 1, -1, FALSE, 1, 3);
    OneSpell("potion001.s10", "nw_it_mpotion001", 1, 1, -1, FALSE, 0, 10);
    OneSpell("potion167.s3.c3", "nw_it_mpotion001", 167, 1, 3, FALSE, 0, 3);
    Multi("wand.167.001", "nw_wmgwn004", 167, 6, 1, 6, -1, 0, 10);
    Multi("wand.001.167", "nw_wmgwn004", 1, 6, 167, 6, -1, 0, 10);
    Multi("wand.000.001.167", "nw_wmgwn004", 0, 6, 1, 6, 167, 6, 10);
    Multi("wand.167.001.000", "nw_wmgwn004", 167, 6, 1, 6, 0, 6, 10);
    Multi("sword.167u10.001u13", "nw_wswls001", 167, 10, 1, 13, -1, 0, 0);
    Multi("sword.001u13.167u10.000u8", "nw_wswls001", 1, 13, 167, 10, 0, 8, 0);
}

void Part4()
{
    Stacks("nw_it_sparscr104");
    Stacks("nw_it_sparscr211");
    Stacks("nw_it_mpotion001");
    Stacks("nw_it_mpotion020");
    Stacks("nw_wamar001");
    Stacks("nw_it_gem001");
    Unident("nw_wmgwn004");
    Unident("nw_wmgwn006");
    Unident("nw_it_sparscr104");
    Unident("nw_it_mpotion001");
    Unident("nw_it_mring001");
    Unident("nw_wswls001");
    Unident("nw_wswmls002");
    Unident("nw_maarcl050");
}

// When charges set after creation reach the value: by holder, and on moving between holders.
void Holders()
{
    object cr = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(6.0, 0.0), FALSE, "VALHOLDER");
    SetAILevel(cr, AI_LEVEL_NORMAL);
    ChangeToStandardFaction(cr, STANDARD_FACTION_COMMONER);
    object a = CreateItemOnObject("nw_wmgwn006", cr);
    SetItemCharges(a, 3);
    PLog("holder.creature", V(a));
    Keep("holder.creature", a);
    object b = CreateItemOnObject("nw_wmgwn006", Chest());
    SetItemCharges(b, 3);
    AssignCommand(cr, ActionTakeItem(b, Chest()));
    PLog("holder.taken", V(b));
    DelayCommand(1.0, Later("holder.taken.1s", b));
    object c = CreateItemOnObject("nw_wmgwn006", Chest());
    SetItemCharges(c, 3);
    object c2 = CopyItem(c, NewChest(), TRUE);
    PLog("holder.copy", V(c) + " " + V(c2) + " c" + IntToString(GetItemCharges(c2)));
    Keep("holder.copy", c2);
    object d = CreateItemOnObject("nw_wmgwn006", Chest());
    SetItemCharges(d, 3);
    SetIdentified(d, TRUE);
    PLog("holder.reidentify", V(d));
    object e = CreateItemOnObject("nw_wmgwn006", Chest());
    SetItemCharges(e, 3);
    SetPlotFlag(e, TRUE); SetPlotFlag(e, FALSE);
    PLog("holder.plot", V(e));
    SetStolenFlag(e, TRUE);
    PLog("holder.stolen", V(e));
    Keep("holder.stolen", e);
    object f = CreateItemOnObject("nw_wmgwn006", Chest());
    SetItemCharges(f, 3);
    AssignCommand(cr, ActionTakeItem(f, Chest()));
    AssignCommand(cr, ActionDoCommand(ActionGiveItem(f, GetObjectByTag("VALCHEST"))));
    DelayCommand(2.0, Later("holder.given.2s", f));
    // Read first (50 charges), then 3 charges, then copied.
    object g = CreateItemOnObject("nw_wmgwn006", Chest());
    string sg = V(g);
    SetItemCharges(g, 3);
    object g2 = CopyItem(g, NewChest(), TRUE);
    PLog("holder.readcopy", sg + " " + V(g) + " " + V(g2));
    Keep("holder.readcopy", g2);
    // A property added without reading the value, then charges changed, then read.
    object h = CreateItemOnObject("nw_wmgwn006", Chest());
    string sh = V(h);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyCastSpell(167, 6), h);
    SetItemCharges(h, 3);
    PLog("holder.addthencharges", sh + " " + V(h));
    Keep("holder.addthencharges", h);
    // Identified again without reading first.
    object i = CreateItemOnObject("nw_wmgwn006", Chest());
    string si = V(i);
    SetItemCharges(i, 3);
    SetIdentified(i, FALSE);
    SetIdentified(i, TRUE);
    PLog("holder.unidreid", si + " " + V(i));
    // Taken by a creature after being read.
    object j = CreateItemOnObject("nw_wmgwn006", Chest());
    string sj = V(j);
    SetItemCharges(j, 3);
    AssignCommand(cr, ActionTakeItem(j, Chest()));
    PLog("holder.readtaken", sj + " " + V(j));
    DelayCommand(1.0, Later("holder.readtaken.1s", j));
    // A stack split by SetItemStackSize after a read.
    object k = CreateItemOnObject("nw_it_mpotion001", NewChest(), 5);
    string sk = V(k);
    SetItemStackSize(k, 2);
    PLog("holder.stackread", sk + " " + V(k));
}

void main()
{
    CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(-4.0, 4.0), FALSE, "VALCHEST");
    DelayCommand(0.5, Part1());
    DelayCommand(1.0, Part2());
    DelayCommand(1.5, Part3());
    DelayCommand(2.0, Part4());
    DelayCommand(2.5, Holders());
    DelayCommand(5.0, PDone());
}
