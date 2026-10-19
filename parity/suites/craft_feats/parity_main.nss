// Crafting runs on stock scripts: Brew Potion, Scribe Scroll and Craft Wand (x2_pc_craft / x2_inc_craft, reached
// from a spell cast at a blank item), item appearance modification (x2_inc_itemprop / x2_inc_craft, the
// x0_skill_ctrap conversation). This suite runs those stock functions on both
// engines and logs what they make, take and return, so the engine functions under them are compared.
#include "parity_items"
#include "x2_inc_craft"

string Pack(object o)
{
    string s = "";
    object i = GetFirstItemInInventory(o);
    while (GetIsObjectValid(i))
    {
        s += GetResRef(i) + ":" + IntToString(GetBaseItemType(i)) + ":s" + IntToString(GetItemStackSize(i))
            + ":c" + IntToString(GetItemCharges(i)) + ":i" + IntToString(GetIdentified(i)) + ":v" + IntToString(GetGoldPieceValue(i))
            + ":[" + PIpList(i) + "] ";
        i = GetNextItemInInventory(o);
    }
    return s;
}

object Crafter(string tag, string resref, float x, float y, int nGold, int nXP)
{
    object c = CreateObject(OBJECT_TYPE_CREATURE, resref, PCentre(x, y), FALSE, tag);
    SetAILevel(c, AI_LEVEL_NORMAL);
    SetPlotFlag(c, TRUE);
    ChangeToStandardFaction(c, STANDARD_FACTION_COMMONER);
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(c, e, "");
    object i = GetFirstItemInInventory(c);
    while (GetIsObjectValid(i)) { DestroyObject(i); i = GetNextItemInInventory(c); }
    TakeGoldFromCreature(GetGold(c), c, TRUE);
    if (nGold > 0) GiveGoldToCreature(c, nGold);
    SetXP(c, nXP);
    SetLocalString(c, "case", tag);
    return c;
}

void Report(object c, int nSpell)
{
    string k = GetTag(c);
    object w = GetItemPossessedBy(c, "x2_it_pcwand");
    if (k == "wandmm" && GetIsObjectValid(w))
    {
        // Charges are the caster level + d20: compared as a range, then set to 20 for the rest.
        int n = GetItemCharges(w);
        PLogI(k + ".chargesinrange", n >= 11 && n <= 30);
        SetItemCharges(w, 20);
        w = CopyItem(w, c, TRUE);
        DestroyObject(GetItemPossessedBy(c, "x2_it_pcwand"));
        PLog(k + ".wand", GetResRef(w) + ":c" + IntToString(GetItemCharges(w)) + ":v" + IntToString(GetGoldPieceValue(w)) + ":[" + PIpList(w) + "]");
        return;
    }
    PLog(k + ".pack", Pack(c));
    PLogI(k + ".gold", GetGold(c));
    PLogI(k + ".xp", GetXP(c));
    PLogI(k + ".hasspell", GetHasSpell(nSpell, c));
    PLogI(k + ".blank", GetIsObjectValid(GetLocalObject(c, "blank")));
}

