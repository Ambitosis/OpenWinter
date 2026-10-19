// Polymorph and equipment: what happens to the creature's own items and the form's, how the form ends
// (expiry, RemoveEffect, dispel, death, resting, a new polymorph), and what copies and saved creatures carry.
//
// Left out on purpose (OpenWinter differs from the original by design):
// - Where an item taken from the creature while polymorphed ends up afterwards. The original puts it back in
//   the creature's slot while the taker keeps it as well (one object in two places: a duplicate); OpenWinter
//   leaves it with the taker.
// - Giving away the form's own items. The original lets the hide go to another creature's inventory while it
//   stays in the slot too; OpenWinter refuses.
// Also left out: EffectDeath on a creature in the drow form leaves it alive at 1 hit point (its bonus hit points;
// see fx_poly_death), so the death case uses the bear form.
// How long the original takes to change back varies (half a second when little else is going on, longer
// with more creatures about), so changing back is only sampled well before and well after.
#include "parity_fx"
#include "parity_items"

int NextCell()
{
    int n = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", n + 1);
    return n;
}

void Strip(object it)
{
    itemproperty ip = GetFirstItemProperty(it);
    while (GetIsItemPropertyValid(ip)) { RemoveItemProperty(it, ip); ip = GetNextItemProperty(it); }
}

object Give(object o, string sRes, string sKey, itemproperty ip, int nSlot)
{
    object it = CreateItemOnObject(sRes, o);
    Strip(it);
    AddItemProperty(DURATION_TYPE_PERMANENT, ip, it);
    SetLocalObject(o, sKey, it);
    if (nSlot >= 0) AssignCommand(o, ActionEquipItem(it, nSlot));
    return it;
}

// Equipped items with their properties.
string Slots(object o, int bProps = TRUE)
{
    string s = "";
    int n;
    for (n = 0; n < 18; n++)
    {
        object it = GetItemInSlot(n, o);
        if (GetIsObjectValid(it)) s += " " + IntToString(n) + "=" + GetResRef(it) + (bProps ? "{" + PIpList(it) + "}" : "");
    }
    return s;
}

string Stats(object o)
{
    return "ac=" + IntToString(GetAC(o)) + " str=" + IntToString(GetAbilityScore(o, 0)) + " dex=" + IntToString(GetAbilityScore(o, 1))
        + " con=" + IntToString(GetAbilityScore(o, 2)) + " int=" + IntToString(GetAbilityScore(o, 3)) + " wis=" + IntToString(GetAbilityScore(o, 4))
        + " cha=" + IntToString(GetAbilityScore(o, 5)) + " spot=" + IntToString(GetSkillRank(SKILL_SPOT, o)) + " listen=" + IntToString(GetSkillRank(SKILL_LISTEN, o))
        + " disc=" + IntToString(GetSkillRank(SKILL_DISCIPLINE, o)) + " conc=" + IntToString(GetSkillRank(SKILL_CONCENTRATION, o))
        + " bab=" + IntToString(GetBaseAttackBonus(o)) + " app=" + IntToString(GetAppearanceType(o)) + " race=" + IntToString(GetRacialType(o))
        + " size=" + IntToString(GetCreatureSize(o));
}

// Where one item is: who possesses it, which of o's slots it is in, whether o's inventory lists it.
string Where(object o, object it)
{
    if (!GetIsObjectValid(it)) return "gone";
    object p = GetItemPossessor(it);
    string s = p == o ? "held" : (GetIsObjectValid(p) ? "other:" + GetTag(p) : "none");
    int n;
    for (n = 0; n < 18; n++) if (GetItemInSlot(n, o) == it) s += ",slot" + IntToString(n);
    object x = GetFirstItemInInventory(o);
    while (GetIsObjectValid(x)) { if (x == it) s += ",inv"; x = GetNextItemInInventory(o); }
    return s;
}

