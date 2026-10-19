// Text-only creature labels: reaction colours, health boundaries, self label, and live changes.
#include "visual_inc"
void Key(string state) { WriteTimestampedLogEntry("VISUAL|key|9|" + state); }
void HP(object o, int hp) {
    SetCurrentHitPoints(o, hp);
    WriteTimestampedLogEntry("VISUAL|note|HP|" + GetName(o) + "|" + IntToString(GetCurrentHitPoints(o)) + "/" + IntToString(GetMaxHitPoints(o)));
}
void ClearTemp(object o) {
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) {
        if (GetEffectType(e) == EFFECT_TYPE_TEMPORARY_HITPOINTS) RemoveEffect(o, e);
        e = GetNextEffect(o);
    }
}
void Relations(object a, object pc) {
    WriteTimestampedLogEntry("VISUAL|note|reaction|rep=" + IntToString(GetReputation(a, pc))
        + "|reverse=" + IntToString(GetReputation(pc, a)) + "|friend=" + IntToString(GetIsFriend(a, pc))
        + "|same=" + IntToString(GetFactionEqual(a, pc)));
}
void main() {
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    object a = VSpawn(pc, "vis_label", -2.3, 2.0, 270.0);
    object b = VSpawn(pc, "vis_label", 0.0, 2.0, 270.0);
    object c = VSpawn(pc, "vis_label", 2.3, 2.0, 270.0);
    SetName(a, "Friendly"); ChangeToStandardFaction(a, STANDARD_FACTION_DEFENDER);
    DelayCommand(3.5, AdjustReputation(pc, a, 100));
    DelayCommand(3.5, AdjustReputation(a, pc, 100));
    SetName(b, "Neutral");
    SetName(c, "Hostile"); ChangeToStandardFaction(c, STANDARD_FACTION_HOSTILE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneParalyze(), a);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneParalyze(), b);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneParalyze(), c);
    DelayCommand(3.0, VCamera(pc, 90.0, 8.0, 60.0));
    DelayCommand(4.0, Key("down"));
    DelayCommand(5.0, Relations(a, pc));
    DelayCommand(6.0, VShot("a_full"));
    DelayCommand(8.0, HP(a, 99)); DelayCommand(8.0, HP(b, 76)); DelayCommand(8.0, HP(c, 75));
    DelayCommand(10.0, VShot("b_99_76_75"));
    DelayCommand(12.0, HP(a, 74)); DelayCommand(12.0, HP(b, 51)); DelayCommand(12.0, HP(c, 50));
    DelayCommand(14.0, VShot("c_74_51_50"));
    DelayCommand(16.0, HP(a, 49)); DelayCommand(16.0, HP(b, 26)); DelayCommand(16.0, HP(c, 25));
    DelayCommand(18.0, VShot("d_49_26_25"));
    DelayCommand(20.0, HP(a, 24)); DelayCommand(20.0, HP(b, 10)); DelayCommand(20.0, HP(c, 1));
    DelayCommand(22.0, VShot("e_24_10_1"));
    DelayCommand(24.0, HP(a, 100));
    DelayCommand(24.0, ChangeToStandardFaction(b, STANDARD_FACTION_HOSTILE));
    DelayCommand(24.0, ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectTemporaryHitpoints(100), c));
    DelayCommand(26.0, VShot("f_heal_reaction_temp"));
    DelayCommand(28.0, HP(a, 96)); DelayCommand(28.0, HP(b, 95));
    DelayCommand(28.0, ClearTemp(c));
    DelayCommand(28.0, HP(c, 94));
    DelayCommand(30.0, VShot("g_96_95_94"));
    DelayCommand(31.0, AssignCommand(b, SetIsDestroyable(FALSE, TRUE, TRUE)));
    DelayCommand(31.0, SetPlotFlag(b, FALSE));
    DelayCommand(32.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), b));
    DelayCommand(34.0, VShot("h_dead"));
    DelayCommand(36.0, Key("up"));
    DelayCommand(38.0, VShot("i_released"));
    DelayCommand(40.0, AddHenchman(pc, a));
    DelayCommand(41.0, Key("down"));
    DelayCommand(42.0, Relations(a, pc));
    DelayCommand(44.0, VShot("j_party"));
    DelayCommand(46.0, VCamera(pc, 45.0, 8.0, 60.0));
    DelayCommand(46.2, Key("up"));
    DelayCommand(46.5, Key("down"));
    DelayCommand(48.0, VShot("k_angled"));
    DelayCommand(50.0, SetCreatureAppearanceType(c, APPEARANCE_TYPE_RAT));
    DelayCommand(50.0, SetName(c, "Rat"));
    DelayCommand(50.0, VCamera(pc, 90.0, 8.0, 60.0));
    DelayCommand(50.2, Key("up"));
    DelayCommand(50.5, Key("down"));
    DelayCommand(52.0, VShot("l_rat"));
    DelayCommand(53.0, Key("up"));
    DelayCommand(55.0, VDone());
}
