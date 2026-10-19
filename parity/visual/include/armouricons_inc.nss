// Armour icons in the original (scenes armouricons, armouriconsm): the inventory icon of body armour is built from the armour's parts. The fixture
// character carries a chain shirt (nw_aarcl004) on quickbar slot 5 and a robe (nw_cloth005); the script builds
// armour with other parts and colours (the six colour channels and per-part overrides), a robe, an unidentified
// piece, parts without an icon and parts the icon doesn't show, then looks at them in the inventory grid, the
// equipped slot, the quickbar, examine windows, a store, a chest and the item carried on the cursor.
#include "visual_inc"

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }
void Tap(int nVk) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(nVk) + "|tap"); }
void Examine(object pc, object o) { AssignCommand(pc, ClearAllActions()); AssignCommand(pc, ActionExamine(o)); }

object Stage() { return GetObjectByTag("armour_stage"); }

// One appearance change by CopyItemAndModify (the copy replaces the item).
object Mod(object o, int nType, int nIndex, int nValue)
{
    object n = CopyItemAndModify(o, nType, nIndex, nValue, TRUE);
    if (!GetIsObjectValid(n)) { Note("modfail|" + GetTag(o) + "|" + IntToString(nType) + "|" + IntToString(nIndex) + "|" + IntToString(nValue)); return o; }
    DestroyObject(o);
    return n;
}

object Part(object o, int nPart, int nModel) { return Mod(o, ITEM_APPR_TYPE_ARMOR_MODEL, nPart, nModel); }
object Colour(object o, int nChannel, int nColour) { return Mod(o, ITEM_APPR_TYPE_ARMOR_COLOR, nChannel, nColour); }
object PartColour(object o, int nPart, int nChannel, int nColour)
{
    return Mod(o, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_NUM_COLORS + nPart * ITEM_APPR_ARMOR_NUM_COLORS + nChannel, nColour);
}

object Six(object o)
{
    o = Colour(o, ITEM_APPR_ARMOR_COLOR_LEATHER1, 5);
    o = Colour(o, ITEM_APPR_ARMOR_COLOR_LEATHER2, 45);
    o = Colour(o, ITEM_APPR_ARMOR_COLOR_CLOTH1, 12);
    o = Colour(o, ITEM_APPR_ARMOR_COLOR_CLOTH2, 60);
    o = Colour(o, ITEM_APPR_ARMOR_COLOR_METAL1, 20);
    return Colour(o, ITEM_APPR_ARMOR_COLOR_METAL2, 55);
}

void Describe(object o, string label)
{
    string s = label + "|" + GetResRef(o) + "|id=" + IntToString(GetIdentified(o)) + "|parts=";
    int i;
    for (i = 0; i <= 18; i++) s += IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_ARMOR_MODEL, i)) + ",";
    s += "|colours=";
    for (i = 0; i < 6; i++) s += IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_ARMOR_COLOR, i)) + ",";
    s += "|overrides=";
    int n;
    for (n = 6; n < 6 + 19 * 6; n++)
    {
        int v = GetItemAppearance(o, ITEM_APPR_TYPE_ARMOR_COLOR, n);
        if (v != 255) s += IntToString(n) + ":" + IntToString(v) + ",";
    }
    Note("item|" + s);
}

object Make(string resref) { return CreateItemOnObject(resref, Stage()); }

