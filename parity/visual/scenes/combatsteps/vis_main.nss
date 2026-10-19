#include "visual_inc"
// When attackers take their small sideways steps in melee: a bandit attacks a standing dummy (with one attack a
// round, then two, then unarmed), the dummy logging each attack and both positions logged every tenth of a second.
object Foe() { return GetObjectByTag("probe_foe"); }
object Dummy() { return GetObjectByTag("probe_dummy"); }
void Log(string s) { WriteTimestampedLogEntry("VISUAL|" + s); }
void Phase(string p) { SetLocalString(GetModule(), "phase", p); Log("note|combatsteps|phase|" + p); }
void Pos() {
 vector a = GetPosition(Foe()); vector b = GetPosition(Dummy());
 Log("note|combatsteps|pos|" + GetLocalString(GetModule(), "phase") + "|foe=" + FloatToString(a.x, 0, 3) + "," + FloatToString(a.y, 0, 3) + "," + FloatToString(GetFacing(Foe()), 0, 1)
  + "|dummy=" + FloatToString(b.x, 0, 3) + "," + FloatToString(b.y, 0, 3) + "|action=" + IntToString(GetCurrentAction(Foe())));
 DelayCommand(0.1, Pos());
}
void Strip(object o) { object r = GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, o); if (GetIsObjectValid(r)) DestroyObject(r); }
void Spawn(string weapon, int extra) {
 object d = Dummy(); vector p = GetPosition(d);
 object foe = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(d), p + Vector(1.6, 0.0, 0.0), 180.0), FALSE, "probe_foe");
 ChangeToStandardFaction(foe, STANDARD_FACTION_HOSTILE); SetImmortal(foe, TRUE);
 Strip(foe);
 if (weapon != "") { object w = CreateItemOnObject(weapon, foe); AssignCommand(foe, ActionEquipItem(w, INVENTORY_SLOT_RIGHTHAND)); }
 if (extra) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectModifyAttacks(1), foe);
}
void Fight() { AssignCommand(Foe(), ActionAttack(Dummy())); }
void End() { object foe = Foe(); AssignCommand(foe, ClearAllActions(TRUE)); SetImmortal(foe, FALSE); DestroyObject(foe); }
void Run(string phase, string weapon, int extra, float t) {
 DelayCommand(t, Phase(phase)); DelayCommand(t, Spawn(weapon, extra)); DelayCommand(t + 1.5, Fight()); DelayCommand(t + 29.0, End());
}
void main() {
 object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
 object d = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(pc), Vector(20.0, 20.0, 0.0), 0.0), FALSE, "probe_dummy"); Strip(d);
 ChangeToStandardFaction(d, STANDARD_FACTION_COMMONER); SetImmortal(d, TRUE); SetCommandable(FALSE, d);
 SetEventScript(d, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "cs_attacked");
 DelayCommand(1.0, Pos());
 float t = 2.0;
 Run("sword_one_attack", "nw_wswls001", FALSE, t); t += 30.0;
 Run("sword_two_attacks", "nw_wswls001", TRUE, t); t += 30.0;
 Run("unarmed_one_attack", "", FALSE, t); t += 30.0;
 DelayCommand(t, VDone());
}
