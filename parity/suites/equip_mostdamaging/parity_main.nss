// ActionEquipMostDamagingMelee / ActionEquipMostDamagingRanged: the melee score is the maximum damage
// of one non-critical hit (damage reduction and immunities of oVersus ignored, versus-race bonuses counted), ties go
// to the later inventory item for a non-monk, container contents and the off hand's size limit; the ranged choice
// by attack bonus (ties keep the earlier one), then the ammunition by damage, and each falling back to the other.
#include "parity_inc"

object Area() { return GetObjectByTag("PARITY_AREA"); }
location L(float x, float y) { return Location(Area(), Vector(x, y, 0.0), 0.0); }

int g_n = 0;

object Actor(string k, string sRes = "em_fighter")
{
    int n = GetLocalInt(GetModule(), "NACT");
    SetLocalInt(GetModule(), "NACT", n + 1);
    float x = 4.0 + 5.0 * IntToFloat(n % 12), y = 4.0 + 5.0 * IntToFloat(n / 12);
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, L(x, y), FALSE, "A_" + k);
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    ChangeToStandardFaction(o, STANDARD_FACTION_COMMONER);
    SetAILevel(o, AI_LEVEL_NORMAL);
    SetLocalString(GetModule(), "CASE" + IntToString(n), k);
    return o;
}

object Dummy(string sTag, float x)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", L(x, 70.0), FALSE, sTag);
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    ChangeToStandardFaction(o, STANDARD_FACTION_MERCHANT);
    return o;
}

object Item(object a, string sRes, string sTag, int nStack = 1)
{
    object i = CreateItemOnObject(sRes, a, nStack, sTag);
    SetIdentified(i, TRUE);
    return i;
}

void Prop(object i, itemproperty p) { AddItemProperty(DURATION_TYPE_PERMANENT, p, i); }

string T(object o) { return GetIsObjectValid(o) ? GetTag(o) : "none"; }

void Melee(object a, object v = OBJECT_INVALID, int bOff = FALSE)
{
    SetLocalInt(a, "KIND", 1);
    SetLocalObject(a, "VS", v);
    SetLocalInt(a, "OFF", bOff);
}
void Ranged(object a, object v = OBJECT_INVALID)
{
    SetLocalInt(a, "KIND", 2);
    SetLocalObject(a, "VS", v);
}