// The variants, built in the staging chest, then copied to the player in order (the grid fills row by row).
void Build(object pc)
{
    object a = Make("nw_aarcl001");                                  // 1 stock: chest 16, pelvis 8, shoulders 15
    SetLocalObject(pc, "v1", a);
    object b = Make("nw_cloth024");                                  // 2 stock: chest 39, pelvis 37, no shoulders
    SetLocalObject(pc, "v2", b);
    object c = Six(Make("nw_aarcl004"));                             // 3 the six colour channels
    SetLocalObject(pc, "v3", c);
    object d = Six(Make("nw_aarcl004"));                             // 4 the six channels and per-part overrides
    d = PartColour(d, ITEM_APPR_ARMOR_MODEL_TORSO, ITEM_APPR_ARMOR_COLOR_CLOTH1, 33);
    d = PartColour(d, ITEM_APPR_ARMOR_MODEL_TORSO, ITEM_APPR_ARMOR_COLOR_METAL1, 9);
    d = PartColour(d, ITEM_APPR_ARMOR_MODEL_PELVIS, ITEM_APPR_ARMOR_COLOR_LEATHER1, 50);
    d = PartColour(d, ITEM_APPR_ARMOR_MODEL_LSHOULDER, ITEM_APPR_ARMOR_COLOR_METAL1, 40);
    d = PartColour(d, ITEM_APPR_ARMOR_MODEL_RSHOULDER, ITEM_APPR_ARMOR_COLOR_METAL1, 3);
    d = PartColour(d, ITEM_APPR_ARMOR_MODEL_BELT, ITEM_APPR_ARMOR_COLOR_LEATHER1, 30);
    SetLocalObject(pc, "v4", d);
    object e = Part(Make("nw_aarcl004"), ITEM_APPR_ARMOR_MODEL_ROBE, 3);   // 5 a robe over the chain shirt
    SetLocalObject(pc, "v5", e);
    object f = Make("nw_aarcl004");                                  // 6 other chest, pelvis, belt, odd shoulders
    f = Part(f, ITEM_APPR_ARMOR_MODEL_TORSO, 5);
    f = Part(f, ITEM_APPR_ARMOR_MODEL_PELVIS, 10);
    f = Part(f, ITEM_APPR_ARMOR_MODEL_BELT, 2);
    f = Part(f, ITEM_APPR_ARMOR_MODEL_LSHOULDER, 3);
    f = Part(f, ITEM_APPR_ARMOR_MODEL_RSHOULDER, 22);
    SetLocalObject(pc, "v6", f);
    object g = CopyItem(f, Stage(), TRUE);                           // 7 the same, unidentified
    SetIdentified(g, FALSE);
    SetLocalObject(pc, "v7", g);
    // Page 2.
    object h = Make("nw_aarcl004");                                  // 8 the 186 parts
    h = Part(h, ITEM_APPR_ARMOR_MODEL_TORSO, 186);
    h = Part(h, ITEM_APPR_ARMOR_MODEL_PELVIS, 186);
    h = Part(h, ITEM_APPR_ARMOR_MODEL_LSHOULDER, 186);
    h = Part(h, ITEM_APPR_ARMOR_MODEL_RSHOULDER, 186);
    SetLocalObject(pc, "v8", h);
    object k = Make("nw_aarcl004");                                  // 9 chest 58 (no ipm_chest058), pelvis and belt 63
    k = Part(k, ITEM_APPR_ARMOR_MODEL_TORSO, 58);
    k = Part(k, ITEM_APPR_ARMOR_MODEL_PELVIS, 63);
    k = Part(k, ITEM_APPR_ARMOR_MODEL_BELT, 63);
    SetLocalObject(pc, "v9", k);
    object l = Make("nw_aarcl004");                                  // 10 chest and pelvis 1 only
    l = Part(l, ITEM_APPR_ARMOR_MODEL_TORSO, 1);
    l = Part(l, ITEM_APPR_ARMOR_MODEL_PELVIS, 1);
    l = Part(l, ITEM_APPR_ARMOR_MODEL_BELT, 0);
    l = Part(l, ITEM_APPR_ARMOR_MODEL_LSHOULDER, 0);
    l = Part(l, ITEM_APPR_ARMOR_MODEL_RSHOULDER, 0);
    SetLocalObject(pc, "v10", l);
    object m = Make("nw_aarcl004");                                  // 11 limbs and neck changed (not in the icon?)
    m = Part(m, ITEM_APPR_ARMOR_MODEL_NECK, 3);
    m = Part(m, ITEM_APPR_ARMOR_MODEL_LBICEP, 2);
    m = Part(m, ITEM_APPR_ARMOR_MODEL_RBICEP, 2);
    m = Part(m, ITEM_APPR_ARMOR_MODEL_LFOREARM, 5);
    m = Part(m, ITEM_APPR_ARMOR_MODEL_RFOREARM, 5);
    m = Part(m, ITEM_APPR_ARMOR_MODEL_LHAND, 3);
    m = Part(m, ITEM_APPR_ARMOR_MODEL_RHAND, 3);
    m = Part(m, ITEM_APPR_ARMOR_MODEL_LTHIGH, 6);
    m = Part(m, ITEM_APPR_ARMOR_MODEL_RTHIGH, 6);
    m = Part(m, ITEM_APPR_ARMOR_MODEL_LSHIN, 6);
    m = Part(m, ITEM_APPR_ARMOR_MODEL_RSHIN, 6);
    m = Part(m, ITEM_APPR_ARMOR_MODEL_LFOOT, 6);
    m = Part(m, ITEM_APPR_ARMOR_MODEL_RFOOT, 6);
    SetLocalObject(pc, "v11", m);
    object n = Part(Make("nw_aarcl004"), ITEM_APPR_ARMOR_MODEL_ROBE, 38);  // 12 robe 38
    SetLocalObject(pc, "v12", n);
    object p = Part(Six(Make("nw_aarcl004")), ITEM_APPR_ARMOR_MODEL_ROBE, 20); // 13 robe 20, coloured
    SetLocalObject(pc, "v13", p);
    object q = Make("nw_aarcl004");                                  // 14 per-part override on the robe only
    q = Part(q, ITEM_APPR_ARMOR_MODEL_ROBE, 5);
    q = PartColour(q, ITEM_APPR_ARMOR_MODEL_ROBE, ITEM_APPR_ARMOR_COLOR_CLOTH1, 40);
    q = PartColour(q, ITEM_APPR_ARMOR_MODEL_ROBE, ITEM_APPR_ARMOR_COLOR_LEATHER1, 40);
    SetLocalObject(pc, "v14", q);
    // Page 3: parts without an icon, a robe that hides fewer parts, and which part's colour each icon part takes.
    object r15 = Part(Make("nw_aarcl004"), ITEM_APPR_ARMOR_MODEL_PELVIS, 40);     // 15 no ipX_pelvis040
    SetLocalObject(pc, "v15", r15);
    object r16 = Part(Make("nw_aarcl004"), ITEM_APPR_ARMOR_MODEL_PELVIS, 40);     // 16 no pelvis or chest icon
    r16 = Part(r16, ITEM_APPR_ARMOR_MODEL_TORSO, 58);
    SetLocalObject(pc, "v16", r16);
    object r17 = Part(Make("nw_aarcl004"), ITEM_APPR_ARMOR_MODEL_LSHOULDER, 26);  // 17 no ipX_shol026
    SetLocalObject(pc, "v17", r17);
    object r18 = Part(Make("nw_aarcl004"), ITEM_APPR_ARMOR_MODEL_RSHOULDER, 26);  // 18 no ipX_shor026
    SetLocalObject(pc, "v18", r18);
    object r19 = Part(Make("nw_aarcl004"), ITEM_APPR_ARMOR_MODEL_ROBE, 30);       // 19 robe 30 (hides pelvis and belt)
    SetLocalObject(pc, "v19", r19);
    object r20 = Make("nw_aarcl004");                                              // 20 metal 1 per part: 100 + 3 x part
    object r21 = Part(Make("nw_aarcl004"), ITEM_APPR_ARMOR_MODEL_ROBE, 5);        // 21 the same on a robe, and cloth 1
    int i;
    for (i = 0; i <= 18; i++)
    {
        r20 = PartColour(r20, i, ITEM_APPR_ARMOR_COLOR_METAL1, 100 + 3 * i);
        r21 = PartColour(r21, i, ITEM_APPR_ARMOR_COLOR_METAL1, 100 + 3 * i);
        r21 = PartColour(r21, i, ITEM_APPR_ARMOR_COLOR_CLOTH1, 100 + 3 * i);
    }
    SetLocalObject(pc, "v20", r20);
    SetLocalObject(pc, "v22", Part(Make("nw_aarcl004"), ITEM_APPR_ARMOR_MODEL_PELVIS, 0));  // 22 no pelvis
    SetLocalObject(pc, "v23", Part(Make("nw_aarcl004"), ITEM_APPR_ARMOR_MODEL_TORSO, 0));   // 23 no chest
    SetLocalObject(pc, "v21", r21);
    // Other part-built items: a helmet and cloaks (palette icons) and a three-part weapon, with other models and colours.
    object h1 = Six(Make("nw_arhe005"));
    SetLocalObject(pc, "o1", h1);
    object h2 = Mod(Six(Make("nw_arhe001")), ITEM_APPR_TYPE_ARMOR_MODEL, 0, 20);
    SetLocalObject(pc, "o2", h2);
    object k1 = Six(Make("x2_it_mcloak001"));
    SetLocalObject(pc, "o3", k1);
    object k2 = Mod(Six(Make("x2_it_mcloak001")), ITEM_APPR_TYPE_SIMPLE_MODEL, 0, 7);
    SetLocalObject(pc, "o4", k2);
    object sw = Make("nw_wswls001");
    sw = Mod(sw, ITEM_APPR_TYPE_WEAPON_MODEL, ITEM_APPR_WEAPON_MODEL_TOP, 2);
    sw = Mod(sw, ITEM_APPR_TYPE_WEAPON_COLOR, ITEM_APPR_WEAPON_COLOR_TOP, 3);
    sw = Mod(sw, ITEM_APPR_TYPE_WEAPON_MODEL, ITEM_APPR_WEAPON_MODEL_BOTTOM, 3);
    SetLocalObject(pc, "o5", sw);
    // Worn: the per-part override armour.
    object w = Six(Make("nw_aarcl004"));
    w = PartColour(w, ITEM_APPR_ARMOR_MODEL_TORSO, ITEM_APPR_ARMOR_COLOR_CLOTH1, 33);
    w = PartColour(w, ITEM_APPR_ARMOR_MODEL_TORSO, ITEM_APPR_ARMOR_COLOR_METAL1, 9);
    SetLocalObject(pc, "worn", w);
}