string Mine(object o)
{
    return "helm:" + Where(o, GetLocalObject(o, "helm")) + " armor:" + Where(o, GetLocalObject(o, "armor"))
        + " sword:" + Where(o, GetLocalObject(o, "sword")) + " shield:" + Where(o, GetLocalObject(o, "shield"))
        + " ring:" + Where(o, GetLocalObject(o, "ring")) + " neck:" + Where(o, GetLocalObject(o, "neck"))
        + " boots:" + Where(o, GetLocalObject(o, "boots")) + " pack:" + Where(o, GetLocalObject(o, "pack"))
        + " f14:" + Where(o, GetLocalObject(o, "f14")) + " f15:" + Where(o, GetLocalObject(o, "f15"))
        + " f16:" + Where(o, GetLocalObject(o, "f16")) + " f17:" + Where(o, GetLocalObject(o, "f17")) + " f4:" + Where(o, GetLocalObject(o, "f4"))
        + " o16:" + Where(o, GetLocalObject(o, "o16"));
}

string Events(object o)
{
    string s = GetLocalString(GetModule(), "ev." + GetTag(o));
    SetLocalString(GetModule(), "ev." + GetTag(o), "");
    return s == "" ? "-" : s;
}

// Events are left out before polymorphing (spawning and dressing fire different events in the two engines,
// and some creatures spawn with random loot) and for objects copied or retrieved from a database.
void Watch(string k, object o, int bEvents = TRUE)
{
    PLog(k + ".stats", Stats(o));
    PLog(k + ".slots", Slots(o));
    PLog(k + ".mine", Mine(o));
    PLog(k + ".fx", PFxList(o));
    if (bEvents) PLog(k + ".ev", Events(o));
    else Events(o);
    PLog(k + ".hp", IntToString(GetCurrentHitPoints(o)) + "/" + IntToString(GetMaxHitPoints(o)) + " dead=" + IntToString(GetIsDead(o)));
}

void Short(string k, object o)
{
    PLog(k + ".app", IntToString(GetAppearanceType(o)) + " fx=" + PFxList(o) + " slots=" + Slots(o, FALSE));
}

void RememberForm(object o)
{
    SetLocalObject(o, "f14", GetItemInSlot(14, o));
    SetLocalObject(o, "f15", GetItemInSlot(15, o));
    SetLocalObject(o, "f16", GetItemInSlot(16, o));
    SetLocalObject(o, "f17", GetItemInSlot(17, o));
    SetLocalObject(o, "f4", GetItemInSlot(4, o));
}

// A creature wearing six items, each with one property that shows in Stats, and a seventh in its backpack.
object Dress(string k, string sRes = "nw_bandit001")
{
    object o = PFxSpawn(NextCell(), sRes);
    SetTag(o, k);
    int i;
    for (i = 5000; i <= 5012; i++) if (i != 5008) SetEventScript(o, i, "");
    SetLocalObject(o, "o16", GetItemInSlot(16, o));
    object armor = GetItemInSlot(1, o);
    if (GetIsObjectValid(armor)) { Strip(armor); AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyAbilityBonus(IP_CONST_ABILITY_INT, 2), armor); SetLocalObject(o, "armor", armor); }
    object sword = GetItemInSlot(4, o);
    if (GetIsObjectValid(sword)) { Strip(sword); AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertySkillBonus(SKILL_DISCIPLINE, 5), sword); SetLocalObject(o, "sword", sword); }
    Give(o, "nw_arhe001", "helm", ItemPropertyAbilityBonus(IP_CONST_ABILITY_WIS, 2), INVENTORY_SLOT_HEAD);
    Give(o, "nw_ashsw001", "shield", ItemPropertySkillBonus(SKILL_CONCENTRATION, 6), INVENTORY_SLOT_LEFTHAND);
    Give(o, "nw_it_mring001", "ring", ItemPropertyAbilityBonus(IP_CONST_ABILITY_CHA, 2), INVENTORY_SLOT_LEFTRING);
    Give(o, "nw_it_mneck001", "neck", ItemPropertySkillBonus(SKILL_SPOT, 3), INVENTORY_SLOT_NECK);
    Give(o, "nw_it_mboots001", "boots", ItemPropertySkillBonus(SKILL_LISTEN, 4), INVENTORY_SLOT_BOOTS);
    Give(o, "nw_it_mring002", "pack", ItemPropertyAbilityBonus(IP_CONST_ABILITY_STR, 1), -1);
    SetTag(GetLocalObject(o, "helm"), "HELM_" + k);
    return o;
}

