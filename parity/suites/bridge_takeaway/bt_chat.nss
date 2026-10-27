// Chat commands that take things away from the speaker, one per line: "bt COMMAND [ARG] [ARG]". Each change is
// reported in chat and the log as "BT|command|what the server holds now".
#include "nwnx_creature"
#include "nwnx_player"
#include "nwnx_player_qbs"
#include "nw_inc_nui"

string Word(string s, int n)
{
    int i;
    for (i = 0; i < n; i++)
    {
        int at = FindSubString(s, " ");
        if (at < 0) return "";
        s = GetSubString(s, at + 1, GetStringLength(s));
    }
    int end = FindSubString(s, " ");
    return end < 0 ? s : GetStringLeft(s, end);
}

void Report(object oPC, string sWhat)
{
    string s = "BT|" + sWhat + "|xp=" + IntToString(GetXP(oPC)) + " level=" + IntToString(GetHitDice(oPC))
        + " cleric=" + IntToString(GetLevelByClass(CLASS_TYPE_CLERIC, oPC)) + " sorcerer=" + IntToString(GetLevelByClass(CLASS_TYPE_SORCERER, oPC))
        + " str=" + IntToString(GetAbilityScore(oPC, ABILITY_STRENGTH, TRUE)) + "/" + IntToString(GetAbilityScore(oPC, ABILITY_STRENGTH))
        + " hp=" + IntToString(GetCurrentHitPoints(oPC)) + "/" + IntToString(GetMaxHitPoints(oPC)) + " ac=" + IntToString(GetAC(oPC))
        + " gold=" + IntToString(GetGold(oPC)) + " turn=" + IntToString(NWNX_Creature_GetFeatRemainingUses(oPC, FEAT_TURN_UNDEAD))
        + " conc=" + IntToString(GetSkillRank(SKILL_CONCENTRATION, oPC, TRUE)) + "/" + IntToString(GetSkillRank(SKILL_CONCENTRATION, oPC));
    SendMessageToPC(oPC, s);
    WriteTimestampedLogEntry(s);
}

void RemoveTagged(object oPC, string sTag)
{
    effect e = GetFirstEffect(oPC);
    while (GetIsEffectValid(e))
    {
        if (GetEffectTag(e) == sTag) RemoveEffect(oPC, e);
        e = GetNextEffect(oPC);
    }
}

void Lasting(object oPC, effect e, string sTag = "BT")
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(SupernaturalEffect(e), sTag), oPC);
}

