// Scroll, potion and wand icons in the original: inventory, quickbar, examine windows, a store and a chest.
// The fixture character carries stock scrolls (arcane and divine appearances) bound to quickbar slots 5..11;
// the script turns one into a custom scroll (Magic Circle -> Fireball), gives one a second cast-spell property and
// makes one unidentified, then adds a blank scroll, a scroll without its cast property, potions and a wand.
#include "visual_inc"

void Bases2(object pc);
void Report(object pc);

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }
void Tap(int nVk) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(nVk) + "|tap"); }
void Examine(object pc, object o) { AssignCommand(pc, ClearAllActions()); AssignCommand(pc, ActionExamine(o)); }

object ByResRef(object pc, string resref)
{
    object o = GetFirstItemInInventory(pc);
    while (GetIsObjectValid(o) && GetResRef(o) != resref) o = GetNextItemInInventory(pc);
    return o;
}

void DropCast(object o)
{
    itemproperty ip = GetFirstItemProperty(o);
    while (GetIsItemPropertyValid(ip))
    {
        if (GetItemPropertyType(ip) == ITEM_PROPERTY_CAST_SPELL) { RemoveItemProperty(o, ip); ip = GetFirstItemProperty(o); }
        else ip = GetNextItemProperty(o);
    }
}

void Describe(object o, string label)
{
    string s = label + "|" + GetResRef(o) + "|base=" + IntToString(GetBaseItemType(o)) + "|id=" + IntToString(GetIdentified(o));
    itemproperty ip = GetFirstItemProperty(o);
    while (GetIsItemPropertyValid(ip))
    {
        if (GetItemPropertyType(ip) == ITEM_PROPERTY_CAST_SPELL) s += "|cast=" + IntToString(GetItemPropertySubType(ip));
        ip = GetNextItemProperty(o);
    }
    Note("item|" + s);
}

void Ready(object pc)
{
    Note("character-fixture|" + GetName(pc) + "|pc=" + IntToString(GetIsPC(pc)));
    object custom = ByResRef(pc, "x2_it_sparscrmc");
    DropCast(custom);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyCastSpell(IP_CONST_CASTSPELL_FIREBALL_5, IP_CONST_CASTSPELL_NUMUSES_SINGLE_USE), custom);
    SetLocalObject(pc, "custom", custom);
    object two = ByResRef(pc, "nw_it_sparscr509");
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyCastSpell(IP_CONST_CASTSPELL_INVISIBILITY_3, IP_CONST_CASTSPELL_NUMUSES_SINGLE_USE), two);
    SetLocalObject(pc, "two", two);
    object unid = ByResRef(pc, "nw_it_spdvscr501");
    SetIdentified(unid, FALSE);
    SetLocalObject(pc, "unid", unid);
    object bare = CreateItemOnObject("nw_it_sparscr101", pc);
    DropCast(bare);
    SetLocalObject(pc, "bare", bare);
    object blank = CreateItemOnObject("x2_it_cfm_bscrl", pc);
    object wand = CreateItemOnObject("nw_wmgwn005", pc);
    SetIdentified(wand, TRUE);
    SetLocalObject(pc, "wand", wand);
    object potion = CreateItemOnObject("nw_it_mpotion001", pc, 3);
    object late = CreateItemOnObject("nw_it_sparscr912", pc);
    object early = CreateItemOnObject("x1_it_sparscr103", pc);
    object o = GetFirstItemInInventory(pc);
    while (GetIsObjectValid(o)) { Describe(o, GetName(o)); o = GetNextItemInInventory(pc); }
}

void Stock(object store, object pc)
{
    CreateItemOnObject("nw_it_sparscr211", store);
    CreateItemOnObject("x1_it_spdvscr301", store);
    CreateItemOnObject("nw_it_sparscr912", store);
    CreateItemOnObject("nw_it_mpotion008", store);
    CreateItemOnObject("nw_wmgwn002", store);
    object u = CreateItemOnObject("nw_it_sparscr509", store);
    SetIdentified(u, FALSE);
}

object Cast(object o, int nSpell)
{
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyCastSpell(nSpell, IP_CONST_CASTSPELL_NUMUSES_SINGLE_USE), o);
    return o;
}

// Which items take the spell's icon: other base items with a cast-spell property, property order, a property row
// without an icon, and a scroll without its cast property.
void Bases(object pc)
{
    object o = GetFirstItemInInventory(pc);
    while (GetIsObjectValid(o)) { DestroyObject(o); o = GetNextItemInInventory(pc); }
    DelayCommand(0.5, Bases2(pc));
}

