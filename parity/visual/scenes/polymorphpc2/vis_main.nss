// Polymorph, second scene: whether the engine itself shows a visual when a form starts and ends (no scripted
// VFX here: the player and a bandit are polymorphed and changed back), the radial's Cast Spell wedge before and
// while polymorphed, the character file ExportSingleCharacter writes while polymorphed, and a form ability
// (the mind flayer's Psionic Inertial Barrier) cast twice from the radial.
// Docs: docs/POLYMORPH.md.
#include "visual_inc"
void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }
void Tap(int nVk) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(nVk) + "|tap"); }
void Unpoly(object o)
{
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { if (GetEffectType(e) == EFFECT_TYPE_POLYMORPH) RemoveEffect(o, e); e = GetNextEffect(o); }
}
string FxList(object o)
{
    string s = "";
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { s += " " + IntToString(GetEffectType(e)) + "/" + IntToString(GetEffectSpellId(e)); e = GetNextEffect(o); }
    return s;
}
void State(object o, string k)
{
    Note(k + "|app=" + IntToString(GetAppearanceType(o)) + "|hp=" + IntToString(GetCurrentHitPoints(o)) + "/" + IntToString(GetMaxHitPoints(o))
        + "|s693=" + IntToString(GetHasSpell(693, o)) + "|s741=" + IntToString(GetHasSpell(741, o)) + "|fx=" + FxList(o));
}
// Her own radial, Cast Spell (numpad 9), then its east wedge: the form's second ability (Psionic Inertial Barrier).
void RadialCast(string k, float t)
{
    DelayCommand(t, Tap(96));
    DelayCommand(t + 0.6, Tap(105));
    DelayCommand(t + 1.6, VShot(k));
    DelayCommand(t + 2.0, Tap(102));
}
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    Note("character-fixture|" + GetName(pc) + "|pc=" + IntToString(GetIsPC(pc)));
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0, VCamera(pc, 90.0, 9.0, 60.0));
    object b = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(pc), GetPosition(pc) + Vector(2.5, 0.0, 0.0), 180.0));
    SetPlotFlag(b, TRUE);
    ChangeToStandardFaction(b, STANDARD_FACTION_COMMONER);
    // Her Cast Spell wedge before polymorphing, and its second wedge (east).
    DelayCommand(3.0, Tap(96));
    DelayCommand(3.6, Tap(105));
    DelayCommand(4.6, VShot("a_cast_before"));
    DelayCommand(5.0, Tap(102));
    DelayCommand(6.0, VShot("a_cast_before_e"));
    DelayCommand(6.5, Tap(101));
    DelayCommand(6.9, Tap(101));
    DelayCommand(7.3, Tap(101));
    // Polymorph without any scripted visual: the player and the bandit together.
    DelayCommand(9.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectPolymorph(POLYMORPH_TYPE_BROWN_BEAR), pc, 60.0));
    DelayCommand(9.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectPolymorph(POLYMORPH_TYPE_WEREWOLF), b, 60.0));
    DelayCommand(9.0, WriteTimestampedLogEntry("VISUAL|burst|b_start|1.5|5"));
    DelayCommand(11.0, State(pc, "pc.during"));
    DelayCommand(11.0, State(b, "bandit.during"));
    DelayCommand(11.5, VShot("c_during"));
    // The Cast Spell wedge while polymorphed, and its east wedge.
    DelayCommand(12.5, Tap(96));
    DelayCommand(13.1, Tap(105));
    DelayCommand(14.1, VShot("d_cast_during"));
    DelayCommand(14.5, Tap(102));
    DelayCommand(15.5, VShot("d_cast_during_e"));
    DelayCommand(16.0, Tap(101));
    DelayCommand(16.4, Tap(101));
    DelayCommand(16.8, Tap(101));
    // Her character file while polymorphed.
    DelayCommand(18.0, ExportSingleCharacter(pc));
    DelayCommand(18.1, Note("exported"));
    // Both changed back by RemoveEffect, without any scripted visual.
    DelayCommand(21.0, Unpoly(pc));
    DelayCommand(21.0, Unpoly(b));
    DelayCommand(21.0, WriteTimestampedLogEntry("VISUAL|burst|e_end|2.0|5"));
    DelayCommand(21.2, State(pc, "pc.t02"));
    DelayCommand(21.8, State(pc, "pc.t08"));
    DelayCommand(24.0, State(pc, "pc.after"));
    DelayCommand(24.0, State(b, "bandit.after"));
    DelayCommand(24.2, VShot("f_after"));
    // A mind flayer: its second ability cast twice from her radial.
    DelayCommand(26.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectPolymorph(68), pc, 60.0));
    DelayCommand(28.0, State(pc, "flayer.before"));
    RadialCast("g_flayer_cast", 28.5);
    DelayCommand(34.5, State(pc, "flayer.one"));
    DelayCommand(34.6, VShot("h_flayer_one"));
    RadialCast("i_flayer_cast2", 35.5);
    DelayCommand(41.5, State(pc, "flayer.two"));
    DelayCommand(41.6, VShot("j_flayer_two"));
    DelayCommand(43.0, VDone());
}