// A form for 6 s: before, during and after.
void Form(string k, int nPoly, string sRes = "nw_bandit001")
{
    object o = Dress(k, sRes);
    DelayCommand(1.5, Watch(k + ".before", o, FALSE));
    DelayCommand(1.6, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectPolymorph(nPoly), o, 6.0));
    DelayCommand(1.65, RememberForm(o));
    DelayCommand(1.7, Watch(k + ".during", o));
    DelayCommand(9.0, Watch(k + ".after", o));
}

void Unpoly(object o)
{
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { if (GetEffectType(e) == EFFECT_TYPE_POLYMORPH) RemoveEffect(o, e); e = GetNextEffect(o); }
}

void Copy(object o)
{
    object c = CopyObject(o, PCentre(0.0, 18.0), OBJECT_INVALID, "copy_" + GetTag(o));
    DelayCommand(0.2, PLog("copy.now", Stats(c) + " /" + Slots(c, FALSE) + " / fx=" + PFxList(c)));
}

void Sql(object o)
{
    sqlquery q = SqlPrepareQueryObject(GetModule(), "CREATE TABLE IF NOT EXISTS pp (o BLOB)");
    SqlStep(q);
    q = SqlPrepareQueryObject(GetModule(), "INSERT INTO pp (o) VALUES (@o)");
    SqlBindObject(q, "@o", o);
    SqlStep(q);
    q = SqlPrepareQueryObject(GetModule(), "SELECT o FROM pp");
    if (SqlStep(q))
    {
        object c = SqlGetObject(q, 0, PCentre(3.0, 18.0));
        DelayCommand(0.2, PLog("sql.now", Stats(c) + " /" + Slots(c, FALSE) + " / fx=" + PFxList(c)));
    }
    else PLog("sql.now", "no row");
}

void Campaign(object o)
{
    PLogI("camp.stored", StoreCampaignObject("pp_db", "o", o));
    object c = RetrieveCampaignObject("pp_db", "o", PCentre(-3.0, 18.0));
    DelayCommand(0.2, PLog("camp.now", Stats(c) + " /" + Slots(c, FALSE) + " / fx=" + PFxList(c)));
}

void Lookup(string k, object o)
{
    object helm = GetLocalObject(o, "helm");
    PLogI(k + ".possessor", GetItemPossessor(helm) == o);
    object found = GetItemPossessedBy(o, "HELM_" + GetTag(o));
    PLogI(k + ".possessedby", GetIsObjectValid(found) && found == helm);
}

void TryEquip(object o)
{
    AssignCommand(o, ActionEquipItem(GetLocalObject(o, "pack"), INVENTORY_SLOT_RIGHTRING));
    AssignCommand(o, ActionUnequipItem(GetItemInSlot(17, o)));
    AssignCommand(o, ActionUnequipItem(GetItemInSlot(4, o)));
}

