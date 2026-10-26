// NWNX_Events creature events: stealth, detect and combat modes, feats (use, uses, has feat), skills, polymorph,
// associates, faction reputation, experience, levels, jumps, locks, event scripts, the calendar and combat.
#include "pnwnxev_inc"
#include "parity_time"

object F() { return GetObjectByTag("FTR"); }
object G() { return GetObjectByTag("GOB"); }
object H() { return GetObjectByTag("GOB2"); }

void Watch()
{
    EvWatch("NWNX_ON_STEALTH_ENTER", "");
    EvWatch("NWNX_ON_STEALTH_EXIT", "");
    EvWatch("NWNX_ON_DETECT_ENTER", "");
    EvWatch("NWNX_ON_DETECT_EXIT", "");
    NWNX_Events_SubscribeEvent("NWNX_ON_COMBAT_MODE_ON", "pev_log");
    NWNX_Events_SubscribeEvent("NWNX_ON_COMBAT_MODE_OFF", "pev_log");
    EvTags("NWNX_ON_COMBAT_MODE_ON", "COMBAT_MODE_ID");
    EvTags("NWNX_ON_COMBAT_MODE_OFF", "COMBAT_MODE_ID");
    EvWatch("NWNX_ON_USE_FEAT", "FEAT_ID,SUBFEAT_ID,o:TARGET_OBJECT_ID,o:AREA_OBJECT_ID,x:TARGET_POSITION_X,ACTION_RESULT");
    EvWatch("NWNX_ON_DECREMENT_REMAINING_FEAT_USES", "FEAT_ID,REMAINING_USES");
    EvWatch("NWNX_ON_USE_SKILL", "o:USED_ITEM_OBJECT_ID,o:TARGET_OBJECT_ID,SKILL_ID,SUB_SKILL_ID,x:TARGET_POSITION_X,x:ACTION_RESULT");
    EvWatch("NWNX_ON_POLYMORPH", "POLYMORPH_TYPE");
    EvWatch("NWNX_ON_UNPOLYMORPH", "x:POLYMORPH_TYPE");
    EvWatch("NWNX_ON_ADD_ASSOCIATE", "o:ASSOCIATE_OBJECT_ID");
    EvWatch("NWNX_ON_REMOVE_ASSOCIATE", "o:ASSOCIATE_OBJECT_ID");
    EvWatch("NWNX_ON_SET_NPC_FACTION_REPUTATION", "FACTION_ID,SUBJECT_FACTION_ID,PREVIOUS_REPUTATION,NEW_REPUTATION");
    EvWatch("NWNX_ON_SET_EXPERIENCE", "XP");
    EvWatch("NWNX_ON_LEVEL_UP", "");
    EvWatch("NWNX_ON_LEVEL_UP_AUTOMATIC", "");
    EvWatch("NWNX_ON_LEVEL_DOWN", "");
    EvWatch("NWNX_ON_CREATURE_JUMP_TO_POINT", "o:TARGET_AREA,x:POS_X");
    EvWatch("NWNX_ON_CREATURE_JUMP_TO_OBJECT", "o:OBJECT");
    EvWatch("NWNX_ON_OBJECT_LOCK", "o:DOOR,ACTION_RESULT");
    EvWatch("NWNX_ON_OBJECT_UNLOCK", "o:DOOR,o:THIEVES_TOOL,ACTIVE_PROPERTY_INDEX,ACTION_RESULT");
    EvWatch("NWNX_ON_RUN_EVENT_SCRIPT", "EVENT_TYPE,EVENT_SCRIPT");
    NWNX_Events_SubscribeEvent("NWNX_ON_CALENDAR_HOUR", "pev_log");
    EvTags("NWNX_ON_CALENDAR_HOUR", "x:OLD,x:NEW");
    NWNX_Events_SubscribeEvent("NWNX_ON_CALENDAR_DAY", "pev_log");
    EvWatch("NWNX_ON_START_COMBAT_ROUND", "o:TARGET_OBJECT_ID");
    EvWatch("NWNX_ON_ATTACK_TARGET_CHANGE", "o:OLD_TARGET_OBJECT_ID,o:NEW_TARGET_OBJECT_ID,AUTOMATIC_CHANGE,RETARGETABLE");
    EvWatch("NWNX_ON_COMBAT_ENTER", "");
    EvWatch("NWNX_ON_COMBAT_EXIT", "");
    EvWatch("NWNX_ON_DISARM", "o:DISARMER_OBJECT_ID,FEAT_ID,x:ACTION_RESULT");
    EvWatch("NWNX_ON_COMBAT_ATTACK_OF_OPPORTUNITY", "o:TARGET_OBJECT_ID");
    EvWatch("NWNX_ON_AREA_PLAY_BATTLE_MUSIC", "PLAY");
    EvWatch("NWNX_ON_COMBAT_DR_BROKEN", "TYPE");
}