void main()
{
    object oPC = GetPCChatSpeaker();
    string sLine = GetPCChatMessage();
    if (Word(sLine, 0) != "bt") return;
    string sCmd = Word(sLine, 1), a = Word(sLine, 2), b = Word(sLine, 3);
    int n = StringToInt(a);
    object oItem = GetItemPossessedBy(oPC, a);
    object oTalker = GetObjectByTag("BT_TALKER");
    // Experience, abilities, hit points, skills, armour class and attack.
    if (sCmd == "xp") SetXP(oPC, n);
    else if (sCmd == "str") NWNX_Creature_SetRawAbilityScore(oPC, ABILITY_STRENGTH, n);
    else if (sCmd == "dec") Lasting(oPC, EffectAbilityDecrease(n, StringToInt(b)));
    else if (sCmd == "hpmax") NWNX_Creature_SetMaxHitPointsByLevel(oPC, 1, n);
    else if (sCmd == "hurt") ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(n), oPC);
    else if (sCmd == "skilldec") Lasting(oPC, EffectSkillDecrease(SKILL_CONCENTRATION, 6));
    else if (sCmd == "skillrank") NWNX_Creature_SetSkillRank(oPC, SKILL_CONCENTRATION, n);
    else if (sCmd == "acdec") Lasting(oPC, EffectACDecrease(5));
    else if (sCmd == "atkdec") Lasting(oPC, EffectAttackDecrease(5));
    // Feats and spells.
    else if (sCmd == "featadd") NWNX_Creature_AddFeat(oPC, n);
    else if (sCmd == "featrm") NWNX_Creature_RemoveFeat(oPC, n);
    else if (sCmd == "turnuse") DecrementRemainingFeatUses(oPC, FEAT_TURN_UNDEAD);
    else if (sCmd == "knownrm") NWNX_Creature_RemoveKnownSpell(oPC, CLASS_TYPE_SORCERER, n, GetKnownSpellId(oPC, CLASS_TYPE_SORCERER, n, 0));
    else if (sCmd == "memset")
    {
        int i;
        for (i = 0; i < 6; i++) SetMemorizedSpell(oPC, CLASS_TYPE_CLERIC, 1, i, SPELL_BLESS);
    }
    else if (sCmd == "memclear") ClearMemorizedSpell(oPC, CLASS_TYPE_CLERIC, 1, n);
    else if (sCmd == "memspent") SetMemorizedSpellReady(oPC, CLASS_TYPE_CLERIC, 1, n, FALSE);
    else if (sCmd == "spelluse") DecrementRemainingSpellUses(oPC, GetKnownSpellId(oPC, CLASS_TYPE_SORCERER, n, 0));
    else if (sCmd == "sorcslots") NWNX_Creature_SetRemainingSpellSlots(oPC, CLASS_TYPE_SORCERER, n, StringToInt(b));
    else if (sCmd == "classlevel") NWNX_Creature_SetLevelByPosition(oPC, n, StringToInt(b));
    // Items and gold.
    else if (sCmd == "destroy") DestroyObject(oItem);
    else if (sCmd == "take")
    {
        CopyItem(oItem, GetObjectByTag("BT_CHEST"), TRUE);
        DestroyObject(oItem);
    }
    else if (sCmd == "destroyslot") DestroyObject(GetItemInSlot(n, oPC));
    else if (sCmd == "unequip")
    {
        AssignCommand(oPC, ClearAllActions());
        AssignCommand(oPC, ActionUnequipItem(GetItemInSlot(n, oPC)));
    }
    else if (sCmd == "stack") SetItemStackSize(oItem, StringToInt(b));
    else if (sCmd == "charges") SetItemCharges(oItem, StringToInt(b));
    else if (sCmd == "gold") AssignCommand(oPC, TakeGoldFromCreature(n, oPC, TRUE));
    else if (sCmd == "iprm")
    {
        itemproperty p = GetFirstItemProperty(oItem);
        while (GetIsItemPropertyValid(p))
        {
            RemoveItemProperty(oItem, p);
            p = GetNextItemProperty(oItem);
        }
    }
    else if (sCmd == "rename") SetName(oItem, b);
    else if (sCmd == "unname") SetName(oItem, "");
    else if (sCmd == "unid") SetIdentified(oItem, FALSE);
    else if (sCmd == "restyle")
    {
        object oNew = CopyItemAndModify(oItem, ITEM_APPR_TYPE_WEAPON_MODEL, ITEM_APPR_WEAPON_MODEL_TOP, 2, TRUE);
        if (GetIsObjectValid(oNew)) DestroyObject(oItem);
    }
    else if (sCmd == "fill")
    {
        // Longswords enough to fill the first backpack page, so what comes next lands on another.
        int i;
        for (i = 0; i < n; i++) CreateItemOnObject("nw_wswls001", oPC, 1, "BT_FILL");
    }
    else if (sCmd == "deep") CreateItemOnObject("nw_it_mring001", oPC, 1, "BT_DEEP");
    else if (sCmd == "bagdestroy") DestroyObject(GetItemPossessedBy(oPC, "BT_BAGPOTION"));
    // Effects, forms, looks and names.
    else if (sCmd == "haste") Lasting(oPC, EffectHaste());
    else if (sCmd == "glow") Lasting(oPC, EffectVisualEffect(VFX_DUR_GLOW_RED));
    else if (sCmd == "unfx") RemoveTagged(oPC, "BT");
    else if (sCmd == "superbear") Lasting(oPC, EffectPolymorph(POLYMORPH_TYPE_BROWN_BEAR), "BT_POLY");
    else if (sCmd == "bear") AssignCommand(oPC, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, TagEffect(EffectPolymorph(POLYMORPH_TYPE_BROWN_BEAR), "BT_POLY"), oPC, 600.0));
    else if (sCmd == "polyself") AssignCommand(oPC, ActionCastSpellAtObject(388, oPC, METAMAGIC_ANY, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    else if (sCmd == "dragon") Lasting(oPC, EffectPolymorph(POLYMORPH_TYPE_RED_DRAGON), "BT_POLY");
    else if (sCmd == "unpoly") RemoveTagged(oPC, "BT_POLY");
    else if (sCmd == "appear")
    {
        if (!GetLocalInt(oPC, "BT_APPEAR")) SetLocalInt(oPC, "BT_APPEAR", GetAppearanceType(oPC) + 1);
        SetCreatureAppearanceType(oPC, n);
    }
    else if (sCmd == "unappear") SetCreatureAppearanceType(oPC, GetLocalInt(oPC, "BT_APPEAR") - 1);
    else if (sCmd == "portrait")
    {
        if (!GetLocalInt(oPC, "BT_PORTRAIT")) SetLocalInt(oPC, "BT_PORTRAIT", GetPortraitId(oPC) + 1);
        SetPortraitId(oPC, n);
    }
    else if (sCmd == "unportrait") SetPortraitId(oPC, GetLocalInt(oPC, "BT_PORTRAIT") - 1);
    else if (sCmd == "name") SetName(oPC, a);
    else if (sCmd == "unnamepc") SetName(oPC, "");
    // Associates.
    else if (sCmd == "hench")
    {
        object oHench = CreateObject(OBJECT_TYPE_CREATURE, "nw_commale", GetLocation(oPC), FALSE, "BT_HENCH");
        AddHenchman(oPC, oHench);
    }
    else if (sCmd == "unhench") RemoveHenchman(oPC, GetObjectByTag("BT_HENCH"));
    else if (sCmd == "henchkill") DestroyObject(GetObjectByTag("BT_HENCH"));
    else if (sCmd == "summon") AssignCommand(oPC, ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY, EffectSummonCreature("nw_s_badgerdire"), GetLocation(oPC), 600.0));
    else if (sCmd == "unsummon") DestroyObject(GetAssociate(ASSOCIATE_TYPE_SUMMONED, oPC));
    // Windows, the journal, map pins, the quickbar, conversations, stores and containers.
    else if (sCmd == "nui")
    {
        json jCol = JsonArrayInsert(JsonArray(), NuiLabel(JsonString("Taken away soon"), JsonInt(NUI_HALIGN_CENTER), JsonInt(NUI_VALIGN_MIDDLE)));
        json jWin = NuiWindow(NuiCol(jCol), JsonString("Takeaway"), NuiRect(100.0, 100.0, 300.0, 150.0), JsonBool(FALSE), JsonBool(FALSE), JsonBool(TRUE), JsonBool(FALSE), JsonBool(TRUE));
        SetLocalInt(oPC, "BT_NUI", NuiCreate(oPC, jWin, "btwin"));
    }
    else if (sCmd == "nuikill") NuiDestroy(oPC, GetLocalInt(oPC, "BT_NUI"));
    else if (sCmd == "jadd") AddJournalQuestEntry("bt_quest", n, oPC, FALSE, FALSE, TRUE);
    else if (sCmd == "jdel") RemoveJournalQuestEntry("bt_quest", oPC, FALSE);
    else if (sCmd == "pin") SetMapPinEnabled(GetWaypointByTag("BT_PIN"), n);
    else if (sCmd == "qbfeat") NWNX_Player_SetQuickBarSlot(oPC, n, NWNX_Player_QBS_UseFeat(FEAT_TURN_UNDEAD));
    else if (sCmd == "qbclear") NWNX_Player_SetQuickBarSlot(oPC, n, NWNX_Player_QBS_Empty());
    else if (sCmd == "talk") AssignCommand(oTalker, ActionStartConversation(oPC, "bt_talk", FALSE, FALSE));
    else if (sCmd == "talkclear") AssignCommand(oTalker, ClearAllActions(TRUE));
    else if (sCmd == "talkkill") DestroyObject(oTalker);
    else if (sCmd == "talker") CreateObject(OBJECT_TYPE_CREATURE, "nw_commale", GetLocation(oPC), FALSE, "BT_TALKER");
    else if (sCmd == "store") OpenStore(GetObjectByTag("BT_STORE"), oPC);
    else if (sCmd == "storekill") DestroyObject(GetObjectByTag("BT_STORE"));
    else if (sCmd == "jump") AssignCommand(oPC, JumpToLocation(GetLocation(oPC)));
    else if (sCmd == "chest") AssignCommand(oPC, ActionInteractObject(GetObjectByTag("BT_CHEST")));
    else if (sCmd == "chestempty") DestroyObject(GetFirstItemInInventory(GetObjectByTag("BT_CHEST")));
    else if (sCmd == "useable") SetUseableFlag(GetObjectByTag("BT_BARREL"), n);
    else if (sCmd == "chestkill") DestroyObject(GetObjectByTag("BT_CHEST"));
    DelayCommand(1.0, Report(oPC, sLine));
}
