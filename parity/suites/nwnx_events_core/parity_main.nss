// NWNX_Events dispatch: subscription order and duplicates, event data, nesting, script chunks, dispatch lists,
// unsubscribing, and the effect applied/removed events with skipping.
#include "pnwnxev_inc"

const string CHUNK = "SetLocalString(GetModule(), \"EVLOG\", GetLocalString(GetModule(), \"EVLOG\") + \"chunk(\" + GetTag(OBJECT_SELF) + \");\");";

void Custom(object oC)
{
    object oMod = GetModule();
    PLog("out.current", NWNX_Events_GetCurrentEvent());
    PLog("out.data", NWNX_Events_GetEventData("K"));
    PLogI("num.none", NWNX_Events_GetNumSubscribers("EVT_A"));
    NWNX_Events_SubscribeEvent("EVT_A", "ev_a");
    NWNX_Events_SubscribeEvent("EVT_A", "ev_b");
    NWNX_Events_SubscribeEvent("EVT_A", "ev_a");
    PLogI("num.a", NWNX_Events_GetNumSubscribers("EVT_A"));
    NWNX_Events_PushEventData("K", "v1");
    PLogI("sig.a", NWNX_Events_SignalEvent("EVT_A", oMod));
    EvFlush("log.a1");
    PLogI("sig.a2", NWNX_Events_SignalEvent("EVT_A", OBJECT_INVALID));
    EvFlush("log.a2");
    PLogI("sig.none", NWNX_Events_SignalEvent("EVT_NONE", oMod));
    NWNX_Events_PushEventData("K", "stale");
    NWNX_Events_SignalEvent("EVT_NONE", oMod);
    NWNX_Events_SignalEvent("EVT_A", oC);
    EvFlush("log.a3");
    NWNX_Events_PushEventData("K", "x1");
    NWNX_Events_PushEventData("K", "x2");
    NWNX_Events_SignalEvent("EVT_A", oC);
    EvFlush("log.a4");

    NWNX_Events_SubscribeEvent("EVT_N", "ev_nest");
    NWNX_Events_PushEventData("K", "outer");
    NWNX_Events_SignalEvent("EVT_N", oC);
    EvFlush("log.nest");

    NWNX_Events_SubscribeEvent("EVT_O", "ev_a");
    NWNX_Events_SubscribeEventScriptChunk("EVT_O", CHUNK);
    NWNX_Events_SubscribeEvent("EVT_O", "ev_b");
    NWNX_Events_SubscribeEventScriptChunk("EVT_O", CHUNK);
    PLogI("num.o", NWNX_Events_GetNumSubscribers("EVT_O"));
    NWNX_Events_SignalEvent("EVT_O", oC);
    EvFlush("log.o");
    NWNX_Events_UnsubscribeEventScriptChunk("EVT_O", CHUNK);
    NWNX_Events_UnsubscribeEvent("EVT_O", "ev_a");
    PLogI("num.o2", NWNX_Events_GetNumSubscribers("EVT_O"));
    NWNX_Events_SignalEvent("EVT_O", oC);
    EvFlush("log.o2");
    NWNX_Events_UnsubscribeEvent("EVT_O", "ev_zzz");
    NWNX_Events_SubscribeEvent("EVT_O", "ev_a");
    NWNX_Events_SignalEvent("EVT_O", oC);
    EvFlush("log.o3");

    NWNX_Events_SubscribeEvent("EVT_D", "ev_a");
    NWNX_Events_SubscribeEvent("EVT_D", "ev_b");
    NWNX_Events_ToggleDispatchListMode("EVT_D", "ev_a", TRUE);
    PLogI("sig.d1", NWNX_Events_SignalEvent("EVT_D", oC));
    EvFlush("log.d1");
    NWNX_Events_AddObjectToDispatchList("EVT_D", "ev_a", oC);
    NWNX_Events_SignalEvent("EVT_D", oC);
    EvFlush("log.d2");
    NWNX_Events_SignalEvent("EVT_D", oMod);
    EvFlush("log.d3");
    NWNX_Events_RemoveObjectFromDispatchList("EVT_D", "ev_a", oC);
    NWNX_Events_SignalEvent("EVT_D", oC);
    EvFlush("log.d4");
    NWNX_Events_AddObjectToDispatchList("EVT_D", "ev_a", oC);
    NWNX_Events_ToggleDispatchListMode("EVT_D", "ev_a", FALSE);
    NWNX_Events_SignalEvent("EVT_D", oMod);
    EvFlush("log.d5");
    NWNX_Events_ToggleDispatchListMode("EVT_D", "ev_a", TRUE);
    NWNX_Events_SignalEvent("EVT_D", oC);
    EvFlush("log.d6");
    NWNX_Events_SubscribeEvent("EVT_D2", "ev_a");
    NWNX_Events_ToggleDispatchListMode("EVT_D2", "ev_a", TRUE);
    PLogI("sig.d7", NWNX_Events_SignalEvent("EVT_D2", oC));
    EvFlush("log.d7");
    NWNX_Events_ToggleDispatchListMode("EVT_D3", "ev_a", TRUE);
    NWNX_Events_AddObjectToDispatchList("EVT_D3", "ev_a", oC);
    NWNX_Events_SubscribeEvent("EVT_D3", "ev_a");
    NWNX_Events_SignalEvent("EVT_D3", oC);
    NWNX_Events_SignalEvent("EVT_D3", oMod);
    EvFlush("log.d8");

    NWNX_Events_SubscribeEvent("EVT_U", "ev_a");
    NWNX_Events_SubscribeEvent("EVT_U", "ev_b");
    NWNX_Events_SubscribeEventScriptChunk("EVT_U", CHUNK);
    NWNX_Events_UnsubscribeAllStartingWith("ev_a");
    PLogI("num.u1", NWNX_Events_GetNumSubscribers("EVT_U"));
    NWNX_Events_UnsubscribeAllStartingWith("SetLocal");
    PLogI("num.u2", NWNX_Events_GetNumSubscribers("EVT_U"));
    NWNX_Events_UnsubscribeAllStartingWith("EV_B");
    PLogI("num.u3", NWNX_Events_GetNumSubscribers("EVT_U"));
    NWNX_Events_SubscribeEvent("OTHER_A", "ev_a");
    NWNX_Events_UnsubscribeAllStartingWith("EVT_");
    PLogI("num.all.a", NWNX_Events_GetNumSubscribers("EVT_A"));
    PLogI("num.all.o", NWNX_Events_GetNumSubscribers("EVT_O"));
    PLogI("num.all.other", NWNX_Events_GetNumSubscribers("OTHER_A"));
    NWNX_Events_UnsubscribeAllStartingWith("");
    PLogI("num.all.other2", NWNX_Events_GetNumSubscribers("OTHER_A"));
}

