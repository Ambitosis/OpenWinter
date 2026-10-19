// Spell context logging: overrides of spell impact scripts call PSpellContext to report what the
// engine hands the script (save DC, caster level, metamagic, target, class, ...).
#include "parity_inc"

void PSpellContext(string sSpell)
{
    object oTarget = GetSpellTargetObject();
    location lTarget = GetSpellTargetLocation();
    // Keyed by the phase the main script is in and a count per spell, so queue order doesn't matter.
    string k = "ctx." + sSpell + "." + GetTag(OBJECT_SELF) + "." + GetLocalString(GetModule(), "phase");
    int n = GetLocalInt(OBJECT_SELF, k);
    SetLocalInt(OBJECT_SELF, k, n + 1);
    k += "." + IntToString(n);
    PLog(k, "dc=" + IntToString(GetSpellSaveDC())
        + " cl=" + IntToString(GetCasterLevel(OBJECT_SELF))
        + " meta=" + IntToString(GetMetaMagicFeat())
        + " id=" + IntToString(GetSpellId())
        + " class=" + IntToString(GetLastSpellCastClass())
        + " level=" + IntToString(GetLastSpellLevel())
        + " feat=" + IntToString(GetSpellFeatId())
        + " spont=" + IntToString(GetSpellCastSpontaneously())
        + " item=" + IntToString(GetIsObjectValid(GetSpellCastItem()))
        + " target=" + (oTarget == OBJECT_SELF ? "self" : GetIsObjectValid(oTarget) ? GetTag(oTarget) : "none")
        + " loc=" + FloatToString(GetDistanceBetweenLocations(lTarget, GetLocation(OBJECT_SELF)), 0, 0)
        + " area=" + IntToString(GetAreaFromLocation(lTarget) == GetArea(OBJECT_SELF)));
}

// ResistSpell from inside a spell script against the module's "srtarget", as a rate over 200 checks.
void PSpellResist(string sSpell)
{
    object oT = GetLocalObject(GetModule(), "srtarget");
    if (!GetIsObjectValid(oT)) return;
    int i, n = 0;
    for (i = 0; i < 200; i++) if (ResistSpell(OBJECT_SELF, oT) == 1) n++;
    // sd 0.035 per engine.
    PLogF("resist." + sSpell + "." + GetTag(OBJECT_SELF) + "." + GetLocalString(GetModule(), "phase"), IntToFloat(n) / 200.0, 0.13);
}
