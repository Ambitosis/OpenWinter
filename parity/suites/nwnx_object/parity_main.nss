// NWNX_Object: locks, traps, doors, damage immunity, visual effects, inventories, areas of effect, spell defences,
// UUIDs, export, and the older functions of the plugin.
#include "parity_inc"
#include "nwnx_object"
#include "nwnx_effect"

string O(object o) { return o == OBJECT_INVALID ? "inv" : GetIsObjectValid(o) ? GetTag(o) : "gone"; }

void PRaw(string sKey, string s)
{
    int i = 0;
    while (s != "")
    {
        WriteTimestampedLogEntry("RAW|" + sKey + "." + IntToString(i) + "|" + GetStringLeft(s, 800));
        s = GetSubString(s, 800, 1000000);
        i++;
    }
}

// Creatures playing each animation, read a moment later.
object Animated(int nAnim, float fX)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(fX, 15.0), FALSE, "OBJ_ANIM" + IntToString(nAnim));
    SetAILevel(o, AI_LEVEL_NORMAL);
    AssignCommand(o, ActionPlayAnimation(nAnim, 1.0, 10.0));
    return o;
}

void Later(object oGob, object oAoe)
{
    PLogF("aoe.remaining", NWNX_Object_GetAoEObjectDurationRemaining(oAoe), 0.6);
    PLogI("anim.idle", NWNX_Object_GetCurrentAnimation(oGob));
    int i;
    for (i = 0; i < 120; i++)
    {
        object o = GetObjectByTag("OBJ_ANIM" + IntToString(i));
        if (GetIsObjectValid(o)) PLogI("anim." + IntToString(i), NWNX_Object_GetCurrentAnimation(o));
    }
    PLogI("anim.walk", NWNX_Object_GetCurrentAnimation(GetObjectByTag("OBJ_WALK")));
    PLogI("anim.run", NWNX_Object_GetCurrentAnimation(GetObjectByTag("OBJ_RUN")));
    PLogI("anim.dead", NWNX_Object_GetCurrentAnimation(GetObjectByTag("OBJ_DEAD")));
    PLogI("anim.chest", NWNX_Object_GetCurrentAnimation(GetObjectByTag("OBJ_CHEST")));
    PLogI("anim.invalid", NWNX_Object_GetCurrentAnimation(OBJECT_INVALID));
    PDone();
}

