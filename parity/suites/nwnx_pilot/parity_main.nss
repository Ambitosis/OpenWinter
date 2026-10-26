// The NWNX side of the parity lab: NWNX:EE plugin calls on the Linux nwserver against OpenWinter.
#include "parity_inc"
#include "nwnx_object"
#include "nwnx_util"
#include "nwnx_creature"

void main()
{
    object oMod = GetModule();
    SetLocalInt(oMod, "a", 1);
    SetLocalString(oMod, "b", "x");
    PLogI("object.localcount", NWNX_Object_GetLocalVariableCount(oMod));
    struct NWNX_Object_LocalVariable v = NWNX_Object_GetLocalVariable(oMod, 0);
    PLog("object.local0", v.key + "/" + IntToString(v.type));
    PLog("util.script", NWNX_Util_GetCurrentScriptName());
    object oC = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre());
    PLogI("creature.featcount", NWNX_Creature_GetFeatCount(oC));
    NWNX_Creature_AddFeat(oC, FEAT_ALERTNESS);
    PLogI("creature.hasalert", GetHasFeat(FEAT_ALERTNESS, oC));
    PDone();
}