void Bases2(object pc)
{
    object first = CreateItemOnObject("nw_it_sparscr305", pc);
    object a = Cast(CreateItemOnObject("x2_it_cfm_bscrl", pc), IP_CONST_CASTSPELL_FIREBALL_5);
    object b = Cast(CreateItemOnObject("nw_it_book001", pc), IP_CONST_CASTSPELL_FIREBALL_5);
    object c = Cast(CreateItemOnObject("nw_it_mring001", pc), IP_CONST_CASTSPELL_FIREBALL_5);
    object d = CreateItemOnObject("nw_it_sparscr101", pc);
    DropCast(d); Cast(d, IP_CONST_CASTSPELL_INVISIBILITY_3); Cast(d, 1);
    object e = CreateItemOnObject("nw_it_sparscr101", pc);
    DropCast(e); Cast(e, 12);
    object f = CreateItemOnObject("nw_wmgrd002", pc);
    SetIdentified(f, TRUE);
    object g = Cast(CreateItemOnObject("nw_it_gem001", pc), IP_CONST_CASTSPELL_FIREBALL_5);
    object h = CreateItemOnObject("x1_it_sparscr103", pc);
    DropCast(h);
    object i = Cast(CreateItemOnObject("nw_it_msmlmisc06", pc), IP_CONST_CASTSPELL_FIREBALL_5);
    // Copies of baseitems row 75 (row 500) and of row 102 with its other differing columns set as row 75's (501).
    CreateItemOnObject("scrclone75", pc);
    CreateItemOnObject("scrclone102", pc);
    // Crafted-scroll base item 105, and iprp_spells rows whose Icon is missing, empty or another texture.
    CreateItemOnObject("scrcraft105", pc);
    CreateItemOnObject("scricon600", pc);
    CreateItemOnObject("scricon601", pc);
    CreateItemOnObject("scricon602", pc);
    SetLocalObject(pc, "blankcast", a);
    SetLocalObject(pc, "ringcast", c);
    DelayCommand(0.5, Report(pc));
}

void Report(object pc)
{
    object o = GetFirstItemInInventory(pc);
    while (GetIsObjectValid(o)) { Describe(o, "bases|" + GetName(o)); o = GetNextItemInInventory(pc); }
}

void Mouse(float x, float y, string what)
{
    WriteTimestampedLogEntry("VISUAL|mouse|" + FloatToString(x / 1280.0, 0, 6) + "," + FloatToString(y / 720.0, 0, 6) + "|" + what);
}

void Hover(float x, float y)
{
    WriteTimestampedLogEntry("VISUAL|hover|" + FloatToString(x / 1280.0, 0, 6) + "," + FloatToString(y / 720.0, 0, 6));
}

void Pointer(float x, float y)
{
    WriteTimestampedLogEntry("VISUAL|pointer|" + FloatToString(x / 1280.0, 0, 6) + "," + FloatToString(y / 720.0, 0, 6));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    GiveGoldToCreature(pc, 20000);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(0.5, Ready(pc));
    DelayCommand(2.0, VCamera(pc, 90.0, 12.0, 60.0));
    // Inventory and quickbar.
    DelayCommand(4.0, Tap(73));
    DelayCommand(6.0, VShot("a_inventory"));
    DelayCommand(7.0, Tap(73));
    // Examine windows.
    DelayCommand(8.0, Examine(pc, GetLocalObject(pc, "custom")));
    DelayCommand(10.0, VShot("b_examine_custom"));
    DelayCommand(11.0, Examine(pc, GetLocalObject(pc, "two")));
    DelayCommand(13.0, VShot("c_examine_two"));
    DelayCommand(14.0, Examine(pc, GetLocalObject(pc, "unid")));
    DelayCommand(16.0, VShot("d_examine_unid"));
    DelayCommand(17.0, Examine(pc, GetLocalObject(pc, "wand")));
    DelayCommand(19.0, VShot("e_examine_wand"));
    DelayCommand(20.0, Tap(27));
    // A chest with scrolls (its window keeps the inventory beside it).
    object chest = VPlaceable(pc, "plc_chest1", 1.2, 0.0, 180.0);
    CreateItemOnObject("nw_it_sparscr211", chest);
    CreateItemOnObject("x1_it_spdvscr301", chest);
    CreateItemOnObject("nw_it_mpotion008", chest);
    object cu = CreateItemOnObject("nw_it_sparscr912", chest);
    SetIdentified(cu, FALSE);
    DelayCommand(22.0, AssignCommand(pc, ActionInteractObject(chest)));
    DelayCommand(27.0, VShot("f_container"));
    // Other base items and fallbacks; OpenInventory, as the chest may still be open.
    DelayCommand(28.0, Bases(pc));
    DelayCommand(31.0, OpenInventory(pc, pc));
    DelayCommand(33.0, VShot("g_bases"));
    // Drag the bottom-left item (Stinking Cloud) with the real pointer (--real-mouse), then put it back.
    DelayCommand(34.0, Mouse(20.0, 401.0, "down"));
    DelayCommand(34.4, Pointer(60.0, 401.0));
    DelayCommand(34.8, Pointer(500.0, 300.0));
    DelayCommand(35.6, VShot("h_drag"));
    DelayCommand(36.2, Pointer(20.0, 401.0));
    DelayCommand(36.6, Mouse(20.0, 401.0, "up"));
    // The radial on that item (the real right button).
    DelayCommand(38.0, Mouse(20.0, 401.0, "down|right|real"));
    DelayCommand(39.5, VShot("i_radial"));
    DelayCommand(40.0, Mouse(20.0, 401.0, "up|right|real"));
    DelayCommand(42.0, Examine(pc, GetLocalObject(pc, "blankcast")));
    DelayCommand(44.0, VShot("j_examine_blankcast"));
    DelayCommand(45.0, Examine(pc, GetLocalObject(pc, "ringcast")));
    DelayCommand(47.0, VShot("k_examine_ringcast"));
    DelayCommand(48.0, Tap(27));
    // Last, a store with scrolls, a potion and a wand.
    object store = CreateObject(OBJECT_TYPE_STORE, "st_empty", GetLocation(pc), FALSE, "fixture_store");
    object item = GetFirstItemInInventory(store);
    while (GetIsObjectValid(item)) { DestroyObject(item); item = GetNextItemInInventory(store); }
    DelayCommand(1.0, Stock(store, pc));
    DelayCommand(50.0, OpenStore(store, pc));
    DelayCommand(53.0, VShot("l_store"));
    DelayCommand(55.0, VDone());
}
