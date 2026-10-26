// NWNX_Risenholm functions that work without a player: effects by tag, animation swaps, phenotypes and displayed
// heads, limits, shelters, afterimages, level history and the concealment registry.
#include "parity_inc"
#include "nwnx_risenholm"

void Effects(object oGob)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectACIncrease(1), "TAG_A"), oGob);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectACIncrease(2), "TAG_A"), oGob);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectAttackIncrease(1), "TAG_B"), oGob);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectRunScript("", "", "", 0.0, "the_data"), "TAG_C"), oGob);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectRunScript("rh_applied", "rh_removed", "rh_interval", 6.0, "more"), oGob);
    effect eLink = EffectLinkEffects(TagEffect(EffectSkillIncrease(SKILL_HIDE, 2), "TAG_L"), EffectSavingThrowIncrease(SAVING_THROW_ALL, 1));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(eLink, "TAG_L"), oGob);
    PLogI("tag.a", NWNX_Risenholm_GetHasEffectByTag(oGob, "TAG_A"));
    PLogI("tag.case", NWNX_Risenholm_GetHasEffectByTag(oGob, "tag_a"));
    PLogI("tag.none", NWNX_Risenholm_GetHasEffectByTag(oGob, "TAG_X"));
    PLogI("tag.empty", NWNX_Risenholm_GetHasEffectByTag(oGob, ""));
    PLogI("tag.invalid", NWNX_Risenholm_GetHasEffectByTag(OBJECT_INVALID, "TAG_A"));
    int i;
    for (i = -1; i <= 6; i++)
    {
        PLogI("param.data." + IntToString(i), NWNX_Risenholm_GetHasEffectWithStringParam(oGob, i, "the_data"));
        PLogI("param.applied." + IntToString(i), NWNX_Risenholm_GetHasEffectWithStringParam(oGob, i, "rh_applied"));
        PLogI("param.removed." + IntToString(i), NWNX_Risenholm_GetHasEffectWithStringParam(oGob, i, "rh_removed"));
        PLogI("param.interval." + IntToString(i), NWNX_Risenholm_GetHasEffectWithStringParam(oGob, i, "rh_interval"));
        PLogI("param.more." + IntToString(i), NWNX_Risenholm_GetHasEffectWithStringParam(oGob, i, "more"));
        PLogI("param.empty." + IntToString(i), NWNX_Risenholm_GetHasEffectWithStringParam(oGob, i, ""));
    }
    PLogI("remove.a", NWNX_Risenholm_RemoveEffectsByTag(oGob, "TAG_A"));
    PLogI("remove.a.again", NWNX_Risenholm_RemoveEffectsByTag(oGob, "TAG_A"));
    PLogI("remove.l", NWNX_Risenholm_RemoveEffectsByTag(oGob, "TAG_L"));
    PLogI("remove.ac", GetAC(oGob));
}

void Later(object oGob, object oArea, object oShelter, object oBarrel)
{
    PLogI("shelter.after.create", GetLocalInt(oArea, "SHELTER_LIVE_DIRTY"));
    DeleteLocalInt(oArea, "SHELTER_LIVE_DIRTY");
    DestroyObject(oBarrel);
    DestroyObject(oShelter, 0.5);
}

void Later2(object oArea)
{
    PLogI("shelter.after.destroy", GetLocalInt(oArea, "SHELTER_LIVE_DIRTY"));
    PDone();
}

