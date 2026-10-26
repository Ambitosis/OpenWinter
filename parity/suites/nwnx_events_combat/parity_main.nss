// NWNX_Events combat events: combat rounds, attack target changes, disarm, damage resistance running out, attacks of
// opportunity, ammunition stacks and reloading, battle music.
#include "pnwnxev_inc"
#include "parity_time"

object F() { return GetObjectByTag("FTR"); }
object G() { return GetObjectByTag("GOB"); }
object A() { return GetObjectByTag("ARCH"); }
object T() { return GetObjectByTag("TGT"); }

void Max(string sEvent, int n) { SetLocalInt(GetModule(), "EVMAX:" + sEvent, n); }

void Only(string sEvent, int n)
{
    // Only the fighter and the archer, and only the first few in each step: how often creatures swing, step and draw
    // attacks of opportunity is the combat rules' business, not this suite's.
    NWNX_Events_ToggleDispatchListMode(sEvent, "pev_log", TRUE);
    NWNX_Events_AddObjectToDispatchList(sEvent, "pev_log", F());
    if (GetStringLeft(sEvent, 32) != "NWNX_ON_BROADCAST_ATTACK_OF_OPPO") NWNX_Events_AddObjectToDispatchList(sEvent, "pev_log", A());
    Max(sEvent, n);
}

void Watch()
{
    EvWatch("NWNX_ON_START_COMBAT_ROUND", "o:TARGET_OBJECT_ID");
    Only("NWNX_ON_START_COMBAT_ROUND_BEFORE", 1);
    Only("NWNX_ON_START_COMBAT_ROUND_AFTER", 1);
    EvWatch("NWNX_ON_ATTACK_TARGET_CHANGE", "o:OLD_TARGET_OBJECT_ID,o:NEW_TARGET_OBJECT_ID,AUTOMATIC_CHANGE,RETARGETABLE");
    Only("NWNX_ON_ATTACK_TARGET_CHANGE_BEFORE", 1);
    Only("NWNX_ON_ATTACK_TARGET_CHANGE_AFTER", 1);
    EvWatch("NWNX_ON_BROADCAST_ATTACK_OF_OPPORTUNITY", "o:TARGET_OBJECT_ID,MOVEMENT");
    Only("NWNX_ON_BROADCAST_ATTACK_OF_OPPORTUNITY_BEFORE", 1);
    Only("NWNX_ON_BROADCAST_ATTACK_OF_OPPORTUNITY_AFTER", 1);
    EvWatch("NWNX_ON_COMBAT_ENTER", "");
    EvWatch("NWNX_ON_COMBAT_EXIT", "");
    EvWatch("NWNX_ON_DISARM", "o:DISARMER_OBJECT_ID,FEAT_ID,x:ACTION_RESULT");
    EvWatch("NWNX_ON_COMBAT_DR_BROKEN", "TYPE");
    EvWatch("NWNX_ON_AREA_PLAY_BATTLE_MUSIC", "PLAY");
    EvWatch("NWNX_ON_BROADCAST_SAFE_PROJECTILE", "o:TARGET_OBJECT_ID,PROJECTILE_TYPE,SPELL_ID,x:ATTACK_RESULT,PROJECTILE_PATH_TYPE");
}

void Step(string sName)
{
    object oMod = GetModule();
    EvFlush(GetLocalString(oMod, "STEP"));
    SetLocalString(oMod, "STEP", sName);
    DeleteLocalInt(oMod, "EVSEEN:NWNX_ON_START_COMBAT_ROUND_BEFORE");
    DeleteLocalInt(oMod, "EVSEEN:NWNX_ON_START_COMBAT_ROUND_AFTER");
    DeleteLocalInt(oMod, "EVSEEN:NWNX_ON_ATTACK_TARGET_CHANGE_BEFORE");
    DeleteLocalInt(oMod, "EVSEEN:NWNX_ON_ATTACK_TARGET_CHANGE_AFTER");
    DeleteLocalInt(oMod, "EVSEEN:NWNX_ON_BROADCAST_ATTACK_OF_OPPORTUNITY_BEFORE");
    DeleteLocalInt(oMod, "EVSEEN:NWNX_ON_BROADCAST_ATTACK_OF_OPPORTUNITY_AFTER");
    PStart();
}