// A crafter casts nSpell at a blank item (or another item) in its own backpack.
void Case(string tag, string crafter, string blank, int nSpell, float x, float y, int nGold = 50000, int nXP = 100000)
{
    object c = Crafter(tag, crafter, x, y, nGold, nXP);
    object b = CreateItemOnObject(blank, c);
    SetLocalObject(c, "blank", b);
    PLogI(tag + ".setup", GetXP(c) * 0 + GetHasFeat(FEAT_SCRIBE_SCROLL, c) * 100 + GetHasFeat(FEAT_BREW_POTION, c) * 10 + GetHasFeat(FEAT_CRAFT_WAND, c));
    PLog(tag + ".start", "gold=" + IntToString(GetGold(c)) + " xp=" + IntToString(GetXP(c)) + " hd=" + IntToString(GetHitDice(c)) + " hasspell=" + IntToString(GetHasSpell(nSpell, c)));
    AssignCommand(c, ActionCastSpellAtObject(nSpell, b, METAMAGIC_NONE, FALSE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    DelayCommand(7.0, Report(c, nSpell));
}

// A crafter uses a scroll of Magic Missile on a blank scroll.
void ItemCase(float x, float y)
{
    object c = Crafter("itemcast", "crafter", x, y, 50000, 100000);
    object b = CreateItemOnObject("x2_it_cfm_bscrl", c);
    object s = CreateItemOnObject("nw_it_sparscr107", c);
    SetLocalObject(c, "blank", b);
    itemproperty ip = GetFirstItemProperty(s);
    AssignCommand(c, ActionUseItemOnObject(s, ip, b));
    DelayCommand(7.0, Report(c, SPELL_MAGIC_MISSILE));
}

void Crafts()
{
    Case("scroll.mm", "crafter", "x2_it_cfm_bscrl", SPELL_MAGIC_MISSILE, -12.0, -12.0);
    Case("scroll.bull", "crafter", "x2_it_cfm_bscrl", SPELL_BULLS_STRENGTH, -8.0, -12.0);
    Case("potion.armor", "crafter", "x2_it_cfm_pbottl", SPELL_MAGE_ARMOR, -4.0, -12.0);
    Case("potion.bull", "crafter", "x2_it_cfm_pbottl", SPELL_BULLS_STRENGTH, 0.0, -12.0);
    Case("potion.mm", "crafter", "x2_it_cfm_pbottl", SPELL_MAGIC_MISSILE, 4.0, -12.0);
    Case("wand.mm", "crafter", "x2_it_cfm_wand", SPELL_MAGIC_MISSILE, 8.0, -12.0);
    Case("nofeat.scroll", "nocraft", "x2_it_cfm_bscrl", SPELL_MAGIC_MISSILE, -12.0, -6.0);
    Case("nogold.scroll", "crafter", "x2_it_cfm_bscrl", SPELL_MAGIC_MISSILE, -8.0, -6.0, 0);
    Case("noxp.potion", "crafter", "x2_it_cfm_pbottl", SPELL_MAGE_ARMOR, -4.0, -6.0, 50000, 45010);
    Case("sword.mm", "crafter", "nw_wswls001", SPELL_MAGIC_MISSILE, 0.0, -6.0);
    ItemCase(4.0, -6.0);
}

void Crafts2()
{
    SetLocalInt(GetModule(), MODULE_SWITCH_ENABLE_CRAFT_WAND_50_CHARGES, TRUE);
    Case("wand50.mm", "crafter", "x2_it_cfm_wand", SPELL_MAGIC_MISSILE, 8.0, -6.0);
    DelayCommand(5.0, DeleteLocalInt(GetModule(), MODULE_SWITCH_ENABLE_CRAFT_WAND_50_CHARGES));
}

void Crafts3()
{
    SetLocalInt(GetModule(), MODULE_SWITCH_DISABLE_ITEM_CREATION_FEATS, TRUE);
    Case("disabled.scroll", "crafter", "x2_it_cfm_bscrl", SPELL_MAGIC_MISSILE, 12.0, -6.0);
    DelayCommand(5.0, DeleteLocalInt(GetModule(), MODULE_SWITCH_DISABLE_ITEM_CREATION_FEATS));
}

string Appearance(object o)
{
    string s = "";
    int i;
    if (GetBaseItemType(o) == BASE_ITEM_ARMOR)
        for (i = 0; i < ITEM_APPR_ARMOR_NUM_MODELS; i++) s += IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_ARMOR_MODEL, i)) + ",";
    else
        for (i = 0; i <= 2; i++) s += IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_WEAPON_MODEL, i)) + "/" + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_WEAPON_COLOR, i)) + ",";
    return s;
}

