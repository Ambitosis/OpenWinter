// Store prices the player pays and gets: mark-up and mark-down, rounding, OpenStore's bonus, Appraise, the black
// market and the stolen flag, MaxBuyPrice and the store's gold. Each trade is a held-button drag (posted, no real
// mouse) from the store's bottom-left cell to the backpack, or from the backpack's bottom-left cell to the store,
// with the gold logged before and after.
#include "visual_inc"

const float SLOT = 7.0;

void Mouse(float x, float y, string s)
{
    WriteTimestampedLogEntry("VISUAL|mouse|" + FloatToString(x / 1280.0, 0, 6) + "," + FloatToString(y / 720.0, 0, 6) + "|" + s);
}

void Drag(float x1, float y1, float x2, float y2)
{
    Mouse(x1, y1, "up");
    DelayCommand(1.0, Mouse(x1, y1, "down"));
    DelayCommand(2.0, Mouse(x2, y2, "down"));
    DelayCommand(3.0, Mouse(x2, y2, "up"));
}

object PC() { return GetFirstPC(); }
object Store(string tag) { return GetObjectByTag(tag); }

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }

void ClearPack()
{
    object i = GetFirstItemInInventory(PC());
    while (GetIsObjectValid(i)) { DestroyObject(i); i = GetNextItemInInventory(PC()); }
}

void ClearStore(object s)
{
    object i = GetFirstItemInInventory(s);
    while (GetIsObjectValid(i)) { DestroyObject(i); i = GetNextItemInInventory(s); }
}

void AddWare(object s, string resref)
{
    object w = CreateItemOnObject(resref, s);
    SetInfiniteFlag(w, TRUE);
}

void GivePc(string resref, int n, int nCharges, int bUnidentified, int bStolen)
{
    object o = CreateItemOnObject(resref, PC(), n);
    if (nCharges > 0) SetItemCharges(o, nCharges);
    if (bUnidentified) SetIdentified(o, FALSE);
    if (bStolen) SetStolenFlag(o, TRUE);
}

string Pack()
{
    string r = "";
    object i = GetFirstItemInInventory(PC());
    while (GetIsObjectValid(i))
    {
        r += GetResRef(i) + ":" + IntToString(GetItemStackSize(i)) + ":" + IntToString(GetGoldPieceValue(i)) + " ";
        i = GetNextItemInInventory(PC());
    }
    return r;
}

void Before(string k, object s)
{
    object w = GetFirstItemInInventory(s);
    Note(k + "|before|gold=" + IntToString(GetGold(PC())) + "|storegold=" + IntToString(GetStoreGold(s))
        + "|ware=" + GetResRef(w) + ":" + IntToString(GetGoldPieceValue(w)) + "|pack=" + Pack()
        + "|appraise=" + IntToString(GetSkillRank(SKILL_APPRAISE, PC())));
}

void After(string k, object s)
{
    Note(k + "|after|gold=" + IntToString(GetGold(PC())) + "|storegold=" + IntToString(GetStoreGold(s)) + "|pack=" + Pack());
}

// One purchase of resref from store tag (its only, infinite, ware) at time t.
void Buy(float t, string k, string tag, string resref)
{
    object s = Store(tag);
    DelayCommand(t, ClearPack());
    DelayCommand(t, ClearStore(s));
    DelayCommand(t + 0.5, AddWare(s, resref));
    DelayCommand(t + 1.5, Before(k, s));
    DelayCommand(t + 2.0, Drag(31.0, 400.0, 1000.0, 300.0));
    DelayCommand(t + 6.5, After(k, s));
}

// One sale of a new item to store tag at time t.
void Sell(float t, string k, string tag, string resref, int n = 1, int nCharges = 0, int bUnidentified = FALSE, int bStolen = FALSE)
{
    object s = Store(tag);
    DelayCommand(t, ClearPack());
    DelayCommand(t + 0.3, GivePc(resref, n, nCharges, bUnidentified, bStolen));
    DelayCommand(t + 1.5, Before(k, s));
    DelayCommand(t + 2.0, Drag(861.0, 400.0, 165.0, 250.0));
    DelayCommand(t + 6.5, After(k, s));
}

void Open(float t, string tag, int up = 0, int down = 0)
{
    DelayCommand(t, OpenStore(Store(tag), PC(), up, down));
}

void Appraise(int bOn)
{
    effect e = GetFirstEffect(PC());
    while (GetIsEffectValid(e)) { if (GetEffectTag(e) == "APPRAISE") RemoveEffect(PC(), e); e = GetNextEffect(PC()); }
    if (bOn) ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectSkillIncrease(SKILL_APPRAISE, 50), "APPRAISE"), PC());
}

