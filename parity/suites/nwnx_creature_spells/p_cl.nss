// Impact script for the probed spells: what the cast looks like from inside.
#include "parity_inc"
#include "nwnx_creature"

void main()
{
    string k = "cast." + GetTag(OBJECT_SELF);
    object oItem = GetSpellCastItem();
    PLog(k, "cl=" + IntToString(GetCasterLevel(OBJECT_SELF)) + " spell=" + IntToString(GetSpellId())
        + " class=" + IntToString(GetLastSpellCastClass()) + " item=" + (GetIsObjectValid(oItem) ? GetTag(oItem) : "none")
        + " target=" + (GetSpellTargetObject() == OBJECT_SELF ? "self" : GetIsObjectValid(GetSpellTargetObject()) ? GetTag(GetSpellTargetObject()) : "none")
        + " meta=" + IntToString(GetMetaMagicFeat()) + " lastitemcl=" + IntToString(NWNX_Creature_GetLastItemCasterLevel(OBJECT_SELF)));
}