// Polymorph into a drow (a hide and a weapon) for 30 s; at 3 s do something; watch at 3.3 s and 3.7 s
// (the original changes back half a second after the effect goes) and at 8 s.
void Ender(string k, int nAction)
{
    object o = Dress(k);
    if (nAction == 3) AssignCommand(o, SetIsDestroyable(FALSE, FALSE, FALSE));
    object other = PFxSpawn(NextCell(), "nw_bandit001");
    SetTag(other, "OTHER_" + k);
    int i;
    for (i = 5000; i <= 5012; i++) if (i != 5008) SetEventScript(other, i, "");
    // Right next to it, so giving and taking happen at once.
    DelayCommand(0.5, AssignCommand(other, JumpToLocation(Location(GetArea(o), GetPosition(o) + Vector(1.0, 0.0, 0.0), 0.0))));
    SetLocalObject(o, "other", other);
    DelayCommand(1.5, Watch(k + ".before", o, FALSE));
    effect ePoly = EffectPolymorph(nAction == 3 ? POLYMORPH_TYPE_BROWN_BEAR : POLYMORPH_TYPE_MALE_DROW);
    if (nAction == 11) ePoly = SupernaturalEffect(ePoly);
    DelayCommand(1.6, ApplyEffectToObject(nAction == 11 ? DURATION_TYPE_PERMANENT : DURATION_TYPE_TEMPORARY, ePoly, o, 30.0));
    DelayCommand(1.65, RememberForm(o));
    DelayCommand(1.7, Watch(k + ".during", o));
    DelayCommand(1.75, Lookup(k + ".during", o));
    if (nAction == 1) DelayCommand(3.0, Unpoly(o));
    if (nAction == 2) DelayCommand(3.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDispelMagicAll(40), o));
    if (nAction == 3) DelayCommand(3.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), o));
    if (nAction == 4 || nAction == 11) DelayCommand(3.0, ForceRest(o));
    if (nAction == 5) DelayCommand(3.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectPolymorph(POLYMORPH_TYPE_WEREWOLF), o, 2.0));
    if (nAction == 6) DelayCommand(3.0, TryEquip(o));
    if (nAction == 7) DelayCommand(3.0, Copy(o));
    if (nAction == 8) DelayCommand(3.0, Sql(o));
    if (nAction == 9) DelayCommand(3.0, Campaign(o));
    if (nAction == 10) DelayCommand(3.0, DestroyObject(GetLocalObject(o, "helm")));
    if (nAction == 12) DelayCommand(3.0, AssignCommand(other, ActionTakeItem(GetLocalObject(o, "armor"), o)));
    if (nAction == 13) DelayCommand(3.0, AssignCommand(o, ActionGiveItem(GetLocalObject(o, "ring"), other)));
    if (nAction == 14) DelayCommand(3.0, DestroyObject(GetItemInSlot(17, o)));
    if (nAction == 15) DelayCommand(3.0, DestroyObject(o));
    int bEvents = nAction < 7 || nAction > 9;
    DelayCommand(3.3, Short(k + ".t33", o));
    if (nAction == 15)
    {
        DelayCommand(4.5, PLog(k + ".t45.mine", Mine(o)));
        return;
    }
    DelayCommand(4.5, Watch(k + ".t45", o, bEvents));
    DelayCommand(4.55, Lookup(k + ".t45", o));
    DelayCommand(8.0, Watch(k + ".late", o, bEvents));
    DelayCommand(8.05, Lookup(k + ".late", o));
}

// A short polymorph: just before it expires, while it waits, and once the creature changed back.
void Expire()
{
    string k = "expire";
    object o = Dress(k);
    DelayCommand(1.6, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectPolymorph(POLYMORPH_TYPE_MALE_DROW), o, 3.0));
    DelayCommand(4.5, Short(k + ".t45", o));
    DelayCommand(4.8, Short(k + ".t48", o));
    DelayCommand(6.5, Short(k + ".t65", o));
    DelayCommand(6.5, PLog(k + ".t65.mine", Mine(o)));
}

void Run()
{
    Form("werewolf", POLYMORPH_TYPE_WEREWOLF);
    Form("bear", POLYMORPH_TYPE_BROWN_BEAR);
    Form("drow", POLYMORPH_TYPE_MALE_DROW);
    Form("golem", POLYMORPH_TYPE_IRON_GOLEM);
    Form("mino", POLYMORPH_TYPE_MINOTAUR);
    Form("doom", POLYMORPH_TYPE_DOOM_KNIGHT);
    Form("wolfself", POLYMORPH_TYPE_BROWN_BEAR, "nw_wolf");
    Ender("remove", 1);
    Ender("dispel", 2);
    Ender("death", 3);
    Ender("rest", 4);
    Ender("nested", 5);
    Ender("equip", 6);
    Ender("copy", 7);
    Ender("sql", 8);
    Ender("camp", 9);
    Ender("killhelm", 10);
    Ender("restsuper", 11);
    Ender("takearmor", 12);
    Ender("givering", 13);
    Ender("killhide", 14);
    Ender("destroy", 15);
    Expire();
    DelayCommand(11.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}