// CopyItemAndModify at and past the ends of each range.
void Cim()
{
    object chest = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(12.0, 16.0), FALSE, "CIMCHEST");
    object a = CreateItemOnObject("nw_aarcl004", chest);
    object w = CreateItemOnObject("nw_wswls001", chest);
    string s = "";
    int i;
    for (i = 0; i < 7; i++)
    {
        int v = i == 0 ? -1 : i == 1 ? 0 : i == 2 ? 1 : i == 3 ? 255 : i == 4 ? 256 : i == 5 ? 65535 : 70000;
        object m = CopyItemAndModify(a, ITEM_APPR_TYPE_ARMOR_MODEL, ITEM_APPR_ARMOR_MODEL_ROBE, v, TRUE);
        object t = CopyItemAndModify(a, ITEM_APPR_TYPE_ARMOR_MODEL, ITEM_APPR_ARMOR_MODEL_LSHIN, v, TRUE);
        object c = CopyItemAndModify(a, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_LEATHER1, v, TRUE);
        object x = CopyItemAndModify(w, ITEM_APPR_TYPE_WEAPON_MODEL, ITEM_APPR_WEAPON_MODEL_TOP, v, TRUE);
        object y = CopyItemAndModify(w, ITEM_APPR_TYPE_WEAPON_COLOR, ITEM_APPR_WEAPON_MODEL_TOP, v, TRUE);
        s += IntToString(v) + ":" + IntToString(GetIsObjectValid(m)) + "/" + IntToString(GetItemAppearance(m, ITEM_APPR_TYPE_ARMOR_MODEL, ITEM_APPR_ARMOR_MODEL_ROBE))
            + "," + IntToString(GetIsObjectValid(t)) + "/" + IntToString(GetItemAppearance(t, ITEM_APPR_TYPE_ARMOR_MODEL, ITEM_APPR_ARMOR_MODEL_LSHIN))
            + "," + IntToString(GetIsObjectValid(c)) + "/" + IntToString(GetItemAppearance(c, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_LEATHER1))
            + "," + IntToString(GetIsObjectValid(x)) + "/" + IntToString(GetItemAppearance(x, ITEM_APPR_TYPE_WEAPON_MODEL, ITEM_APPR_WEAPON_MODEL_TOP))
            + "," + IntToString(GetIsObjectValid(y)) + "/" + IntToString(GetItemAppearance(y, ITEM_APPR_TYPE_WEAPON_COLOR, ITEM_APPR_WEAPON_MODEL_TOP)) + " ";
    }
    PLog("cim.ranges", s);
    PLog("cim.nwn2", IntToString(GetItemAppearance(a, ITEM_APPR_TYPE_ARMOR_MODEL, 19)) + " " + IntToString(GetIsObjectValid(CopyItemAndModify(a, ITEM_APPR_TYPE_ARMOR_MODEL, 19, 3, TRUE)))
        + " " + IntToString(GetIsObjectValid(CopyItemAndModify(a, 7, 0, 3, TRUE))) + " " + Appearance(CopyItemAndModify(w, ITEM_APPR_TYPE_WEAPON_MODEL, 3, 3, TRUE)) + " " + Appearance(w));
}

// Item appearance modification as the x2_im_* scripts do it: the next/previous part, its cost and DC.
void Modify()
{
    object chest = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(12.0, 12.0), FALSE, "MODCHEST");
    string armors = "nw_aarcl004 nw_aarcl001 nw_aarcl012 nw_cloth001 ";
    int n;
    for (n = 0; n < 4; n++)
    {
        string res = n == 0 ? "nw_aarcl004" : n == 1 ? "nw_aarcl001" : n == 2 ? "nw_aarcl012" : "nw_cloth001";
        object a = CreateItemOnObject(res, chest);
        object backup = CopyItem(a, chest, TRUE);
        string k = "mod.armor." + res;
        PLog(k + ".start", Appearance(a) + " v" + IntToString(GetGoldPieceValue(a)) + " ac" + IntToString(GetItemACValue(a)));
        int part;
        for (part = 0; part < ITEM_APPR_ARMOR_NUM_MODELS; part += 3)
        {
            object b = CopyItem(backup, chest, TRUE);
            object next = IPGetModifiedArmor(b, part, X2_IP_ARMORTYPE_NEXT, TRUE);
            object prev = IPGetModifiedArmor(CopyItem(backup, chest, TRUE), part, X2_IP_ARMORTYPE_PREV, TRUE);
            PLog(k + ".part" + IntToString(part), IntToString(GetItemAppearance(next, ITEM_APPR_TYPE_ARMOR_MODEL, part)) + " "
                + IntToString(GetItemAppearance(prev, ITEM_APPR_TYPE_ARMOR_MODEL, part)) + " cost" + IntToString(CIGetArmorModificationCost(backup, next))
                + " dc" + IntToString(CIGetArmorModificationDC(backup, next)) + " pc" + IntToString(CIGetItemPartModificationCost(backup, part))
                + " pdc" + IntToString(CIGetItemPartModificationDC(backup, part)) + " v" + IntToString(GetGoldPieceValue(next))
                + " tag" + IntToString(GetTag(next) == GetTag(backup)) + " same" + IntToString(next == b) + " oldvalid" + IntToString(GetIsObjectValid(b)));
        }
        // Torso next twice, then a robe.
        object t1 = IPGetModifiedArmor(CopyItem(backup, chest, TRUE), ITEM_APPR_ARMOR_MODEL_TORSO, X2_IP_ARMORTYPE_NEXT, TRUE);
        object t2 = IPGetModifiedArmor(t1, ITEM_APPR_ARMOR_MODEL_TORSO, X2_IP_ARMORTYPE_NEXT, TRUE);
        PLog(k + ".torso2", Appearance(t2) + " cost" + IntToString(CIGetArmorModificationCost(backup, t2)) + " dc" + IntToString(CIGetArmorModificationDC(backup, t2)));
        object c1 = CopyItemAndModify(backup, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_CLOTH1, 5, TRUE);
        PLog(k + ".color", IntToString(GetItemAppearance(backup, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_CLOTH1)) + " "
            + IntToString(GetItemAppearance(c1, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_CLOTH1)) + " cost" + IntToString(CIGetArmorModificationCost(backup, c1)));
    }
    for (n = 0; n < 3; n++)
    {
        string res = n == 0 ? "nw_wswls001" : n == 1 ? "nw_wswmls002" : "nw_waxbt001";
        object w = CreateItemOnObject(res, chest);
        object backup = CopyItem(w, chest, TRUE);
        string k = "mod.weapon." + res;
        PLog(k + ".start", Appearance(w) + " v" + IntToString(GetGoldPieceValue(w)));
        int part;
        for (part = 0; part <= 2; part++)
        {
            object next = IPGetModifiedWeapon(CopyItem(backup, chest, TRUE), part, X2_IP_WEAPONTYPE_NEXT, TRUE);
            object prev = IPGetModifiedWeapon(CopyItem(backup, chest, TRUE), part, X2_IP_WEAPONTYPE_PREV, TRUE);
            PLog(k + ".part" + IntToString(part), Appearance(next) + " " + Appearance(prev) + " cost" + IntToString(CIGetWeaponModificationCost(backup, next)));
        }
        object col = CopyItemAndModify(backup, ITEM_APPR_TYPE_WEAPON_COLOR, ITEM_APPR_WEAPON_MODEL_TOP, 3, TRUE);
        PLog(k + ".color", Appearance(col));
    }
}