void Give(object pc)
{
    int i;
    for (i = 1; i <= 23; i++)
    {
        object o = CopyItem(GetLocalObject(pc, "v" + IntToString(i)), pc, TRUE);
        if (i == 7) SetIdentified(o, FALSE);
        SetLocalObject(pc, "p" + IntToString(i), o);
        Describe(o, "v" + IntToString(i));
    }
    for (i = 1; i <= 5; i++)
    {
        object o = CopyItem(GetLocalObject(pc, "o" + IntToString(i)), pc, TRUE);
        SetLocalObject(pc, "q" + IntToString(i), o);
        Note("item|o" + IntToString(i) + "|" + GetResRef(o) + "|model=" + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_SIMPLE_MODEL, 0))
            + "|armormodel=" + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_ARMOR_MODEL, 0))
            + "|w=" + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_WEAPON_MODEL, 0)) + "," + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_WEAPON_MODEL, 1)) + "," + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_WEAPON_MODEL, 2))
            + "|wc=" + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_WEAPON_COLOR, 0)) + "," + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_WEAPON_COLOR, 1)) + "," + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_WEAPON_COLOR, 2))
            + "|colours=" + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_ARMOR_COLOR, 0)) + "," + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_ARMOR_COLOR, 1)) + "," + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_ARMOR_COLOR, 2)) + "," + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_ARMOR_COLOR, 3)) + "," + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_ARMOR_COLOR, 4)) + "," + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_ARMOR_COLOR, 5)));
    }
}