void Fight()
{
    Step("fight");
    PLogI("enemy", GetIsEnemy(G(), F()));
    AssignCommand(F(), ActionAttack(G()));
}

void Disarm()
{
    Step("disarm");
    AssignCommand(F(), ActionUseFeat(FEAT_DISARM, G()));
}

void Stop()
{
    Step("stop");
    // The first fight is over for good: neither goes back to it while the archer shoots.
    SetIsTemporaryFriend(G(), F());
    SetIsTemporaryFriend(F(), G());
    AssignCommand(F(), ClearAllActions(TRUE));
    AssignCommand(G(), ClearAllActions(TRUE));
    PLog("disarmed", EvObj(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, G())));
}

void Shoot()
{
    Step("shoot");
    PLogI("enemy2", GetIsEnemy(T(), A()));
    AssignCommand(A(), ActionAttack(T()));
}

void Report()
{
    Step("end");
    PDone();
}

void Pair(object a, object b)
{
    SetIsTemporaryEnemy(b, a);
    SetIsTemporaryEnemy(a, b);
}

void main()
{
    object oF = CreateObject(OBJECT_TYPE_CREATURE, "pev_ftr", PCentre(5.0, 5.0), FALSE, "FTR");
    object oG = CreateObject(OBJECT_TYPE_CREATURE, "pev_gob", PCentre(6.5, 5.0), FALSE, "GOB");
    object oA = CreateObject(OBJECT_TYPE_CREATURE, "pev_ftr", PCentre(-5.0, -5.0), FALSE, "ARCH");
    object oT = CreateObject(OBJECT_TYPE_CREATURE, "pev_gob", PCentre(-5.0, 2.0), FALSE, "TGT");
    object o;
    for (o = GetFirstObjectInArea(GetArea(oF)); GetIsObjectValid(o); o = GetNextObjectInArea(GetArea(oF)))
        if (GetObjectType(o) == OBJECT_TYPE_CREATURE) { SetAILevel(o, AI_LEVEL_NORMAL); SetImmortal(o, TRUE); }
    ChangeToStandardFaction(oF, STANDARD_FACTION_COMMONER);
    ChangeToStandardFaction(oA, STANDARD_FACTION_COMMONER);
    ChangeToStandardFaction(oG, STANDARD_FACTION_HOSTILE);
    ChangeToStandardFaction(oT, STANDARD_FACTION_HOSTILE);
    Pair(oF, oG);
    Pair(oA, oT);
    object oSword = CreateItemOnObject("nw_wswls001", oF, 1, "SWORD");
    AssignCommand(oF, ActionEquipItem(oSword, INVENTORY_SLOT_RIGHTHAND));
    object oClub = CreateItemOnObject("nw_wblcl001", oG, 1, "CLUB");
    AssignCommand(oG, ActionEquipItem(oClub, INVENTORY_SLOT_RIGHTHAND));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACDecrease(20), oG);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageResistance(DAMAGE_TYPE_SLASHING, 5, 2), oG);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACDecrease(20), oT);
    object oBow = CreateItemOnObject("nw_wbwsh001", oA, 1, "BOW");
    object oArrows = CreateItemOnObject("nw_wamar001", oA, 2, "ARROWS");
    CreateItemOnObject("nw_wamar001", oA, 5, "SPARE");
    AssignCommand(oA, ActionEquipItem(oBow, INVENTORY_SLOT_RIGHTHAND));
    AssignCommand(oA, ActionEquipItem(oArrows, INVENTORY_SLOT_ARROWS));
    DelayCommand(1.0, Watch());
    DelayCommand(2.0, Fight());
    DelayCommand(16.0, Disarm());
    DelayCommand(26.0, Stop());
    DelayCommand(28.0, Shoot());
    DelayCommand(50.0, Report());
}