void Terms(string k, object s) { Note(k + "|gold=" + IntToString(GetStoreGold(s)) + "|maxbuy=" + IntToString(GetStoreMaxBuyPrice(s)) + "|identify=" + IntToString(GetStoreIdentifyCost(s))); }

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    GiveGoldToCreature(pc, 10000);
    location l = GetLocation(pc);
    CreateObject(OBJECT_TYPE_STORE, "st_a", l, FALSE, "ST_A");
    CreateObject(OBJECT_TYPE_STORE, "st_b", l, FALSE, "ST_B");
    CreateObject(OBJECT_TYPE_STORE, "st_bm", l, FALSE, "ST_BM");
    ClearPack();
    DelayCommand(1.0, Terms("terms.a", Store("ST_A")));
    DelayCommand(1.0, Terms("terms.bm", Store("ST_BM")));
    DelayCommand(2.0, VCamera(pc, 90.0, 12.0, 60.0));
    DelayCommand(3.0, OpenStore(Store("ST_A"), pc));
    float t = 4.0;
    Buy(t, "buy.a.leather", "ST_A", "nw_aarcl001"); t += SLOT;
    Buy(t, "buy.a.cloth", "ST_A", "nw_cloth001"); t += SLOT;
    Buy(t, "buy.a.helmet", "ST_A", "nw_arhe001"); t += SLOT;
    Open(t, "ST_B");
    Buy(t, "buy.b.leather", "ST_B", "nw_aarcl001"); t += SLOT;
    Buy(t, "buy.b.cloth", "ST_B", "nw_cloth001"); t += SLOT;
    Buy(t, "buy.b.helmet", "ST_B", "nw_arhe001"); t += SLOT;
    Open(t, "ST_A", 20, -10);
    Buy(t, "buy.a.bonus.leather", "ST_A", "nw_aarcl001"); t += SLOT;
    Open(t, "ST_A");
    DelayCommand(t, Appraise(TRUE));
    Buy(t, "buy.a.appraise.leather", "ST_A", "nw_aarcl001"); t += SLOT;
    Sell(t, "sell.a.appraise.longsword", "ST_A", "nw_wswls001"); t += SLOT;
    DelayCommand(t, Appraise(FALSE));
    Sell(t, "sell.a.longsword", "ST_A", "nw_wswls001"); t += SLOT;
    Sell(t, "sell.a.gem", "ST_A", "nw_it_gem001"); t += SLOT;
    Sell(t, "sell.a.unidentified", "ST_A", "nw_wswmls002", 1, 0, TRUE); t += SLOT;
    Sell(t, "sell.a.potions3", "ST_A", "nw_it_mpotion001", 3); t += SLOT;
    Sell(t, "sell.a.wand3", "ST_A", "nw_wmgwn006", 1, 3); t += SLOT;
    Sell(t, "sell.a.leather", "ST_A", "nw_aarcl001"); t += SLOT;
    Sell(t, "sell.a.ring", "ST_A", "nw_it_mring001"); t += SLOT;
    Open(t, "ST_B");
    Sell(t, "sell.b.longsword", "ST_B", "nw_wswls001"); t += SLOT;
    Sell(t, "sell.b.gem", "ST_B", "nw_it_gem001"); t += SLOT;
    Open(t, "ST_A", 20, -10);
    Sell(t, "sell.a.bonus.longsword", "ST_A", "nw_wswls001"); t += SLOT;
    Open(t, "ST_BM");
    Sell(t, "sell.bm.stolen", "ST_BM", "nw_wswls001", 1, 0, FALSE, TRUE); t += SLOT;
    Sell(t, "sell.bm.plain", "ST_BM", "nw_wswls001"); t += SLOT;
    Open(t, "ST_A");
    DelayCommand(t, SetStoreMaxBuyPrice(Store("ST_A"), 10));
    Sell(t, "sell.a.maxbuy10", "ST_A", "nw_wswls001"); t += SLOT;
    DelayCommand(t, SetStoreMaxBuyPrice(Store("ST_A"), -1));
    DelayCommand(t, SetStoreGold(Store("ST_A"), 100));
    Sell(t, "sell.a.storegold100", "ST_A", "nw_wswls001"); t += SLOT;
    DelayCommand(t, SetStoreGold(Store("ST_A"), 5));
    Sell(t, "sell.a.storegold5", "ST_A", "nw_wswls001"); t += SLOT;
    Sell(t, "sell.a.stolen", "ST_A", "nw_wswls001", 1, 0, FALSE, TRUE); t += SLOT;
    DelayCommand(t, VShot("z_end"));
    DelayCommand(t + 1.0, VDone());
}