void main()
{
    object oArea = GetObjectByTag("PARITY_AREA");
    object oGob = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(), FALSE, "OBJ_GOB");
    object oCaster = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(3.0), FALSE, "OBJ_CASTER");
    object oChest = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(-3.0), FALSE, "OBJ_CHEST");
    object oWp = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", PCentre(), FALSE, "OBJ_WP");
    object oDoor = GetObjectByTag("OBJ_DOOR");
    object oDoorInvis = GetObjectByTag("OBJ_DOOR_INVIS");
    object oTrig = GetObjectByTag("OBJ_TRIG");

    // Locks and traps.
    PLogI("autokey.chest", NWNX_Object_GetAutoRemoveKey(oChest));
    NWNX_Object_SetAutoRemoveKey(oChest, TRUE);
    PLogI("autokey.chestset", NWNX_Object_GetAutoRemoveKey(oChest));
    PLogI("autokey.door", NWNX_Object_GetAutoRemoveKey(oDoor));
    PLogI("autokey.wp", NWNX_Object_GetAutoRemoveKey(oWp));
    CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_SPIKE, oChest);
    PLog("trap.creator0", O(GetTrapCreator(oChest)));
    NWNX_Object_SetTrapCreator(oChest, oGob);
    PLog("trap.creator", O(GetTrapCreator(oChest)));
    NWNX_Object_SetTrapCreator(oChest, oWp);
    PLog("trap.creatorwp", O(GetTrapCreator(oChest)));

    // Doors.
    PLogI("door.visible", NWNX_Object_GetDoorHasVisibleModel(oDoor));
    PLogI("door.invisible", NWNX_Object_GetDoorHasVisibleModel(oDoorInvis));
    PLogI("door.notdoor", NWNX_Object_GetDoorHasVisibleModel(oChest));

    // Damage immunity.
    PLogI("dmgimm.none", NWNX_Object_GetDamageImmunity(oGob, DAMAGE_TYPE_FIRE));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageImmunityIncrease(DAMAGE_TYPE_FIRE, 60), oGob);
    PLogI("dmgimm.one", NWNX_Object_GetDamageImmunity(oGob, DAMAGE_TYPE_FIRE));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageImmunityIncrease(DAMAGE_TYPE_FIRE, 60), oGob);
    PLogI("dmgimm.two", NWNX_Object_GetDamageImmunity(oGob, DAMAGE_TYPE_FIRE));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageImmunityDecrease(DAMAGE_TYPE_COLD, 30), oGob);
    PLogI("dmgimm.cold", NWNX_Object_GetDamageImmunity(oGob, DAMAGE_TYPE_COLD));
    PLogI("dmgimm.acid", NWNX_Object_GetDamageImmunity(oGob, DAMAGE_TYPE_ACID));
    PLogI("dmgimm.chest", NWNX_Object_GetDamageImmunity(oChest, DAMAGE_TYPE_FIRE));

    // Visual effects.
    PLogI("vfx.none", NWNX_Object_GetHasVisualEffect(oGob, VFX_DUR_GLOW_BLUE));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectVisualEffect(VFX_DUR_GLOW_BLUE), oGob);
    PLogI("vfx.dur", NWNX_Object_GetHasVisualEffect(oGob, VFX_DUR_GLOW_BLUE));
    PLogI("vfx.other", NWNX_Object_GetHasVisualEffect(oGob, VFX_DUR_GLOW_RED));
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectVisualEffect(VFX_IMP_HEALING_S), oGob);
    PLogI("vfx.imp", NWNX_Object_GetHasVisualEffect(oGob, VFX_IMP_HEALING_S));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectBeam(VFX_BEAM_LIGHTNING, oCaster, BODY_NODE_CHEST), oGob);
    PLogI("vfx.beam", NWNX_Object_GetHasVisualEffect(oGob, VFX_BEAM_LIGHTNING));

    // Inventories.
    object oInv = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(6.0), FALSE, "OBJ_INV");
    PLogI("inv.start", NWNX_Object_GetInventoryItemCount(oInv));
    CreateItemOnObject("nw_it_mpotion001", oInv);
    CreateItemOnObject("nw_it_mpotion002", oInv, 3);
    object oBag = CreateItemOnObject("nw_it_contain001", oInv);
    CreateItemOnObject("nw_it_mpotion003", oBag);
    PLogI("inv.creature", NWNX_Object_GetInventoryItemCount(oInv));
    PLogI("inv.bag", NWNX_Object_GetInventoryItemCount(oBag));
    CreateItemOnObject("nw_it_mpotion003", oChest);
    PLogI("inv.chest", NWNX_Object_GetInventoryItemCount(oChest));
    PLogI("inv.wp", NWNX_Object_GetInventoryItemCount(oWp));

    // Areas of effect.
    ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY, EffectAreaOfEffect(AOE_PER_FOGACID, "****", "****", "****"), PCentre(-10.0, -10.0), 12.0);
    object oAoe = GetNearestObjectToLocation(OBJECT_TYPE_AREA_OF_EFFECT, PCentre(-10.0, -10.0));
    PLogI("aoe.valid", GetIsObjectValid(oAoe));
    PLogF("aoe.radius", NWNX_Object_GetAoEObjectRadius(oAoe));
    NWNX_Object_SetAoEObjectRadius(oAoe, 3.0);
    PLogF("aoe.radiusset", NWNX_Object_GetAoEObjectRadius(oAoe));
    NWNX_Object_SetAoEObjectRadius(oAoe, -1.0);
    PLogF("aoe.radiusbad", NWNX_Object_GetAoEObjectRadius(oAoe));
    PLogF("aoe.notaoe", NWNX_Object_GetAoEObjectRadius(oGob));
    PLogF("aoe.notaoedur", NWNX_Object_GetAoEObjectDurationRemaining(oGob));

    // Spell defences.
    object oDef = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(-6.0), FALSE, "OBJ_DEF");
    PLogI("immune.none", NWNX_Object_DoSpellImmunity(oDef, oCaster, SPELL_MAGIC_MISSILE));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSpellImmunity(SPELL_MAGIC_MISSILE), oDef);
    PLogI("immune.mm", NWNX_Object_DoSpellImmunity(oDef, oCaster, SPELL_MAGIC_MISSILE));
    PLogI("immune.other", NWNX_Object_DoSpellImmunity(oDef, oCaster, SPELL_FIREBALL));
    PLogI("immune.default", NWNX_Object_DoSpellImmunity(oDef, oCaster));
    PLogI("absorb.none", NWNX_Object_DoSpellLevelAbsorption(oDef, oCaster, SPELL_MAGIC_MISSILE));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSpellLevelAbsorption(2, 0), oDef);
    PLogI("absorb.globe", NWNX_Object_DoSpellLevelAbsorption(oDef, oCaster, SPELL_MAGIC_MISSILE));
    PLogI("absorb.globehigh", NWNX_Object_DoSpellLevelAbsorption(oDef, oCaster, SPELL_FIREBALL));
    PLogI("absorb.globelevel", NWNX_Object_DoSpellLevelAbsorption(oDef, oCaster, SPELL_FIREBALL, 1));
    object oMan = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(-9.0), FALSE, "OBJ_MANTLE");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSpellLevelAbsorption(9, 4), oMan);
    PLogI("absorb.mantle", NWNX_Object_DoSpellLevelAbsorption(oMan, oCaster, SPELL_FIREBALL));
    effect eM = GetFirstEffect(oMan);
    while (GetIsEffectValid(eM) && GetEffectType(eM) != EFFECT_TYPE_SPELLLEVELABSORPTION) eM = GetNextEffect(oMan);
    PLog("absorb.mantleleft", IntToString(GetIsEffectValid(eM)) + "/" + IntToString(GetEffectInteger(eM, 1)));
    PLogI("absorb.mantle2", NWNX_Object_DoSpellLevelAbsorption(oMan, oCaster, SPELL_FIREBALL));
    eM = GetFirstEffect(oMan);
    while (GetIsEffectValid(eM) && GetEffectType(eM) != EFFECT_TYPE_SPELLLEVELABSORPTION) eM = GetNextEffect(oMan);
    PLogI("absorb.mantlegone", GetIsEffectValid(eM));

    // Spell effects of a caster on others.
    object oVic = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(9.0), FALSE, "OBJ_VICTIM");
    struct NWNX_EffectUnpacked u = NWNX_Effect_UnpackEffect(EffectACIncrease(1));
    u.nSpellId = SPELL_MAGE_ARMOR; u.oCreator = oCaster; u.sTag = "spell";
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, NWNX_Effect_PackEffect(u), oVic);
    u.sTag = "self";
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, NWNX_Effect_PackEffect(u), oCaster);
    u.nSpellId = -1; u.sTag = "nospell";
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, NWNX_Effect_PackEffect(u), oVic);
    NWNX_Object_ClearSpellEffectsOnOthers(oCaster);
    string sLeft = "";
    effect e = GetFirstEffect(oVic);
    while (GetIsEffectValid(e)) { sLeft += " " + GetEffectTag(e); e = GetNextEffect(oVic); }
    PLog("clear.victim", sLeft);
    sLeft = "";
    e = GetFirstEffect(oCaster);
    while (GetIsEffectValid(e)) { sLeft += " " + GetEffectTag(e); e = GetNextEffect(oCaster); }
    PLog("clear.self", sLeft);

    // Last spell facts outside a spell.
    PLogI("lastspell.feat", NWNX_Object_GetLastSpellCastFeat(oGob));
    PLogI("lastspell.featchest", NWNX_Object_GetLastSpellCastFeat(oChest));
    PLogI("lastspell.spont", NWNX_Object_GetLastSpellCastSpontaneous(oGob));
    PLogI("lastspell.domain", NWNX_Object_GetLastSpellCastDomainLevel(oGob));
    PLogI("lastspell.instant", NWNX_Object_GetLastSpellInstant());

    // UUIDs.
    object oW1 = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", PCentre(), FALSE, "OBJ_W1");
    object oW2 = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", PCentre(), FALSE, "OBJ_W2");
    PLog("peek.none", NWNX_Object_PeekUUID(oW1));
    string sU = GetObjectUUID(oW1);
    PLogI("peek.after", NWNX_Object_PeekUUID(oW1) == sU);
    NWNX_Object_ForceAssignUUID(oW2, sU);
    PLogI("force.new", GetObjectUUID(oW2) == sU);
    PLogI("force.old", GetObjectUUID(oW1) == sU);
    PLog("force.byuuid", O(GetObjectByUUID(sU)));
    PLogI("force.oldvalid", GetStringLength(GetObjectUUID(oW1)) > 0);

    // Export, then create from the exported blueprint.
    SetLocalInt(oGob, "exported", 5);
    NWNX_Object_Export(oGob, "nwnxexp_gob");
    object oExp = CreateObject(OBJECT_TYPE_CREATURE, "nwnxexp_gob", PCentre(12.0));
    PLog("export.creature", O(oExp) + "/" + IntToString(GetLocalInt(oExp, "exported")) + "/" + GetResRef(oExp));
    object oPot = CreateItemOnObject("nw_it_mpotion001", oChest, 1, "OBJ_EXPPOT");
    SetName(oPot, "Exported potion");
    NWNX_Object_Export(oPot, "nwnxexp_pot");
    object oPot2 = CreateItemOnObject("nwnxexp_pot", oChest);
    PLog("export.item", O(oPot2) + "/" + GetName(oPot2));

    // Older functions.
    PLogI("type.gob", NWNX_Object_GetInternalObjectType(oGob));
    PLogI("type.door", NWNX_Object_GetInternalObjectType(oDoor));
    PLogI("type.trig", NWNX_Object_GetInternalObjectType(oTrig));
    PLogI("type.aoe", NWNX_Object_GetInternalObjectType(oAoe));
    PLogI("type.area", NWNX_Object_GetInternalObjectType(oArea));
    PLogI("type.mod", NWNX_Object_GetInternalObjectType(GetModule()));
    PLogI("type.invalid", NWNX_Object_GetInternalObjectType(OBJECT_INVALID));
    PLog("trig.geometry", NWNX_Object_GetTriggerGeometry(oTrig));
    PLogI("trig.inside", NWNX_Object_GetPositionIsInTrigger(oTrig, Vector(31.0, 31.0, 0.0)));
    PLogI("trig.outside", NWNX_Object_GetPositionIsInTrigger(oTrig, Vector(35.0, 31.0, 0.0)));
    NWNX_Object_SetTriggerGeometry(oTrig, "{20.0, 20.0}{22.0, 20.0}{22.0, 22.0}");
    PLog("trig.geometry2", NWNX_Object_GetTriggerGeometry(oTrig));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectTemporaryHitpoints(5), oGob);
    PLog("hp", IntToString(NWNX_Object_GetCurrentHitPoints(oGob)) + "/" + IntToString(GetCurrentHitPoints(oGob)));
    NWNX_Object_SetCurrentHitPoints(oGob, 2);
    PLog("hp.set", IntToString(NWNX_Object_GetCurrentHitPoints(oGob)) + "/" + IntToString(GetCurrentHitPoints(oGob)));
    NWNX_Object_SetMaxHitPoints(oChest, 40);
    PLogI("hp.chestmax", GetMaxHitPoints(oChest));
    PLog("dialog", NWNX_Object_GetDialogResref(oGob) + "/" + NWNX_Object_GetDialogResref(oChest));
    PLogI("appearance", NWNX_Object_GetAppearance(oChest));
    PLogI("static", NWNX_Object_GetPlaceableIsStatic(oChest));
    PLog("destroyable", IntToString(NWNX_Object_GetIsDestroyable(oGob)) + "/" + IntToString(NWNX_Object_GetIsDestroyable(oChest)) + "/" + IntToString(NWNX_Object_GetIsDestroyable(oWp)));
    PLogI("ailevel", NWNX_Object_GetAILevel(oGob));
    NWNX_Object_SetPosition(oWp, Vector(5.0, 6.0, 0.0));
    vector vW = GetPosition(oWp);
    PLog("setposition", FloatToString(vW.x, 0, 1) + "," + FloatToString(vW.y, 0, 1));
    NWNX_Object_SetMapNote(oWp, "note");
    PLog("mapnote", NWNX_Object_GetMapNote(oWp));

    // Projectile override, persisted.
    NWNX_Object_OverrideSpellProjectileVFX(oCaster, NWNX_OBJECT_SPELL_PROJECTILE_TYPE_USE_PATH, PROJECTILE_PATH_TYPE_HOMING, SPELL_MAGIC_MISSILE, TRUE);
    PRaw("projpersist", NWNX_Object_Serialize(oCaster));

    int nA;
    float fX = -18.0;
    for (nA = 0; nA <= 20; nA++) { Animated(nA, fX); fX += 1.5; }
    Animated(ANIMATION_FIREFORGET_BOW, fX); fX += 1.5;
    Animated(ANIMATION_FIREFORGET_SALUTE, fX); fX += 1.5;
    Animated(ANIMATION_FIREFORGET_DRINK, fX); fX += 1.5;
    Animated(ANIMATION_FIREFORGET_READ, fX); fX += 1.5;
    Animated(ANIMATION_FIREFORGET_GREETING, fX); fX += 1.5;
    Animated(ANIMATION_FIREFORGET_TAUNT, fX); fX += 1.5;
    Animated(ANIMATION_FIREFORGET_VICTORY1, fX); fX += 1.5;
    Animated(ANIMATION_FIREFORGET_PAUSE_BORED, fX); fX += 1.5;
    Animated(ANIMATION_FIREFORGET_HEAD_TURN_LEFT, fX); fX += 1.5;
    object oWalk = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(-15.0, -15.0), FALSE, "OBJ_WALK");
    SetAILevel(oWalk, AI_LEVEL_NORMAL);
    AssignCommand(oWalk, ActionMoveToLocation(PCentre(15.0, -15.0)));
    object oRun = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(-15.0, -17.0), FALSE, "OBJ_RUN");
    SetAILevel(oRun, AI_LEVEL_NORMAL);
    AssignCommand(oRun, ActionMoveToLocation(PCentre(15.0, -17.0), TRUE));
    object oDead = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(-15.0, 10.0), FALSE, "OBJ_DEAD");
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), oDead);

    DelayCommand(1.0, Later(oGob, oAoe));
}
