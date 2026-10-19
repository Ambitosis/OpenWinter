// The player polymorphed (Rakshasa: three form abilities, a staff and a hide): the model swap, the quickbar,
// inventory, character sheet, spell book and own radial while polymorphed (first run; this second run
// looks at the inventory before, the radial's Cast Spell wedge and the ending), script casts of a form ability and
// an own spell, then Cancel Polymorph from the radial and a second, short form that runs out.
// Docs: docs/POLYMORPH.md.
#include "visual_inc"
void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }
void Tap(int nVk) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(nVk) + "|tap"); }
string Slots(object o)
{
    string s = "";
    int n;
    for (n = 0; n < 18; n++) { object it = GetItemInSlot(n, o); if (GetIsObjectValid(it)) s += " " + IntToString(n) + "=" + GetResRef(it); }
    return s;
}
string FxList(object o)
{
    string s = "";
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { s += " " + IntToString(GetEffectType(e)) + "/" + IntToString(GetEffectSpellId(e)); e = GetNextEffect(o); }
    return s;
}
void State(object pc, string k)
{
    Note(k + "|app=" + IntToString(GetAppearanceType(pc)) + "|sound=" + IntToString(GetSoundset(pc)) + "|portrait=" + GetPortraitResRef(pc)
        + "|hp=" + IntToString(GetCurrentHitPoints(pc)) + "/" + IntToString(GetMaxHitPoints(pc))
        + "|s41=" + IntToString(GetHasSpell(41, pc)) + "|s368=" + IntToString(GetHasSpell(368, pc)) + "|s523=" + IntToString(GetHasSpell(523, pc))
        + "|own102=" + IntToString(GetHasSpell(102, pc)) + "|own107=" + IntToString(GetHasSpell(107, pc))
        + "|str=" + IntToString(GetAbilityScore(pc, ABILITY_STRENGTH)) + "|ac=" + IntToString(GetAC(pc))
        + "|slots=" + Slots(pc) + "|fx=" + FxList(pc) + "|action=" + IntToString(GetCurrentAction(pc)));
}
void Poly(object pc, int nPoly, float fDur)
{
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectVisualEffect(VFX_IMP_POLYMORPH), pc);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectPolymorph(nPoly), pc, fDur);
}
void CastAt(object pc, int nSpell) { AssignCommand(pc, ClearAllActions()); AssignCommand(pc, ActionCastSpellAtObject(nSpell, pc, METAMAGIC_ANY, FALSE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE)); }
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    Note("character-fixture|" + GetName(pc) + "|pc=" + IntToString(GetIsPC(pc)));
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0, VCamera(pc, 90.0, 9.0, 60.0));
    DelayCommand(3.0, State(pc, "before"));
    DelayCommand(3.2, VShot("a_before"));
    DelayCommand(4.0, Tap(73));
    DelayCommand(5.5, VShot("a_inventory"));
    DelayCommand(6.0, Tap(73));
    DelayCommand(7.0, Poly(pc, 88, 120.0));
    DelayCommand(7.0, WriteTimestampedLogEntry("VISUAL|burst|b_swap|1.5|6"));
    DelayCommand(7.1, State(pc, "during"));
    DelayCommand(10.0, State(pc, "during3"));
    DelayCommand(10.2, VShot("c_during"));
    // Her own radial: the Cast Spell wedge (numpad 9) while polymorphed.
    DelayCommand(11.0, Tap(96));
    DelayCommand(11.6, Tap(105));
    DelayCommand(12.6, VShot("g_radial_cast"));
    DelayCommand(13.0, Tap(101));
    DelayCommand(13.5, Tap(101));
    DelayCommand(14.0, VShot("g_radial_closed"));
    // F4 held her Mage Armor.
    DelayCommand(15.0, Tap(115));
    DelayCommand(16.0, VShot("i_f4"));
    DelayCommand(16.5, State(pc, "afterF4"));
    // Script casts: a form ability (Dispel Magic, 41) and her own Mage Armor (102).
    DelayCommand(17.0, CastAt(pc, 41));
    DelayCommand(20.0, State(pc, "cast41"));
    DelayCommand(20.5, CastAt(pc, 102));
    DelayCommand(24.0, State(pc, "cast102"));
    DelayCommand(24.2, VShot("j_casts"));
    // Cancel Polymorph from her own radial (numpad 0, then numpad 2 for the south wedge).
    DelayCommand(25.0, Tap(96));
    DelayCommand(26.0, VShot("k_radial_cancel"));
    DelayCommand(26.5, Tap(98));
    DelayCommand(26.6, WriteTimestampedLogEntry("VISUAL|burst|l_cancel|2.0|5"));
    DelayCommand(27.0, State(pc, "cancel05"));
    DelayCommand(29.0, State(pc, "cancel25"));
    DelayCommand(29.2, VShot("m_cancelled"));
    // A short form that runs out (a water elemental: one ability and its own sound set).
    DelayCommand(31.0, Poly(pc, POLYMORPH_TYPE_HUGE_WATER_ELEMENTAL, 6.0));
    DelayCommand(33.0, State(pc, "water"));
    DelayCommand(33.2, VShot("n_water"));
    DelayCommand(36.8, WriteTimestampedLogEntry("VISUAL|burst|o_expire|2.0|5"));
    DelayCommand(37.2, State(pc, "water62"));
    DelayCommand(39.5, State(pc, "waterafter"));
    DelayCommand(39.7, VShot("p_after"));
    DelayCommand(41.0, VDone());
}
