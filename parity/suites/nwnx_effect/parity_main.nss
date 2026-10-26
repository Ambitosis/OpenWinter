// NWNX_Effect: unpacked fields of constructed, linked and applied effects; packing, replacing and removing.
#include "parity_inc"
#include "nwnx_effect"

string V(vector v) { return FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2) + "," + FloatToString(v.z, 0, 2); }
string O(object o) { return o == OBJECT_INVALID ? "inv" : GetIsObjectValid(o) ? GetTag(o) : "gone"; }

void LogU(string k, struct NWNX_EffectUnpacked u, int bId = FALSE)
{
    if (bId) PLog(k + ".id", u.sID);
    PLog(k + ".type", IntToString(u.nType) + "/" + IntToString(u.nSubType));
    PLogF(k + ".dur", u.fDuration);
    PLog(k + ".expiry", IntToString(u.nExpiryCalendarDay > 0) + "/" + IntToString(u.nExpiryTimeOfDay > 0));
    PLog(k + ".creator", O(u.oCreator));
    PLog(k + ".spell", IntToString(u.nSpellId) + "/" + IntToString(u.nCasterLevel));
    PLog(k + ".expose", IntToString(u.bExpose) + "/" + IntToString(u.bShowIcon));
    PLog(k + ".links", IntToString(u.bLinkLeftValid) + "/" + IntToString(u.bLinkRightValid));
    PLog(k + ".ints", IntToString(u.nNumIntegers) + ":" + IntToString(u.nParam0) + "," + IntToString(u.nParam1) + "," + IntToString(u.nParam2) + "," + IntToString(u.nParam3) + "," + IntToString(u.nParam4) + "," + IntToString(u.nParam5) + "," + IntToString(u.nParam6) + "," + IntToString(u.nParam7));
    PLog(k + ".floats", FloatToString(u.fParam0, 0, 2) + "," + FloatToString(u.fParam1, 0, 2) + "," + FloatToString(u.fParam2, 0, 2) + "," + FloatToString(u.fParam3, 0, 2));
    PLog(k + ".strings", u.sParam0 + "," + u.sParam1 + "," + u.sParam2 + "," + u.sParam3 + "," + u.sParam4 + "," + u.sParam5);
    PLog(k + ".objects", O(u.oParam0) + "," + O(u.oParam1) + "," + O(u.oParam2) + "," + O(u.oParam3));
    PLog(k + ".vectors", V(u.vParam0) + "/" + V(u.vParam1));
    PLog(k + ".tag", u.sTag + "|" + u.sItemProp);
}

void Nat(string k, effect e)
{
    struct NWNX_EffectUnpacked u = NWNX_Effect_UnpackEffect(e);
    PLog("nat." + k, IntToString(u.nType) + "/" + IntToString(u.nSubType) + "/" + IntToString(GetEffectType(e, TRUE)) + "/" + IntToString(u.nNumIntegers)
        + ":" + IntToString(u.nParam0) + "," + IntToString(u.nParam1) + "," + IntToString(u.nParam2) + "," + IntToString(u.nParam3) + "," + IntToString(u.nParam4) + "," + IntToString(u.nParam5) + "," + IntToString(u.nParam6) + "," + IntToString(u.nParam7)
        + ":" + FloatToString(u.fParam0, 0, 2) + ":" + u.sParam0 + ":" + O(u.oParam0));
}

string Fx(object o)
{
    string s = "";
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        s += " " + IntToString(GetEffectType(e)) + ":" + IntToString(GetEffectDurationType(e)) + ":" + GetEffectTag(e) + ":" + IntToString(GetEffectSpellId(e));
        e = GetNextEffect(o);
    }
    return s;
}