// Mundane Craft Weapon / Craft Armor (x2_s2_crafting and the x2_p_craftskills conversation): the recipe tables
// as the stock code reads them, and CIUseCraftItemSkill at a sure success and a sure failure.
void Mundane()
{
    int t, row, i;
    for (t = 0; t < 2; t++)
    {
        string tda = t == 0 ? X2_CI_CRAFTING_WP_2DA : X2_CI_CRAFTING_AR_2DA;
        string s = "";
        for (row = 0; row < 12; row++)
            for (i = 0; i < X2_CI_CRAFTING_ITEMS_PER_ROW; i++)
            {
                struct craft_struct st = CIGetCraftItemStructFrom2DA(tda, row, i);
                if (st.sLabel == "") break;
                s += IntToString(row) + "." + IntToString(i) + ":" + st.sResRef + ":" + IntToString(st.nDC) + ":" + IntToString(st.nCost) + " ";
            }
        PLog("mundane." + tda, s);
    }
    object good = Crafter("mundanegood", "crafter", 16.0, 0.0, 0, 0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_CRAFT_WEAPON, 60), good);
    object bad = Crafter("mundanebad", "nocraft", 16.0, 4.0, 0, 0);
    struct craft_struct a = CIGetCraftItemStructFrom2DA(X2_CI_CRAFTING_WP_2DA, 1, 0);
    struct craft_struct b = CIGetCraftItemStructFrom2DA(X2_CI_CRAFTING_WP_2DA, 1, 1);
    object n1 = CIUseCraftItemSkill(good, SKILL_CRAFT_WEAPON, a.sResRef, a.nDC);
    object n2 = CIUseCraftItemSkill(good, SKILL_CRAFT_WEAPON, b.sResRef, b.nDC, good);
    object n3 = CIUseCraftItemSkill(bad, SKILL_CRAFT_WEAPON, a.sResRef, 100);
    PLog("mundane.use", IntToString(GetIsObjectValid(n1)) + " " + GetResRef(n1) + " " + IntToString(GetItemStackSize(n1)) + " " + GetTag(GetItemPossessor(n1))
        + " | " + IntToString(GetIsObjectValid(n2)) + " " + GetResRef(n2) + " " + IntToString(GetItemStackSize(n2)) + " " + GetTag(GetItemPossessor(n2))
        + " | " + IntToString(GetIsObjectValid(n3)));
    DelayCommand(1.0, PLog("mundane.good", Pack(good)));
    DelayCommand(1.0, PLog("mundane.bad", Pack(bad)));
}

void main()
{
    DelayCommand(0.5, Crafts());
    DelayCommand(1.0, Modify());
    DelayCommand(1.5, Cim());
    DelayCommand(2.0, Mundane());
    DelayCommand(9.0, Crafts2());
    DelayCommand(17.0, Crafts3());
    DelayCommand(26.0, PDone());
}
