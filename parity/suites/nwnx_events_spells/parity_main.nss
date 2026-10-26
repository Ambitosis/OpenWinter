// NWNX_Events spell events: cast spell (and when it fires), broadcast, input cast, spell count decrements,
// interruptions and failures, memorised slot changes, skipping and the spell ID whitelist.
#include "pnwnxev_inc"
#include "parity_time"

object W() { return GetObjectByTag("WIZ"); }
object G() { return GetObjectByTag("GOB"); }

void Watch()
{
    EvWatch("NWNX_ON_CAST_SPELL", "SPELL_ID,o:TARGET_OBJECT_ID,o:ITEM_OBJECT_ID,MULTI_CLASS,SPELL_COUNTERED,COUNTERING_SPELL,PROJECTILE_PATH_TYPE,IS_INSTANT_SPELL,x:TARGET_POSITION_X");
    EvWatch("NWNX_ON_BROADCAST_CAST_SPELL", "SPELL_ID,MULTI_CLASS,FEAT,o:TARGET_OBJECT_ID,x:TARGET_POSITION_X,SPELL_DOMAIN,SPELL_SPONTANEOUS,SPELL_METAMAGIC,PROJECTILE_PATH_TYPE");
    EvWatch("NWNX_ON_INPUT_CAST_SPELL", "o:TARGET,SPELL_ID,MULTICLASS,DOMAIN_LEVEL,META_TYPE,INSTANT,PROJECTILE_PATH,SPONTANEOUS,FAKE,FEAT,CASTER_LEVEL,IS_AREA_TARGET,x:POS_X");
    EvWatch("NWNX_ON_DECREMENT_SPELL_COUNT", "SPELL_ID,CLASS,DOMAIN,METAMAGIC,CASTERLEVEL");
    EvWatch("NWNX_ON_SPELL_INTERRUPTED", "SPELL_ID,SPELL_CLASS,SPELL_DOMAIN,SPELL_METAMAGIC,SPELL_FEAT,SPELL_SPONTANEOUS");
    EvWatch("NWNX_ON_SPELL_FAILED", "SPELL_ID,MULTI_CLASS,DOMAIN,METAMAGIC,FEAT,SPELL_SPONTANEOUS,DEFENSIVELY_CAST,o:TARGET_OBJECT_ID,IS_INSTANT_SPELL,PROJECTILE_PATH_TYPE,CASTERLEVEL,IS_FAKE,REASON");
    EvWatch("NWNX_SET_MEMORIZED_SPELL_SLOT", "SPELL_MULTICLASS,SPELL_SLOT,SPELL_ID,SPELL_DOMAIN,SPELL_METAMAGIC,SPELL_FROMCLIENT,ACTION_RESULT");
    EvWatch("NWNX_CLEAR_MEMORIZED_SPELL_SLOT", "SPELL_MULTICLASS,SPELL_LEVEL,SPELL_SLOT");
    EvWatch("NWNX_ON_EFFECT_APPLIED", "TYPE,SPELL_ID,CASTER_LEVEL");
    NWNX_Events_SubscribeEvent("NWNX_ON_CAST_SPELL_BEFORE", "pev_t");
    NWNX_Events_SubscribeEvent("NWNX_ON_BROADCAST_CAST_SPELL_BEFORE", "pev_t");
    NWNX_Events_SubscribeEvent("NWNX_ON_INPUT_CAST_SPELL_BEFORE", "pev_t");
    NWNX_Events_SubscribeEvent("NWNX_ON_DECREMENT_SPELL_COUNT_BEFORE", "pev_t");
    // Effects on the wizard only: the order spell scripts reach their targets isn't what this suite measures.
    NWNX_Events_ToggleDispatchListMode("NWNX_ON_EFFECT_APPLIED_BEFORE", "pev_log", TRUE);
    NWNX_Events_ToggleDispatchListMode("NWNX_ON_EFFECT_APPLIED_AFTER", "pev_log", TRUE);
    NWNX_Events_AddObjectToDispatchList("NWNX_ON_EFFECT_APPLIED_BEFORE", "pev_log", W());
    NWNX_Events_AddObjectToDispatchList("NWNX_ON_EFFECT_APPLIED_AFTER", "pev_log", W());
}

void Step(string sName)
{
    object oMod = GetModule();
    EvFlush(GetLocalString(oMod, "STEP"));
    SetLocalString(oMod, "STEP", sName);
    PStart();
}