void Wear(object pc)
{
    object o = CopyItem(GetLocalObject(pc, "worn"), pc, TRUE);
    Describe(o, "worn");
    AssignCommand(pc, ActionEquipItem(o, INVENTORY_SLOT_CHEST));
}

void Clear(object pc)
{
    Note("character-fixture|" + GetName(pc) + "|pc=" + IntToString(GetIsPC(pc)));
    Note("gender|" + IntToString(GetGender(pc)));
    // Skin, hair and tattoo colours the icons might take from the player.
    SetColor(pc, COLOR_CHANNEL_SKIN, 14);
    SetColor(pc, COLOR_CHANNEL_HAIR, 30);
    SetColor(pc, COLOR_CHANNEL_TATTOO_1, 40);
    SetColor(pc, COLOR_CHANNEL_TATTOO_2, 50);
    Note("skin|" + IntToString(GetColor(pc, COLOR_CHANNEL_SKIN)) + "|hair|" + IntToString(GetColor(pc, COLOR_CHANNEL_HAIR)));
    object o = GetFirstItemInInventory(pc);
    while (GetIsObjectValid(o))
    {
        string r = GetResRef(o);
        if (r == "nw_aarcl004") Describe(o, "quickbar");
        else if (GetTag(o) == "NW_CLOTH005") Describe(o, "robe_fixture");
        else DestroyObject(o);
        o = GetNextItemInInventory(pc);
    }
    DestroyObject(GetItemInSlot(INVENTORY_SLOT_CHEST, pc));
}

void Stock(object store, object pc)
{
    CopyItem(GetLocalObject(pc, "v3"), store, TRUE);
    CopyItem(GetLocalObject(pc, "v5"), store, TRUE);
    CopyItem(GetLocalObject(pc, "v8"), store, TRUE);
    object u = CopyItem(GetLocalObject(pc, "v4"), store, TRUE);
    SetIdentified(u, FALSE);
}