void Setup()
{
    object vHuman = Dummy("D_HUMAN", 10.0);
    object vImmune = Dummy("D_IMMUNE", 15.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageImmunityIncrease(DAMAGE_TYPE_SLASHING, 100), vImmune);
    object vDR = Dummy("D_DR", 20.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageReduction(10, DAMAGE_POWER_PLUS_ONE), vDR);

    object a, i;
    // +1 longsword (max 9) against a greataxe (max 12).
    a = Actor("M_ENH"); Prop(Item(a, "nw_wswls001", "LS_P1"), ItemPropertyEnhancementBonus(1)); Item(a, "nw_waxgr001", "GAXE"); Melee(a);
    // Against a target immune to slashing: greataxe (slashing, 12) against a warhammer (bludgeoning, 8).
    a = Actor("M_IMMUNE"); Item(a, "nw_waxgr001", "GAXE"); Item(a, "nw_wblhw001", "WHAM"); Melee(a, vImmune);
    // Against damage reduction 10/+1: +1 longsword (9, passes) against a greataxe (12, reduced).
    a = Actor("M_DR"); Prop(Item(a, "nw_wswls001", "LS_P1"), ItemPropertyEnhancementBonus(1)); Item(a, "nw_waxgr001", "GAXE"); Melee(a, vDR);
    // Equal: two plain longswords; and the same with the other order of creation.
    a = Actor("M_TIE"); Item(a, "nw_wswls001", "LS_A"); Item(a, "nw_wswls001", "LS_B"); Melee(a);
    // Equal to the weapon in hand.
    a = Actor("M_TIEHAND"); i = Item(a, "nw_wswls001", "LS_HAND"); AssignCommand(a, ActionEquipItem(i, INVENTORY_SLOT_RIGHTHAND)); Item(a, "nw_wswls001", "LS_INV"); Melee(a);
    // Unarmed (1d3) against a dagger (1d4).
    a = Actor("M_UNARMED"); Item(a, "nw_wswdg001", "DAGGER"); Melee(a);
    // A monk: unarmed 1d6 against a dagger; against a greataxe.
    a = Actor("M_MONKDAG", "em_monk"); Item(a, "nw_wswdg001", "DAGGER"); Melee(a);
    a = Actor("M_MONKAXE", "em_monk"); Item(a, "nw_waxgr001", "GAXE"); Melee(a);
    a = Actor("M_MONKTIE", "em_monk"); Item(a, "nw_wswdg001", "DAG_A"); Item(a, "nw_wswdg001", "DAG_B"); Melee(a);
    // The off hand: a greataxe (too large) and a short sword, a longsword in the main hand.
    a = Actor("M_OFF"); i = Item(a, "nw_wswls001", "LS_HAND"); AssignCommand(a, ActionEquipItem(i, INVENTORY_SLOT_RIGHTHAND));
    Item(a, "nw_waxgr001", "GAXE"); Item(a, "nw_wswss001", "SSWORD"); Melee(a, OBJECT_INVALID, TRUE);
    // +5 slashing versus humans on a longsword (13 against a human) against a greataxe (12).
    a = Actor("M_RACE"); Prop(Item(a, "nw_wswls001", "LS_RACE"), ItemPropertyDamageBonusVsRace(IP_CONST_RACIALTYPE_HUMAN, IP_CONST_DAMAGETYPE_SLASHING, IP_CONST_DAMAGEBONUS_5));
    Item(a, "nw_waxgr001", "GAXE"); Melee(a, vHuman);
    a = Actor("M_RACENONE"); Prop(Item(a, "nw_wswls001", "LS_RACE"), ItemPropertyDamageBonusVsRace(IP_CONST_RACIALTYPE_HUMAN, IP_CONST_DAMAGETYPE_SLASHING, IP_CONST_DAMAGEBONUS_5));
    Item(a, "nw_waxgr001", "GAXE"); Melee(a);
    // 2d6 fire on a longsword (8 + 12) against a greataxe.
    a = Actor("M_DICE"); Prop(Item(a, "nw_wswls001", "LS_FIRE"), ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_FIRE, IP_CONST_DAMAGEBONUS_2d6)); Item(a, "nw_waxgr001", "GAXE"); Melee(a);
    // 1d4 fire on a longsword (8 + 4) against a greataxe (12): equal, the later one.
    a = Actor("M_DICETIE"); Prop(Item(a, "nw_wswls001", "LS_FIRE"), ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_FIRE, IP_CONST_DAMAGEBONUS_1d4)); Item(a, "nw_waxgr001", "GAXE"); Melee(a);
    // A greataxe in a bag, a longsword loose.
    a = Actor("M_BAG"); i = Item(a, "nw_it_contain001", "BAG"); CreateItemOnObject("nw_waxgr001", i, 1, "GAXE_BAG"); Item(a, "nw_wswls001", "LS"); Melee(a);
    // A longbow in hand, a dagger in the pack.
    a = Actor("M_FROMBOW"); i = Item(a, "nw_wbwln001", "BOW"); AssignCommand(a, ActionEquipItem(i, INVENTORY_SLOT_RIGHTHAND)); Item(a, "nw_wswdg001", "DAGGER"); Melee(a);
    // Only a bow and arrows.
    a = Actor("M_ONLYBOW"); Item(a, "nw_wbwln001", "BOW"); Item(a, "nw_wamar001", "ARROWS", 20); Melee(a);
    // A greataxe in hand, a +1 longsword in the pack.
    a = Actor("M_KEEP"); i = Item(a, "nw_waxgr001", "GAXE"); AssignCommand(a, ActionEquipItem(i, INVENTORY_SLOT_RIGHTHAND)); Prop(Item(a, "nw_wswls001", "LS_P1"), ItemPropertyEnhancementBonus(1)); Melee(a);
    // An unidentified greataxe against a longsword.
    a = Actor("M_UNID"); i = Item(a, "nw_waxgr001", "GAXE"); SetIdentified(i, FALSE); Item(a, "nw_wswls001", "LS"); Melee(a);
    // Strength 10 fighter against the immune dummy with only the greataxe: still equipped.
    a = Actor("M_IMMONLY"); Item(a, "nw_waxgr001", "GAXE"); Melee(a, vImmune);

    // Flat damage bonuses: +5 slashing, +5 fire, 2d6 slashing versus humans.
    a = Actor("M_FLAT5"); Prop(Item(a, "nw_wswls001", "LS_5"), ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_SLASHING, IP_CONST_DAMAGEBONUS_5)); Item(a, "nw_waxgr001", "GAXE"); Melee(a);
    a = Actor("M_FLAT5F"); Prop(Item(a, "nw_wswls001", "LS_5F"), ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_FIRE, IP_CONST_DAMAGEBONUS_5)); Item(a, "nw_waxgr001", "GAXE"); Melee(a);
    a = Actor("M_RACE2D6"); Prop(Item(a, "nw_wswls001", "LS_R2D6"), ItemPropertyDamageBonusVsRace(IP_CONST_RACIALTYPE_HUMAN, IP_CONST_DAMAGETYPE_SLASHING, IP_CONST_DAMAGEBONUS_2d6)); Item(a, "nw_waxgr001", "GAXE"); Melee(a, vHuman);
    // Partial immunity: 50 % (greataxe 6 against a warhammer 8) and 25 % (9 against 8).
    object v50 = Dummy("D_IMM50", 25.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageImmunityIncrease(DAMAGE_TYPE_SLASHING, 50), v50);
    object v25 = Dummy("D_IMM25", 30.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageImmunityIncrease(DAMAGE_TYPE_SLASHING, 25), v25);
    a = Actor("M_IMM50"); Item(a, "nw_waxgr001", "GAXE"); Item(a, "nw_wblhw001", "WHAM"); Melee(a, v50);
    a = Actor("M_IMM25"); Item(a, "nw_waxgr001", "GAXE"); Item(a, "nw_wblhw001", "WHAM"); Melee(a, v25);
    // Damage reduction 5/+5 (neither passes): +1 longsword 4 against a greataxe 7.
    object vDR5 = Dummy("D_DR5", 35.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageReduction(5, DAMAGE_POWER_PLUS_FIVE), vDR5);
    a = Actor("M_DR5"); Prop(Item(a, "nw_wswls001", "LS_P1"), ItemPropertyEnhancementBonus(1)); Item(a, "nw_waxgr001", "GAXE"); Melee(a, vDR5);
    // Damage resistance 10 slashing: greataxe 2 against a warhammer 8.
    object vRes = Dummy("D_RES", 40.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageResistance(DAMAGE_TYPE_SLASHING, 10), vRes);
    a = Actor("M_RES"); Item(a, "nw_waxgr001", "GAXE"); Item(a, "nw_wblhw001", "WHAM"); Melee(a, vRes);
    // Two slashing weapons against the immune target: both 0.
    a = Actor("M_IMMBOTH"); Item(a, "nw_waxgr001", "GAXE"); Item(a, "nw_wswls001", "LS"); Melee(a, vImmune);
    // The off hand with nothing in the main hand; with a dagger only.
    a = Actor("M_OFF2"); Item(a, "nw_wswss001", "SSWORD"); Item(a, "nw_wswdg001", "DAGGER"); Melee(a, OBJECT_INVALID, TRUE);
    a = Actor("M_OFF3"); i = Item(a, "nw_wswls001", "LS_HAND"); AssignCommand(a, ActionEquipItem(i, INVENTORY_SLOT_RIGHTHAND)); Item(a, "nw_wswdg001", "DAGGER"); Melee(a, OBJECT_INVALID, TRUE);
    // Dice bonuses at their maximum or not: 1d4 slashing (8 + 4 = 12, equal) and 1d6 (14, or 11.5 on average); +5 bludgeoning.
    a = Actor("M_D1D4"); Prop(Item(a, "nw_wswls001", "LS_1D4"), ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_SLASHING, IP_CONST_DAMAGEBONUS_1d4)); Item(a, "nw_waxgr001", "GAXE"); Melee(a);
    a = Actor("M_D1D6"); Prop(Item(a, "nw_wswls001", "LS_1D6"), ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_SLASHING, IP_CONST_DAMAGEBONUS_1d6)); Item(a, "nw_waxgr001", "GAXE"); Melee(a);
    a = Actor("M_FLAT5B"); Prop(Item(a, "nw_wswls001", "LS_5B"), ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_BLUDGEONING, IP_CONST_DAMAGEBONUS_5)); Item(a, "nw_waxgr001", "GAXE"); Melee(a);
    // The off hand with a dagger in the main hand: a short sword (6) beats it.
    a = Actor("M_OFF4"); i = Item(a, "nw_wswdg001", "DAG_HAND"); AssignCommand(a, ActionEquipItem(i, INVENTORY_SLOT_RIGHTHAND)); Item(a, "nw_waxgr001", "GAXE"); Item(a, "nw_wswss001", "SSWORD"); Melee(a, OBJECT_INVALID, TRUE);
    // Strength 10 against the target immune to bludgeoning: unarmed scores 0, a dagger 4.
    object vBlud = Dummy("D_IMMB", 45.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageImmunityIncrease(DAMAGE_TYPE_BLUDGEONING, 100), vBlud);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageImmunityIncrease(DAMAGE_TYPE_SLASHING, 100), vBlud);
    a = Actor("M_IMMBLUD"); Item(a, "nw_waxgr001", "GAXE"); Melee(a, vBlud);
    // Bolts: +5 piercing made first (so later in the pack) against plain ones.
    a = Actor("R_AMMOFLAT"); Item(a, "nw_wbwxl001", "XL"); Prop(Item(a, "nw_wambo001", "BOLTS_5", 20), ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_PIERCING, IP_CONST_DAMAGEBONUS_5));
    Item(a, "nw_wambo001", "BOLTS", 20); Ranged(a);
    // +3 attack light crossbow against a heavy crossbow, bolts.
    a = Actor("R_AB"); Prop(Item(a, "nw_wbwxl001", "XL_P3"), ItemPropertyAttackBonus(3)); Item(a, "nw_wbwxh001", "XH"); Item(a, "nw_wambo001", "BOLTS", 20); Ranged(a);
    // The same without bolts, a longsword in the pack.
    a = Actor("R_NOAMMO"); Prop(Item(a, "nw_wbwxl001", "XL_P3"), ItemPropertyAttackBonus(3)); Item(a, "nw_wbwxh001", "XH"); Item(a, "nw_wswls001", "LS"); Ranged(a);
    // Two stacks of bolts: plain and 1d6 fire.
    a = Actor("R_AMMO"); Item(a, "nw_wbwxl001", "XL"); Item(a, "nw_wambo001", "BOLTS", 20);
    Prop(Item(a, "nw_wambo001", "BOLTS_FIRE", 20), ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_FIRE, IP_CONST_DAMAGEBONUS_1d6)); Ranged(a);
    a = Actor("R_AMMO2"); Item(a, "nw_wbwxl001", "XL"); Prop(Item(a, "nw_wambo001", "BOLTS_FIRE", 20), ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_FIRE, IP_CONST_DAMAGEBONUS_1d6));
    Item(a, "nw_wambo001", "BOLTS", 20); Ranged(a);
    // Equal attack: a light crossbow made before a heavy one, and the other way round.
    a = Actor("R_TIE_LH"); Item(a, "nw_wbwxl001", "XL"); Item(a, "nw_wbwxh001", "XH"); Item(a, "nw_wambo001", "BOLTS", 20); Ranged(a);
    a = Actor("R_TIE_HL"); Item(a, "nw_wbwxh001", "XH"); Item(a, "nw_wbwxl001", "XL"); Item(a, "nw_wambo001", "BOLTS", 20); Ranged(a);
    // No ranged weapon at all: a longsword.
    a = Actor("R_MELEE"); Item(a, "nw_wswls001", "LS"); Ranged(a);
    // A sling in hand and a +3 longbow with arrows in the pack.
    a = Actor("R_HAND"); i = Item(a, "nw_wbwsl001", "SLING"); Item(a, "nw_wambu001", "BULLETS", 20); AssignCommand(a, ActionEquipItem(i, INVENTORY_SLOT_RIGHTHAND));
    Prop(Item(a, "nw_wbwln001", "BOW_P3"), ItemPropertyAttackBonus(3)); Item(a, "nw_wamar001", "ARROWS", 20); Ranged(a);
}