void Step(string sName)
{
    object oMod = GetModule();
    EvFlush(GetLocalString(oMod, "STEP"));
    SetLocalString(oMod, "STEP", sName);
    PStart();
}

void Modes()
{
    Step("modes");
    EvLog("stealth");
    SetActionMode(F(), ACTION_MODE_STEALTH, TRUE);
    EvLog("detect");
    SetActionMode(F(), ACTION_MODE_DETECT, TRUE);
    EvLog("parry");
    SetActionMode(F(), ACTION_MODE_PARRY, TRUE);
    EvLog("power");
    SetActionMode(F(), ACTION_MODE_POWER_ATTACK, TRUE);
    EvLog("off");
    SetActionMode(F(), ACTION_MODE_POWER_ATTACK, FALSE);
    SetActionMode(F(), ACTION_MODE_STEALTH, FALSE);
    SetActionMode(F(), ACTION_MODE_DETECT, FALSE);
    EvLog("end");
}

void Feats()
{
    Step("feats");
    PLogI("feats.rage.uses", GetHasFeat(FEAT_BARBARIAN_RAGE, F()));
    AssignCommand(F(), ActionUseFeat(FEAT_BARBARIAN_RAGE, F()));
}

void HasFeat()
{
    Step("hasfeat");
    // Watched only around these calls: the engine checks feats at other times too.
    EvWatch("NWNX_ON_HAS_FEAT", "FEAT_ID,HAS_FEAT");
    NWNX_Events_AddIDToWhitelist("NWNX_ON_HAS_FEAT", FEAT_POWER_ATTACK);
    PLogI("has.power", GetHasFeat(FEAT_POWER_ATTACK, F()));
    PLogI("has.disarm", GetHasFeat(FEAT_DISARM, F()));
    PLogI("has.powergob", GetHasFeat(FEAT_POWER_ATTACK, G()));
    EvSkip("NWNX_ON_HAS_FEAT_BEFORE");
    EvResult("NWNX_ON_HAS_FEAT_BEFORE", "0");
    PLogI("has.power.skip0", GetHasFeat(FEAT_POWER_ATTACK, F()));
    EvResult("NWNX_ON_HAS_FEAT_BEFORE", "1");
    PLogI("has.power.skip1gob", GetHasFeat(FEAT_POWER_ATTACK, G()));
    EvSkip("NWNX_ON_HAS_FEAT_BEFORE", FALSE);
    EvResult("NWNX_ON_HAS_FEAT_BEFORE", "");
    NWNX_Events_RemoveIDFromWhitelist("NWNX_ON_HAS_FEAT", FEAT_POWER_ATTACK);
    NWNX_Events_UnsubscribeEvent("NWNX_ON_HAS_FEAT_BEFORE", "pev_log");
    NWNX_Events_UnsubscribeEvent("NWNX_ON_HAS_FEAT_AFTER", "pev_log");
}

void Skill()
{
    Step("skill");
    AssignCommand(F(), ActionUseSkill(SKILL_TAUNT, G()));
}

void Poly()
{
    Step("poly");
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectPolymorph(POLYMORPH_TYPE_WEREWOLF), H(), 30.0);
    EvLog("applied");
}

void Unpoly()
{
    Step("unpoly");
    effect e = GetFirstEffect(H());
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e) == EFFECT_TYPE_POLYMORPH) RemoveEffect(H(), e);
        e = GetNextEffect(H());
    }
    EvLog("removed");
}

void Assoc()
{
    Step("assoc");
    AddHenchman(F(), H());
    EvLog("added");
    RemoveHenchman(F(), H());
    EvLog("removed");
}

void Faction()
{
    Step("faction");
    AdjustReputation(G(), H(), -10);
    EvLog("adjusted");
    SetStandardFactionReputation(STANDARD_FACTION_COMMONER, 20, G());
    EvLog("standard");
}

void Xp()
{
    Step("xp");
    SetXP(F(), 20000);
    EvLog("set");
    GiveXPToCreature(F(), 100);
    EvLog("given");
    PLogI("xp.level", GetHitDice(F()));
    LevelUpHenchman(F(), CLASS_TYPE_BARBARIAN, TRUE);
    EvLog("levelled");
    PLogI("xp.level2", GetHitDice(F()));
    EvSkip("NWNX_ON_SET_EXPERIENCE_BEFORE");
    EvResult("NWNX_ON_SET_EXPERIENCE_BEFORE", "30000");
    SetXP(F(), 25000);
    EvSkip("NWNX_ON_SET_EXPERIENCE_BEFORE", FALSE);
    EvResult("NWNX_ON_SET_EXPERIENCE_BEFORE", "");
    PLogI("xp.skipped", GetXP(F()));
}