void StockChest(object chest, object pc)
{
    CopyItem(GetLocalObject(pc, "v1"), chest, TRUE);
    CopyItem(GetLocalObject(pc, "v6"), chest, TRUE);
    CopyItem(GetLocalObject(pc, "v12"), chest, TRUE);
}

void ArmourIconsMain()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    GiveGoldToCreature(pc, 50000);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    object stage = VPlaceable(pc, "plc_chest1", 8.0, 8.0, 0.0);
    SetTag(stage, "armour_stage");
    DelayCommand(0.3, Clear(pc));
    DelayCommand(0.6, Build(pc));
    DelayCommand(1.2, Wear(pc));
    DelayCommand(2.4, Give(pc));
    DelayCommand(3.0, VCamera(pc, 90.0, 12.0, 60.0));
    // Inventory (page 1, then page 2) and the quickbar.
    DelayCommand(5.0, Tap(73));
    DelayCommand(7.0, VShot("a_inventory"));
    // The first item carried on the cursor (posted left button held), then put back.
    DelayCommand(7.4, WriteTimestampedLogEntry("VISUAL|hover|0.029,0.368"));
    DelayCommand(7.6, WriteTimestampedLogEntry("VISUAL|mouse|0.029,0.368|down"));
    DelayCommand(7.9, WriteTimestampedLogEntry("VISUAL|hover|0.45,0.45"));
    DelayCommand(8.6, VShot("a_drag"));
    DelayCommand(9.2, WriteTimestampedLogEntry("VISUAL|hover|0.029,0.368"));
    DelayCommand(9.5, WriteTimestampedLogEntry("VISUAL|mouse|0.029,0.368|up"));
    DelayCommand(9.7, WriteTimestampedLogEntry("VISUAL|hover|0.6,0.97"));
    DelayCommand(10.2, WriteTimestampedLogEntry("VISUAL|hover|off"));
    DelayCommand(11.0, Tap(221));
    DelayCommand(13.0, VShot("b_inventory_page2"));
    DelayCommand(13.5, Tap(221));
    DelayCommand(14.5, VShot("b_inventory_page3"));
    DelayCommand(14.7, Tap(221));
    DelayCommand(15.2, VShot("b_inventory_page4"));
    DelayCommand(15.5, Tap(73));
    // Examine windows.
    DelayCommand(16.0, Examine(pc, GetLocalObject(pc, "p4")));
    DelayCommand(18.0, VShot("c_examine_overrides"));
    DelayCommand(19.0, Examine(pc, GetLocalObject(pc, "p5")));
    DelayCommand(21.0, VShot("d_examine_robe"));
    DelayCommand(22.0, Examine(pc, GetLocalObject(pc, "p7")));
    DelayCommand(24.0, VShot("e_examine_unid"));
    DelayCommand(25.0, Tap(27));
    // A store with armour (last: closing it differs between the engines).
    object store = CreateObject(OBJECT_TYPE_STORE, "st_empty", GetLocation(pc), FALSE, "fixture_store");
    DelayCommand(3.5, Stock(store, pc));
    DelayCommand(113.0, OpenStore(store, pc));
    DelayCommand(116.0, VShot("f_store"));
    // A chest with armour.
    object chest = VPlaceable(pc, "plc_chest1", 1.2, 0.0, 180.0);
    DelayCommand(3.5, StockChest(chest, pc));
    DelayCommand(27.0, AssignCommand(pc, ActionInteractObject(chest)));
    DelayCommand(32.0, VShot("g_container"));
    // (left open: Esc there opens OpenWinter's game menu, which pauses the scene)
    // Each built piece examined on its own (the examine window shows one icon at a fixed place).
    int n;
    for (n = 1; n <= 23; n++)
    {
        float t = 38.5 + 2.5 * IntToFloat(n);
        DelayCommand(t, Examine(pc, GetLocalObject(pc, "p" + IntToString(n))));
        DelayCommand(t + 1.6, VShot("x_examine_v" + GetStringRight("0" + IntToString(n), 2)));
    }
    for (n = 1; n <= 5; n++)
    {
        float t = 96.0 + 2.5 * IntToFloat(n);
        DelayCommand(t, Examine(pc, GetLocalObject(pc, "q" + IntToString(n))));
        DelayCommand(t + 1.6, VShot("y_examine_o" + IntToString(n)));
    }
    DelayCommand(110.0, Tap(27));
    DelayCommand(118.0, VDone());
}