void Go()
{
    int i, n = GetLocalInt(GetModule(), "NACT");
    for (i = 0; i < n; i++)
    {
        object a = GetObjectByTag("A_" + GetLocalString(GetModule(), "CASE" + IntToString(i)));
        string s = "";
        object it = GetFirstItemInInventory(a);
        while (GetIsObjectValid(it)) { s += GetTag(it) + " "; it = GetNextItemInInventory(a); }
        SetLocalString(a, "INV", s);
        PLog(GetLocalString(GetModule(), "CASE" + IntToString(i)) + ".before", T(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, a)));
        if (GetLocalInt(a, "KIND") == 1) AssignCommand(a, ActionEquipMostDamagingMelee(GetLocalObject(a, "VS"), GetLocalInt(a, "OFF")));
        else AssignCommand(a, ActionEquipMostDamagingRanged(GetLocalObject(a, "VS")));
    }
}

void Read()
{
    int i, n = GetLocalInt(GetModule(), "NACT");
    for (i = 0; i < n; i++)
    {
        string k = GetLocalString(GetModule(), "CASE" + IntToString(i));
        object a = GetObjectByTag("A_" + k);
        PLog(k + ".order", GetLocalString(a, "INV"));
        PLog(k + ".right", T(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, a)));
        PLog(k + ".left", T(GetItemInSlot(INVENTORY_SLOT_LEFTHAND, a)));
        object am = GetItemInSlot(INVENTORY_SLOT_BOLTS, a);
        if (!GetIsObjectValid(am)) am = GetItemInSlot(INVENTORY_SLOT_ARROWS, a);
        if (!GetIsObjectValid(am)) am = GetItemInSlot(INVENTORY_SLOT_BULLETS, a);
        PLog(k + ".ammo", T(am));
    }
}

void main()
{
    DelayCommand(1.0, Setup());
    DelayCommand(4.0, Go());
    DelayCommand(10.0, Read());
    DelayCommand(11.0, PDone());
}