int CountEffects(object o)
{
    int n = 0;
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { n++; e = GetNextEffect(o); }
    return n;
}

void Effects(object oT)
{
    string sTags = "x:UNIQUE_ID,o:CREATOR,TYPE,SUB_TYPE,DURATION_TYPE,DURATION,SPELL_ID,CASTER_LEVEL,CUSTOM_TAG,"
        + "INT_PARAM_1,INT_PARAM_2,INT_PARAM_3,INT_PARAM_4,FLOAT_PARAM_1,STRING_PARAM_1,o:OBJECT_PARAM_1,x:NOPE";
    EvWatch("NWNX_ON_EFFECT_APPLIED", sTags);
    EvWatch("NWNX_ON_EFFECT_REMOVED", sTags);
    EvLog("pre");
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, TagEffect(EffectACIncrease(3, AC_DODGE_BONUS), "dodge"), oT, 12.5);
    EvLog("post");
    EvFlush("fx.ac");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectLinkEffects(EffectAttackIncrease(1), EffectVisualEffect(VFX_DUR_GLOW_BLUE)), oT);
    EvFlush("fx.link");
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectHeal(3), oT);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectVisualEffect(VFX_IMP_HEALING_S), oT);
    EvFlush("fx.instant");
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectVisualEffect(VFX_DUR_GLOW_RED), oT, 5.0);
    EvFlush("fx.vfx");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageResistance(DAMAGE_TYPE_FIRE, 5, 20), oT);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_HIDE, 4), oT);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityDecrease(ABILITY_STRENGTH, 2), oT);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectConcealment(20, MISS_CHANCE_TYPE_VS_RANGED), oT);
    EvFlush("fx.more");
    PLogI("fx.count", CountEffects(oT));

    effect e = GetFirstEffect(oT);
    while (GetIsEffectValid(e))
    {
        if (GetEffectTag(e) == "dodge") RemoveEffect(oT, e);
        e = GetNextEffect(oT);
    }
    EvFlush("fx.remove");
    PLogI("fx.count2", CountEffects(oT));

    EvSkip("NWNX_ON_EFFECT_APPLIED_BEFORE");
    int nAC = GetAC(oT);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectACDecrease(2), "skipme"), oT);
    EvSkip("NWNX_ON_EFFECT_APPLIED_BEFORE", FALSE);
    EvFlush("fx.skip");
    PLogI("fx.skip.count", CountEffects(oT));
    PLogI("fx.skip.ac", GetAC(oT) - nAC);

    EvSkip("NWNX_ON_EFFECT_REMOVED_BEFORE");
    e = GetFirstEffect(oT);
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e) == EFFECT_TYPE_SKILL_INCREASE) RemoveEffect(oT, e);
        e = GetNextEffect(oT);
    }
    EvSkip("NWNX_ON_EFFECT_REMOVED_BEFORE", FALSE);
    EvFlush("fx.skipremove");
    PLogI("fx.skipremove.count", CountEffects(oT));
}

void main()
{
    object oC = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(), FALSE, "GOB_C");
    object oT = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(3.0), FALSE, "GOB_T");
    Custom(oC);
    Effects(oT);
    DelayCommand(1.0, EvFlush("log.delayed"));
    DelayCommand(1.5, PDone());
}