void main()
{
    object oArea = GetObjectByTag("PARITY_AREA");
    object oGob = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre());
    int e;
    for (e = 5000; e <= 5012; e++) SetEventScript(oGob, e, "");
    Effects(oGob);

    PLogI("dm.manifested", NWNX_Risenholm_GetIsDMManifested(oGob));
    PLogI("dm.manifested.invalid", NWNX_Risenholm_GetIsDMManifested(OBJECT_INVALID));

    ReplaceObjectAnimation(oGob, "walk", "run");
    ReplaceObjectAnimation(oGob, "pause1", "a_name_longer_than_sixteen");
    ReplaceObjectAnimation(oGob, "a_long_animation_name_x", "kneel");
    PLog("anim.walk", NWNX_Risenholm_GetAnimationReplace(oGob, "walk"));
    PLog("anim.WALK", NWNX_Risenholm_GetAnimationReplace(oGob, "WALK"));
    PLog("anim.long.new", NWNX_Risenholm_GetAnimationReplace(oGob, "pause1"));
    PLog("anim.long.old", NWNX_Risenholm_GetAnimationReplace(oGob, "a_long_animation_name_x"));
    PLog("anim.long.cut", NWNX_Risenholm_GetAnimationReplace(oGob, "a_long_animation"));
    PLog("anim.none", NWNX_Risenholm_GetAnimationReplace(oGob, "run"));
    ReplaceObjectAnimation(oGob, "walk", "");
    PLog("anim.cleared", NWNX_Risenholm_GetAnimationReplace(oGob, "walk"));

    object oHuman = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(3.0));
    for (e = 5000; e <= 5012; e++) SetEventScript(oHuman, e, "");
    PLogI("pheno.start", GetPhenoType(oHuman));
    PLogI("pheno.set150", NWNX_Risenholm_SetPhenoType(oHuman, 150));
    PLogI("pheno.get150", GetPhenoType(oHuman));
    PLogI("pheno.set255", NWNX_Risenholm_SetPhenoType(oHuman, 255));
    PLogI("pheno.set256", NWNX_Risenholm_SetPhenoType(oHuman, 256));
    PLogI("pheno.setneg", NWNX_Risenholm_SetPhenoType(oHuman, -1));
    PLogI("pheno.get", GetPhenoType(oHuman));
    PLogI("pheno.gob", NWNX_Risenholm_SetPhenoType(oGob, 3));
    PLogI("pheno.gob.get", GetPhenoType(oGob));
    PLogI("pheno.invalid", NWNX_Risenholm_SetPhenoType(OBJECT_INVALID, 3));
    NWNX_Risenholm_SetPhenoType(oHuman, 0);

    int nHead = GetCreatureBodyPart(CREATURE_PART_HEAD, oHuman);
    PLogI("head.real", nHead);
    PLogI("head.displayed.start", NWNX_Risenholm_GetDisplayedHead(oHuman) == nHead);
    PLogI("head.set", NWNX_Risenholm_SetDisplayedHead(oHuman, 1044));
    PLogI("head.displayed", NWNX_Risenholm_GetDisplayedHead(oHuman));
    PLogI("head.part", GetCreatureBodyPart(CREATURE_PART_HEAD, oHuman) == nHead);
    PLogI("head.set0", NWNX_Risenholm_SetDisplayedHead(oHuman, 0));
    PLogI("head.displayed0", NWNX_Risenholm_GetDisplayedHead(oHuman) == nHead);
    NWNX_Risenholm_SetDisplayedHead(oHuman, 1044);
    SetCreatureBodyPart(CREATURE_PART_HEAD, 3, oHuman);
    PLogI("head.afterpart", NWNX_Risenholm_GetDisplayedHead(oHuman));
    PLogI("head.big", NWNX_Risenholm_SetDisplayedHead(oHuman, 70000));
    PLogI("head.big.get", NWNX_Risenholm_GetDisplayedHead(oHuman));
    PLogI("head.neg", NWNX_Risenholm_SetDisplayedHead(oHuman, -2));
    PLogI("head.neg.get", NWNX_Risenholm_GetDisplayedHead(oHuman));
    PLogI("head.gob", NWNX_Risenholm_SetDisplayedHead(oGob, 5));
    PLogI("head.gob.get", NWNX_Risenholm_GetDisplayedHead(oGob));
    PLogI("head.invalid", NWNX_Risenholm_SetDisplayedHead(OBJECT_INVALID, 5));
    PLogI("head.invalid.get", NWNX_Risenholm_GetDisplayedHead(OBJECT_INVALID));
    object oChest = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(-3.0));
    PLogI("head.plc.get", NWNX_Risenholm_GetDisplayedHead(oChest));

    PLogI("msglimit.first", NWNX_Risenholm_SetObjectUpdateMessageLimit(1000));
    PLogI("msglimit.zero", NWNX_Risenholm_SetObjectUpdateMessageLimit(0));
    PLogI("msglimit.neg", NWNX_Risenholm_SetObjectUpdateMessageLimit(-5));
    PLogI("msglimit.second", NWNX_Risenholm_SetObjectUpdateMessageLimit(400));

    // ExecuteCommand is not measured: a command that cannot be run leaves NWNX's server waiting forever.
    PLogI("shutdown", NWNX_Risenholm_CheckForShutdownFile());
    PLogI("trim", NWNX_Risenholm_TrimAILists());

    NWNX_Risenholm_SetDamageBonusLimit(300);
    PLogI("dmglimit.300", GetDamageBonusLimit());
    NWNX_Risenholm_SetDamageBonusLimit(-4);
    PLogI("dmglimit.neg", GetDamageBonusLimit());
    NWNX_Risenholm_SetDamageBonusLimit(100);
    PLogI("dmglimit.100", GetDamageBonusLimit());

    NWNX_Risenholm_SetScriptedCleave(TRUE);
    PLogI("cleave.idle", NWNX_Risenholm_DoCleaveAttack(oGob, oHuman));
    NWNX_Risenholm_SetScriptedCleave(FALSE);

    // Skill points are left out: for creatures without a stored history NWNX reports an uninitialised value.
    json jHistory = NWNX_Risenholm_GetLevelHistory(oGob);
    json jLevel = JsonObjectDel(JsonArrayGet(JsonObjectGet(jHistory, "levels"), 0), "skillpoints");
    PLog("history.gob", JsonDump(JsonObjectSet(jHistory, "levels", JsonArraySet(JsonObjectGet(jHistory, "levels"), 0, jLevel))));
    PLog("history.invalid", JsonDump(NWNX_Risenholm_GetLevelHistory(OBJECT_INVALID)));
    PLogI("history.restore", NWNX_Risenholm_RestoreLevelHistory(oGob, NWNX_Risenholm_GetLevelHistory(oGob), 0));
    PLogI("history.restore.bad", NWNX_Risenholm_RestoreLevelHistory(oGob, JsonObject(), 0));
    PLogI("history.restore.invalid", NWNX_Risenholm_RestoreLevelHistory(OBJECT_INVALID, JsonObject(), 0));

    PLog("rpc.request", NWNX_Risenholm_GetRpcRequest());
    NWNX_Risenholm_SetRpcResponse("{}");
    PLogI("inv.owner", GetIsObjectValid(NWNX_Risenholm_GetOtherInventoryOwner(oGob)));
    object oItem = CreateItemOnObject("nw_it_mpotion001", oHuman);
    NWNX_Risenholm_ConcealItemFromViewer(oGob, oHuman, oItem);
    PLogI("conceal.npc", NWNX_Risenholm_GetIsItemConcealedFrom(oGob, oItem));
    NWNX_Risenholm_ClearConcealedItems(oGob);
    PLogI("conceal.cleared", NWNX_Risenholm_GetIsItemConcealedFrom(oGob, oItem));
    PLogI("resoverride.npc", NWNX_Risenholm_SetResourceOverride(oGob, 2002, "a", "b"));

    object oWeapon = GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, oHuman);
    PLogI("apply.worn", NWNX_Risenholm_ApplyItemProperties(oHuman, oWeapon));
    PLogI("apply.pack", NWNX_Risenholm_ApplyItemProperties(oHuman, oItem));
    PLogI("apply.other", NWNX_Risenholm_ApplyItemProperties(oGob, oWeapon));
    SetLocalInt(oWeapon, "LOADOUT_DORMANT", 1);
    PLogI("apply.dormant", NWNX_Risenholm_ApplyItemProperties(oHuman, oWeapon));
    DeleteLocalInt(oWeapon, "LOADOUT_DORMANT");

    // An afterimage of the bandit.
    SetLocalInt(oHuman, "own_var", 5);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectHaste(), oHuman);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(3), oHuman);
    PLogI("after.prepare", NWNX_Risenholm_PrepareAfterimage(oHuman) > 0);
    PLogI("after.prepare.invalid", NWNX_Risenholm_PrepareAfterimage(OBJECT_INVALID));
    object oImage = NWNX_Risenholm_CreateAfterimage(oHuman, Location(oArea, Vector(14.0, 15.0, 0.0), 90.0), STANDARD_FACTION_COMMONER + 1, 2.0);
    NWNX_Risenholm_ReleaseAfterimage();
    PLogI("after.valid", GetIsObjectValid(oImage));
    PLogI("after.new", oImage != oHuman);
    PLog("after.tag", GetTag(oImage));
    PLog("after.name", GetName(oImage));
    PLogI("after.plot", GetPlotFlag(oImage));
    PLogI("after.pc", GetIsPC(oImage));
    PLogI("after.lootable", GetLootable(oImage));
    PLogI("after.useable", GetUseableFlag(oImage));
    PLogI("after.hp", GetCurrentHitPoints(oImage) == GetMaxHitPoints(oImage));
    PLogI("after.ownvar", GetLocalInt(oImage, "own_var"));
    PLogI("after.setpiece", GetLocalInt(oImage, "IS_SET_PIECE"));
    PLogI("after.vfx", GetLocalInt(oImage, "IS_VFX"));
    PLogI("after.faction.commoner", GetStandardFactionReputation(STANDARD_FACTION_COMMONER, oImage));
    PLogI("after.faction.hostile", GetStandardFactionReputation(STANDARD_FACTION_HOSTILE, oImage));
    PLogI("after.weapon", GetIsObjectValid(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, oImage)));
    PLogI("after.pack", GetIsObjectValid(GetFirstItemInInventory(oImage)));
    PLogI("after.pack.source", GetIsObjectValid(GetFirstItemInInventory(oHuman)));
    PLogF("after.x", GetPosition(oImage).x);
    PLogF("after.y", GetPosition(oImage).y);
    PLogF("after.facing", GetFacing(oImage));
    PLogF("after.speed", GetObjectVisualTransform(oImage, OBJECT_VISUAL_TRANSFORM_ANIMATION_SPEED));
    string sFx = "";
    effect x = GetFirstEffect(oImage);
    while (GetIsEffectValid(x))
    {
        sFx += IntToString(GetEffectType(x)) + "/" + IntToString(GetEffectDurationType(x)) + "/" + IntToString(GetEffectSubType(x)) + ";";
        x = GetNextEffect(oImage);
    }
    PLog("after.effects", sFx);
    object oNone = NWNX_Risenholm_CreateAfterimage(OBJECT_INVALID, Location(oArea, Vector(14.0, 15.0, 0.0), 90.0), 2);
    PLogI("after.invalid", GetIsObjectValid(oNone));

    NWNX_Risenholm_SetShelterAppearances("5, 7,abc");
    object oShelter = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(-6.0));
    PLogI("shelter.chest.appearance", GetAppearanceType(oShelter));
    PLogI("shelter.after.chest", GetLocalInt(oArea, "SHELTER_LIVE_DIRTY"));
    NWNX_Risenholm_SetShelterAppearances(IntToString(GetAppearanceType(oShelter)));
    PLogI("shelter.after.set", GetLocalInt(oArea, "SHELTER_LIVE_DIRTY"));
    object oShelter2 = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(-6.0, 3.0));
    PLogI("shelter.after.create.now", GetLocalInt(oArea, "SHELTER_LIVE_DIRTY"));
    object oBarrel = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_barrel", PCentre(-6.0, -3.0));
    DelayCommand(1.0, Later(oGob, oArea, oShelter2, oBarrel));
    DelayCommand(3.0, Later2(oArea));
}