void Jumps()
{
    Step("jumps");
    AssignCommand(H(), JumpToLocation(PCentre(7.0, 8.0)));
    EvLog("queued");
}

void Jumps2()
{
    Step("jumps2");
    AssignCommand(H(), JumpToObject(G()));
    EvLog("queued");
}

void Locks()
{
    Step("locks");
    object oChest = GetObjectByTag("CHEST");
    SetLockLockable(oChest, TRUE);
    SetLockKeyTag(oChest, "PEVKEY");
    CreateItemOnObject("nw_it_gem001", F(), 1, "PEVKEY");
    SetLocked(oChest, FALSE);
    AssignCommand(F(), ActionLockObject(oChest));
}

void Unlocks()
{
    Step("unlocks");
    PLogI("locked", GetLocked(GetObjectByTag("CHEST")));
    SetLocked(GetObjectByTag("CHEST"), TRUE);
    AssignCommand(F(), ActionUnlockObject(GetObjectByTag("CHEST")));
}

void Walk()
{
    Step("walk");
    EvWatch("NWNX_ON_CREATURE_TILE_CHANGE", "OLD_TILE_INDEX,OLD_TILE_X,OLD_TILE_Y,NEW_TILE_INDEX,NEW_TILE_X,NEW_TILE_Y");
    EvWatch("NWNX_ON_MATERIALCHANGE", "MATERIAL_TYPE");
    PLogI("unlocked", GetLocked(GetObjectByTag("CHEST")));
    AssignCommand(H(), ActionMoveToLocation(PCentre(-5.0, 8.5)));
}

void Scripts()
{
    Step("scripts");
    NWNX_Events_AddIDToWhitelist("NWNX_ON_RUN_EVENT_SCRIPT", EVENT_SCRIPT_CREATURE_ON_USER_DEFINED_EVENT);
    SetEventScript(H(), EVENT_SCRIPT_CREATURE_ON_USER_DEFINED_EVENT, "pev_ud");
    SignalEvent(H(), EventUserDefined(7));
    EvLog("signalled");
    DelayCommand(0.5, EvSkip("NWNX_ON_RUN_EVENT_SCRIPT_BEFORE"));
    DelayCommand(0.6, SignalEvent(H(), EventUserDefined(8)));
    DelayCommand(1.2, EvSkip("NWNX_ON_RUN_EVENT_SCRIPT_BEFORE", FALSE));
}

void Calendar()
{
    Step("calendar");
    SetTime(GetTimeHour() + 1, 0, 0, 0);
    EvLog("set");
}

void main()
{
    object oF = CreateObject(OBJECT_TYPE_CREATURE, "pev_ftr", PCentre(5.0, 5.0), FALSE, "FTR");
    object oG = CreateObject(OBJECT_TYPE_CREATURE, "pev_gob", PCentre(6.5, 5.0), FALSE, "GOB");
    object oH = CreateObject(OBJECT_TYPE_CREATURE, "pev_gob", PCentre(5.0, 8.5), FALSE, "GOB2");
    CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(3.5, 5.0), FALSE, "CHEST");
    SetAILevel(oF, AI_LEVEL_NORMAL);
    SetAILevel(oG, AI_LEVEL_NORMAL);
    SetAILevel(oH, AI_LEVEL_NORMAL);
    SetImmortal(oG, TRUE);
    SetImmortal(oF, TRUE);
    DelayCommand(0.5, Watch());
    DelayCommand(1.0, Modes());
    DelayCommand(3.0, Feats());
    DelayCommand(7.0, HasFeat());
    DelayCommand(8.0, Skill());
    DelayCommand(13.0, Poly());
    DelayCommand(16.0, Unpoly());
    DelayCommand(19.0, Assoc());
    DelayCommand(20.0, Faction());
    DelayCommand(21.0, Xp());
    DelayCommand(22.0, Jumps());
    DelayCommand(23.0, Jumps2());
    DelayCommand(24.0, Locks());
    DelayCommand(31.0, Unlocks());
    DelayCommand(38.0, Walk());
    DelayCommand(45.0, Scripts());
    DelayCommand(48.0, Calendar());
    DelayCommand(49.0, Step("end"));
    DelayCommand(49.5, PDone());
}
