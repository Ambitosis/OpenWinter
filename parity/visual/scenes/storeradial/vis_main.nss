#include "visual_inc"
// storeradial: the item ring while a store is open, on the
// player's own items (backpack, bag, equipped; plot, stolen, worth more than the store's gold) and on the store's
// wares (finite and infinite, a stack, one the player can't afford). Posted input only: the ring opens on a posted
// right press and its labels show while posted moves with the right button held rest on each of the eight hooks
// (100 px from the centre, which is clamped 154 px from the window's edges); numpad 5 and a release at the centre
// close it. Run: python tools/visual/visual.py run storeradial --only nwn --native-framebuffer --silent
void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }
void Click(float x, float y)
{
    string xy = FloatToString(x / 1280.0, 0, 6) + "," + FloatToString(y / 720.0, 0, 6);
    WriteTimestampedLogEntry("VISUAL|mouse|" + xy + "|down");
    DelayCommand(0.15, WriteTimestampedLogEntry("VISUAL|mouse|" + xy + "|up"));
}
object Give(object pc, string resref, int n, string name)
{
    object i = CreateItemOnObject(resref, pc, n);
    SetName(i, name);
    return i;
}
void Setup(object pc, object store)
{
    object i = GetFirstItemInInventory(pc);
    while (GetIsObjectValid(i)) { DestroyObject(i); i = GetNextItemInInventory(pc); }
    TakeGoldFromCreature(GetGold(pc), pc, TRUE);
    DelayCommand(0.3, GiveGoldToCreature(pc, 200));
}
void Fill(object pc, object store)
{
    Give(pc, "nw_wswls001", 1, "Pack Sword");
    Give(pc, "nw_aarcl001", 1, "Pack Armour");
    Give(pc, "nw_it_gem015", 1, "Pack Gem");
    Give(pc, "nw_it_mpotion001", 5, "Pack Potions");
    Give(pc, "nw_wamar001", 50, "Pack Arrows");
    Give(pc, "nw_it_sparscr101", 1, "Pack Scroll");
    object bag = Give(pc, "nw_it_contain001", 1, "Pack Bag");
    object inner = CreateItemOnObject("nw_it_gem010", bag); SetName(inner, "Bag Gem");
    object plot = Give(pc, "nw_it_gem001", 1, "Plot Gem"); SetPlotFlag(plot, TRUE);
    object stolen = Give(pc, "nw_it_gem003", 1, "Stolen Gem"); SetStolenFlag(stolen, TRUE);
    Give(pc, "nw_it_gem012", 1, "Dear Gem");
    Give(pc, "nw_it_medkit001", 1, "Pack Kit");
    object w;
    w = CreateItemOnObject("nw_aarcl001", store); SetInfiniteFlag(w, TRUE);
    w = CreateItemOnObject("nw_aarcl004", store);
    w = CreateItemOnObject("nw_wswls001", store);
    w = CreateItemOnObject("nw_wamar001", store, 50);
    w = CreateItemOnObject("nw_it_mpotion001", store); SetInfiniteFlag(w, TRUE);
    w = CreateItemOnObject("nw_it_sparscr101", store);
    w = CreateItemOnObject("nw_it_mring020", store);
    w = CreateItemOnObject("nw_it_gem015", store);
}
string P(float x, float y) { return FloatToString(x / 1280.0, 0, 4) + "," + FloatToString(y / 720.0, 0, 4); }
void Log(string s) { WriteTimestampedLogEntry(s); }
string Dir(int i)
{
    switch (i) { case 0: return "n"; case 1: return "ne"; case 2: return "e"; case 3: return "se"; case 4: return "s";
        case 5: return "sw"; case 6: return "w"; } return "nw";
}
float Clamp(float v, float lo, float hi) { return v < lo ? lo : v > hi ? hi : v; }
// Opens the ring on (x, y), shoots it with the pointer at the centre and on each hook, closes it; returns the time after.
float Ring(string name, float x, float y, float t)
{
    float cx = Clamp(x, 154.0, 1126.0), cy = Clamp(y, 154.0, 566.0);
    DelayCommand(t, Log("VISUAL|hover|" + P(x, y)));
    DelayCommand(t + 0.3, Log("VISUAL|mouse|" + P(x, y) + "|down|right"));
    DelayCommand(t + 0.6, Log("VISUAL|hover|" + P(cx, cy)));
    DelayCommand(t + 1.2, VShot(name + "_c"));
    t += 2.0;
    int i;
    for (i = 0; i < 8; i++)
    {
        float a = 45.0 * IntToFloat(i);
        DelayCommand(t, Log("VISUAL|hover|" + P(cx + 100.0 * sin(a), cy - 100.0 * cos(a))));
        DelayCommand(t + 0.6, VShot(name + "_" + Dir(i)));
        t += 1.2;
    }
    DelayCommand(t, Log("VISUAL|hover|" + P(cx, cy)));
    DelayCommand(t + 0.4, Log("VISUAL|key|101|tap"));
    DelayCommand(t + 0.7, Log("VISUAL|mouse|" + P(cx, cy) + "|up|right"));
    return t + 1.5;
}
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    object store = CreateObject(OBJECT_TYPE_STORE, "st_empty", GetLocation(pc), FALSE, "fixture_store");
    SetStoreGold(store, 300);
    Setup(pc, store);
    DelayCommand(1.0, Fill(pc, store));
    DelayCommand(2.0, VCamera(pc, 90.0, 12.0, 60.0));
    DelayCommand(4.0, OpenStore(store, pc));
    DelayCommand(6.0, VShot("a_layout"));
    float t = 7.0;
    // The player's items (backpack cells 33 px from (845, 220), row 0 at the top), then the robe she wears.
    t = Ring("p_sword", 861.0, 368.0, t);
    t = Ring("p_armour", 910.0, 368.0, t);
    t = Ring("p_kit", 960.0, 335.0, t);
    t = Ring("p_purple1", 960.0, 368.0, t);
    t = Ring("p_purple2", 960.0, 401.0, t);
    t = Ring("p_potions", 993.0, 385.0, t);
    t = Ring("p_arrows", 1026.0, 385.0, t);
    t = Ring("p_green1", 1059.0, 368.0, t);
    t = Ring("p_scroll", 1059.0, 401.0, t);
    t = Ring("p_bag", 1109.0, 385.0, t);
    t = Ring("p_round", 1158.0, 401.0, t);
    t = Ring("p_robe", 960.0, 70.0, t);
    // The store's wares, tab by tab.
    t = Ring("s_leather_inf", 48.0, 369.0, t);
    t = Ring("s_studded", 114.0, 369.0, t);
    DelayCommand(t, Click(114.0, 43.0)); t += 1.5;
    t = Ring("s_arrows", 30.0, 385.0, t);
    t = Ring("s_sword", 63.0, 352.0, t);
    DelayCommand(t, Click(180.0, 43.0)); t += 1.5;
    t = Ring("s_potion_inf", 30.0, 385.0, t);
    t = Ring("s_scroll", 63.0, 401.0, t);
    DelayCommand(t, Click(246.0, 43.0)); t += 1.5;
    t = Ring("s_ring_dear", 30.0, 401.0, t);
    DelayCommand(t, VShot("z_end"));
    DelayCommand(t + 1.0, VDone());
}