void main()
{
    object oC = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(), FALSE, "FX_GOB");
    object oD = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(3.0), FALSE, "FX_OTHER");

    struct NWNX_EffectUnpacked u = NWNX_Effect_UnpackEffect(EffectAttackIncrease(3, ATTACK_BONUS_OFFHAND));
    LogU("ab", u);
    u = NWNX_Effect_UnpackEffect(EffectDamage(7, DAMAGE_TYPE_FIRE, DAMAGE_POWER_PLUS_TWO));
    LogU("dmg", u);
    u = NWNX_Effect_UnpackEffect(TagEffect(SupernaturalEffect(EffectACIncrease(2, AC_DEFLECTION_BONUS)), "tagged"));
    LogU("ac", u);
    u = NWNX_Effect_UnpackEffect(EffectVisualEffect(VFX_DUR_GLOW_BLUE));
    LogU("vfx", u);
    u = NWNX_Effect_UnpackEffect(EffectSavingThrowIncrease(SAVING_THROW_WILL, 2, SAVING_THROW_TYPE_FEAR));
    LogU("save", u);
    u = NWNX_Effect_UnpackEffect(VersusRacialTypeEffect(EffectSkillIncrease(SKILL_SPOT, 4), RACIAL_TYPE_ELF));
    LogU("skill", u);
    u = NWNX_Effect_UnpackEffect(EffectBeam(VFX_BEAM_LIGHTNING, oD, BODY_NODE_HAND));
    LogU("beam", u);
    u = NWNX_Effect_UnpackEffect(EffectRunScript("x_apply", "x_remove", "x_tick", 6.0, "data"));
    LogU("script", u);
    effect eL = EffectLinkEffects(EffectAttackIncrease(1), EffectACIncrease(1));
    u = NWNX_Effect_UnpackEffect(eL);
    LogU("link", u);
    struct NWNX_EffectUnpacked ul = NWNX_Effect_UnpackEffect(u.eLinkLeft);
    LogU("link.left", ul);
    ul = NWNX_Effect_UnpackEffect(u.eLinkRight);
    LogU("link.right", ul);
    effect eL3 = EffectLinkEffects(EffectSkillIncrease(SKILL_HIDE, 2), eL);
    u = NWNX_Effect_UnpackEffect(eL3);
    LogU("link3", u);
    ul = NWNX_Effect_UnpackEffect(u.eLinkLeft);
    LogU("link3.left", ul);
    ul = NWNX_Effect_UnpackEffect(u.eLinkRight);
    LogU("link3.right", ul);
    u = NWNX_Effect_UnpackEffect(GetFirstEffect(OBJECT_INVALID));
    LogU("invalid", u);
    PLogI("valid.invalid", NWNX_Effect_GetIsEffectValid(GetFirstEffect(OBJECT_INVALID)));
    PLogI("valid.ab", NWNX_Effect_GetIsEffectValid(EffectAttackIncrease(1)));
    PLogI("valid.link", NWNX_Effect_GetIsEffectValid(eL));

    // Applied effects.
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, TagEffect(EffectAttackIncrease(2), "temp"), oC, 30.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, eL, oC);
    effect eApplied = GetFirstEffect(oC);
    u = NWNX_Effect_UnpackEffect(eApplied);
    LogU("applied", u);
    string sId = u.sID;
    PLogI("idnum", StringToInt(sId) > 0);
    PLogI("count.applied", NWNX_Effect_GetAppliedEffectCount(oC));
    PLogI("count.true", NWNX_Effect_GetTrueEffectCount(oC));
    int i;
    int n = NWNX_Effect_GetAppliedEffectCount(oC);
    string sAll = "";
    for (i = 0; i < n; i++)
    {
        effect e = NWNX_Effect_GetAppliedEffect(oC, i);
        sAll += " " + IntToString(GetEffectType(e, TRUE)) + "/" + IntToString(NWNX_Effect_GetIsEffectValid(e)) + "/" + IntToString(GetIsEffectValid(e)) + "/" + GetEffectTag(e);
    }
    PLog("applied.all", sAll);
    n = NWNX_Effect_GetTrueEffectCount(oC);
    sAll = "";
    for (i = 0; i < n; i++)
    {
        u = NWNX_Effect_GetTrueEffect(oC, i);
        sAll += " " + IntToString(u.nType) + "/" + IntToString(u.nSubType) + "/" + u.sTag + "/" + IntToString(u.bExpose);
    }
    PLog("true.all", sAll);
    PLog("fx.before", Fx(oC));

    // Packing.
    struct NWNX_EffectUnpacked p = NWNX_Effect_UnpackEffect(EffectAttackIncrease(4));
    p.sTag = "packed";
    p.nSpellId = 27;
    p.oCreator = oD;
    effect ePacked = NWNX_Effect_PackEffect(p);
    PLog("packed", IntToString(GetEffectType(ePacked)) + ":" + GetEffectTag(ePacked) + ":" + IntToString(GetEffectSpellId(ePacked)) + ":" + O(GetEffectCreator(ePacked)) + ":" + IntToString(GetEffectInteger(ePacked, 0)));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, ePacked, oC);
    PLog("packed.fx", Fx(oC));
    p.nParam0 = 6;
    PLog("packed6", IntToString(GetEffectInteger(NWNX_Effect_PackEffect(p), 0)));
    struct NWNX_EffectUnpacked q = NWNX_Effect_UnpackEffect(EffectAttackIncrease(1));
    q.bLinkLeftValid = TRUE; q.eLinkLeft = EffectACIncrease(3);
    q.bLinkRightValid = TRUE; q.eLinkRight = EffectSkillIncrease(SKILL_HIDE, 1);
    effect eq = NWNX_Effect_PackEffect(q);
    u = NWNX_Effect_UnpackEffect(eq);
    // The original keeps such an effect as an attack bonus with two links; OpenWinter makes it a link of all three
    // (known difference), so only the parts are compared.
    WriteTimestampedLogEntry("RAW|packlink.type|" + IntToString(u.nType) + "/" + IntToString(u.nSubType) + " ints " + IntToString(u.nParam0));
    PLog("packlink.links", IntToString(u.bLinkLeftValid) + "/" + IntToString(u.bLinkRightValid));

    // Creator.
    effect eCr = NWNX_Effect_SetEffectCreator(EffectACIncrease(1), oD);
    PLog("creator", O(GetEffectCreator(eCr)));

    // Replace and remove.
    effect eOld = GetFirstEffect(oC);
    struct NWNX_EffectUnpacked uold = NWNX_Effect_UnpackEffect(eOld);
    effect eNew = TagEffect(EffectAttackIncrease(9), "replaced");
    PLogI("replace", NWNX_Effect_ReplaceEffect(oC, eOld, eNew));
    PLog("fx.replaced", Fx(oC));
    PLogI("replace.bad", NWNX_Effect_ReplaceEffect(oC, EffectACIncrease(1), EffectACIncrease(2)));
    struct NWNX_EffectUnpacked r = NWNX_Effect_GetTrueEffect(oC, 0);
    r.sTag = "byindex";
    r.fDuration = 99.0;
    NWNX_Effect_ReplaceEffectByIndex(oC, 0, r);
    PLog("fx.byindex", Fx(oC));
    r = NWNX_Effect_GetTrueEffect(oC, 0);
    LogU("byindex", r);
    PLogI("remove", NWNX_Effect_RemoveEffectById(oC, uold.sID));
    PLog("fx.removed", Fx(oC));
    PLogI("remove.again", NWNX_Effect_RemoveEffectById(oC, uold.sID));
    PLogI("remove.bad", NWNX_Effect_RemoveEffectById(oC, "notanid"));

    // Apply without the usual checks.
    int nAC = GetAC(oD);
    NWNX_Effect_Apply(EffectACIncrease(5), oD);
    PLog("fx.apply", Fx(oD));
    PLogI("apply.ac", GetAC(oD) - nAC);
    NWNX_Effect_Apply(EffectDamage(1), oD);
    PLog("fx.applydmg", Fx(oD));
    PLogI("applydmg.hp", GetCurrentHitPoints(oD));
    PLogI("count.bad", NWNX_Effect_GetAppliedEffectCount(OBJECT_INVALID));
    PLogI("true.bad", NWNX_Effect_GetTrueEffectCount(OBJECT_INVALID));
    PLogI("applied.oob", GetIsEffectValid(NWNX_Effect_GetAppliedEffect(oC, 99)));

    // Native numbers of more constructors.
    Nat("heal", EffectHeal(3));
    Nat("death", EffectDeath());
    Nat("res", EffectResurrection());
    Nat("knock", EffectKnockdown());
    Nat("summon", EffectSummonCreature("nw_goblina"));
    Nat("thp", EffectTemporaryHitpoints(4));
    Nat("dispel", EffectDispelMagicAll(5));
    Nat("dispelbest", EffectDispelMagicBest(5));
    Nat("aoe", EffectAreaOfEffect(AOE_PER_FOGACID));
    Nat("poly", EffectPolymorph(POLYMORPH_TYPE_BADGER));
    Nat("disappear", EffectDisappear());
    Nat("appear", EffectAppear());
    Nat("icon", EffectIcon(5));
    Nat("dmgshield", EffectDamageShield(2, DAMAGE_BONUS_1d4, DAMAGE_TYPE_FIRE));
    Nat("concealment", EffectConcealment(20));
    Nat("swarm", EffectSwarm(FALSE, "nw_goblina"));
    Nat("hpdying", EffectHitPointChangeWhenDying(1.0));
    Nat("timestop", EffectTimeStop());
    Nat("pacify", EffectPacified());
    Nat("bonusfeat", EffectBonusFeat(FEAT_ALERTNESS));
    Nat("dmgcold", EffectDamage(5, DAMAGE_TYPE_COLD));
    Nat("dmgslash", EffectDamage(4, DAMAGE_TYPE_SLASHING, DAMAGE_POWER_PLUS_ONE));
    Nat("dmgmulti", EffectDamage(6, DAMAGE_TYPE_BLUDGEONING));

    // Ids: fresh effects, applied copies, link mates, icons.
    effect eFresh = EffectAttackIncrease(1);
    struct NWNX_EffectUnpacked uf = NWNX_Effect_UnpackEffect(eFresh);
    PLogI("id.fresh", StringToInt(uf.sID) > 0);
    object oE = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(-3.0), FALSE, "FX_IDS");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectLinkEffects(EffectAttackIncrease(1), EffectSkillIncrease(SKILL_HIDE, 2)), oE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectACIncrease(2), "solo"), oE);
    n = NWNX_Effect_GetTrueEffectCount(oE);
    string sIds = "";
    string sFirst = "";
    for (i = 0; i < n; i++)
    {
        struct NWNX_EffectUnpacked t = NWNX_Effect_GetTrueEffect(oE, i);
        if (i == 0) sFirst = t.sID;
        sIds += " " + IntToString(t.nType) + "=" + IntToString(t.sID == sFirst);
        if (t.nType == 67) PLog("icon." + IntToString(i), IntToString(t.nSubType) + ":" + IntToString(t.nParam0) + "," + IntToString(t.nParam1) + ":" + IntToString(t.bExpose) + "/" + IntToString(t.bShowIcon) + ":" + O(t.oCreator) + ":" + t.sTag);
    }
    PLog("ids", sIds);
    n = NWNX_Effect_GetTrueEffectCount(oC);
    for (i = 0; i < n; i++)
    {
        struct NWNX_EffectUnpacked t = NWNX_Effect_GetTrueEffect(oC, i);
        if (t.nType == 68) LogU("innate", t);
    }
    effect eFirst = GetFirstEffect(oE);
    PLogI("id.applied", NWNX_Effect_UnpackEffect(eFirst).sID == sFirst);
    // Removing a link mate's id.
    PLogI("removelink", NWNX_Effect_RemoveEffectById(oE, sFirst));
    PLog("fx.removelink", Fx(oE));
    PLogI("removelink.true", NWNX_Effect_GetTrueEffectCount(oE));
    // ReplaceEffect on a link mate.
    effect eSolo = GetFirstEffect(oE);
    PLogI("replace.keep", NWNX_Effect_ReplaceEffect(oE, eSolo, TagEffect(SupernaturalEffect(EffectACIncrease(3)), "kept")));
    PLog("fx.replacekeep", Fx(oE));
    PLogI("replace.keepac", GetEffectInteger(GetFirstEffect(oE), 1));
    eSolo = GetFirstEffect(oE);
    PLogI("replace.type", NWNX_Effect_ReplaceEffect(oE, eSolo, EffectAttackIncrease(3)));
    // Unpack, edit, pack and apply.
    struct NWNX_EffectUnpacked ue = NWNX_Effect_UnpackEffect(EffectSkillIncrease(SKILL_SPOT, 1));
    ue.nParam1 = 7;
    ue.nSubType = SUBTYPE_EXTRAORDINARY;
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, NWNX_Effect_PackEffect(ue), oE);
    effect eSk = GetFirstEffect(oE);
    while (GetIsEffectValid(eSk) && GetEffectType(eSk) != EFFECT_TYPE_SKILL_INCREASE) eSk = GetNextEffect(oE);
    PLog("packed.skill", IntToString(GetEffectInteger(eSk, 0)) + "/" + IntToString(GetEffectInteger(eSk, 1)) + "/" + IntToString(GetEffectSubType(eSk)));
    PLogI("packed.rank", GetSkillRank(SKILL_SPOT, oE));
    // A packed link applies both parts.
    struct NWNX_EffectUnpacked uk = NWNX_Effect_UnpackEffect(EffectLinkEffects(EffectSkillIncrease(SKILL_LISTEN, 3), EffectACIncrease(1)));
    uk.sTag = "plink";
    object oF = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(-6.0), FALSE, "FX_PLINK");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, NWNX_Effect_PackEffect(uk), oF);
    PLog("fx.plink", Fx(oF));
    PDone();
}
