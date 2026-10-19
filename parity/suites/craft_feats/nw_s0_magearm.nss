// Stands in for the spell's impact script: runs the stock x2_pc_craft (x2_inc_spellhook skips it for non-players,
// so the suite calls it directly) and logs what it returned. The stock crafting scripts run unchanged.
#include "parity_inc"
#include "x2_inc_switches"
void main()
{
    object t = GetSpellTargetObject();
    int r = ExecuteScriptAndReturnInt("x2_pc_craft", OBJECT_SELF);
    string k = GetLocalString(OBJECT_SELF, "case");
    PLog(k + ".hook", IntToString(r) + " target=" + GetResRef(t) + " base=" + IntToString(GetBaseItemType(t))
        + " spell=" + IntToString(GetSpellId()) + " class=" + IntToString(GetLastSpellCastClass())
        + " castitem=" + IntToString(GetIsObjectValid(GetSpellCastItem())) + " self=" + IntToString(GetTag(OBJECT_SELF) != ""));
}