void Ready(string sKey)
{
    int i;
    string s = "";
    for (i = 0; i < 6; i++) s += IntToString(GetMemorizedSpellReady(W(), CLASS_TYPE_WIZARD, 1, i));
    PLog(sKey, s);
}

void S1()
{
    Step("armor");
    EvLog("queue");
    AssignCommand(W(), ActionCastSpellAtObject(SPELL_MAGE_ARMOR, W()));
    EvLog("queued");
}

void S2()
{
    Ready("ready.armor");
    Step("missile");
    AssignCommand(W(), ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, G()));
}

void S3()
{
    Ready("ready.missile");
    Step("cheat");
    AssignCommand(W(), ActionCastSpellAtObject(SPELL_BLESS, W(), METAMAGIC_ANY, TRUE));
}

void S4()
{
    Step("instant");
    AssignCommand(W(), ActionCastSpellAtObject(SPELL_BLESS, W(), METAMAGIC_ANY, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
}

void S5()
{
    Step("location");
    AssignCommand(W(), ActionCastSpellAtLocation(SPELL_SLEEP, GetLocation(G())));
}

void Clear() { AssignCommand(W(), ClearAllActions()); }

void S6()
{
    Ready("ready.location");
    Step("cancel");
    AssignCommand(W(), ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, G()));
    DelayCommand(0.4, Clear());
}

void S7()
{
    Ready("ready.cancel");
    Step("skipcast");
    EvSkip("NWNX_ON_CAST_SPELL_BEFORE");
    SetLocalInt(GetModule(), "HP", GetCurrentHitPoints(G()));
    AssignCommand(W(), ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, G()));
}

void S8()
{
    EvSkip("NWNX_ON_CAST_SPELL_BEFORE", FALSE);
    Ready("ready.skipcast");
    PLogI("hit.skipcast", GetCurrentHitPoints(G()) < GetLocalInt(GetModule(), "HP"));
    Step("skipdec");
    EvSkip("NWNX_ON_DECREMENT_SPELL_COUNT_BEFORE");
    SetMemorizedSpellReady(W(), CLASS_TYPE_WIZARD, 1, 0, TRUE);
    AssignCommand(W(), ActionCastSpellAtObject(SPELL_MAGE_ARMOR, W()));
}

void S9()
{
    EvSkip("NWNX_ON_DECREMENT_SPELL_COUNT_BEFORE", FALSE);
    Ready("ready.skipdec");
    Step("clear");
    ClearMemorizedSpell(W(), CLASS_TYPE_WIZARD, 1, 4);
    Ready("ready.clear");
    PLogI("id.clear", GetMemorizedSpellId(W(), CLASS_TYPE_WIZARD, 1, 4));
}

void S10()
{
    Step("whitelist");
    NWNX_Events_ToggleIDWhitelist("NWNX_ON_CAST_SPELL", TRUE);
    NWNX_Events_AddIDToWhitelist("NWNX_ON_CAST_SPELL", SPELL_MAGE_ARMOR);
    AssignCommand(W(), ActionCastSpellAtObject(SPELL_BLESS, W(), METAMAGIC_ANY, TRUE));
    AssignCommand(W(), ActionCastSpellAtObject(SPELL_MAGE_ARMOR, W(), METAMAGIC_ANY, TRUE));
}

void Hurt() { ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(40), W()); }

void S11()
{
    Step("interrupt");
    AssignCommand(W(), ActionCastSpellAtLocation(SPELL_SLEEP, GetLocation(G())));
    DelayCommand(0.6, Hurt());
}

void S12()
{
    Ready("ready.interrupt");
    Step("end");
    PDone();
}

void main()
{
    object oW = CreateObject(OBJECT_TYPE_CREATURE, "pev_wiz", PCentre(), FALSE, "WIZ");
    object oG = CreateObject(OBJECT_TYPE_CREATURE, "pev_gob", PCentre(5.0), FALSE, "GOB");
    SetAILevel(oW, AI_LEVEL_NORMAL);
    SetAILevel(oG, AI_LEVEL_NORMAL);
    SetImmortal(oG, TRUE);
    SetImmortal(oW, TRUE);
    DelayCommand(0.5, Watch());
    DelayCommand(1.0, S1());
    DelayCommand(8.0, S2());
    DelayCommand(15.0, S3());
    DelayCommand(22.0, S4());
    DelayCommand(29.0, S5());
    DelayCommand(36.0, S6());
    DelayCommand(43.0, S7());
    DelayCommand(50.0, S8());
    DelayCommand(57.0, S9());
    DelayCommand(58.0, S10());
    DelayCommand(66.0, S11());
    DelayCommand(73.0, S12());
}
