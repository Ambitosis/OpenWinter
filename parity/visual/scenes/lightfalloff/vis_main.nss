// How a point light falls off over the floor. The area has no light of its own; the floor is shot from straight
// above under a uniform grey ambient (128, 64, 16 and 8: the reference for the floor's texture and the display
// curve), dark, then lit by one authored white light at a time (the scene's models replace stock effect models):
// radius 4 m, 8 m, 4 m at multiplier 2, 2 m, 4 m from 2.5 m up, and two 4 m lights 3 m apart. Each light hangs 1 m
// above an invisible object at the player's feet.
#include "visual_inc"

void Ambient(int nColor) { SetAreaLightColor(AREA_LIGHT_COLOR_SUN_AMBIENT, nColor, OBJECT_INVALID); }

// Each light on its own invisible object, destroyed after its shot: a lasting effect can outlive its duration on a
// placeable in the original, and a second one on the same object did not show.
void LitOn(location lAt, int nVfx, string sTag)
{
    object oCarrier = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_invisobj", lAt, FALSE, sTag);
    WriteTimestampedLogEntry("VISUAL|note|carrier|" + sTag + "|" + IntToString(GetIsObjectValid(oCarrier)));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectVisualEffect(nVfx), oCarrier);
}

void LitOff(string sTag) { DestroyObject(GetObjectByTag(sTag)); }

void Lit(object oPC, float t, int nVfx, string sName)
{
    DelayCommand(t, LitOn(GetLocation(oPC), nVfx, "lf_" + sName));
    DelayCommand(t + 2.5, VShot(sName));
    DelayCommand(t + 3.5, LitOff("lf_" + sName));
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    DelayCommand(1.0, VCamera(oPC, 90.0, 14.0, 1.0));
    DelayCommand(2.0, Ambient(0x808080)); DelayCommand(4.0, VShot("amb128"));
    DelayCommand(4.5, Ambient(0x404040)); DelayCommand(6.5, VShot("amb64"));
    DelayCommand(7.0, Ambient(0x101010)); DelayCommand(9.0, VShot("amb16"));
    DelayCommand(9.5, Ambient(0x080808)); DelayCommand(11.5, VShot("amb8"));
    DelayCommand(12.0, Ambient(0));       DelayCommand(14.0, VShot("dark"));
    float t = 14.5;
    Lit(oPC, t, VFX_DUR_SPELLTURNING, "r4");          t += 4.5;
    Lit(oPC, t, VFX_DUR_FREEDOM_OF_MOVEMENT, "r8");   t += 4.5;
    Lit(oPC, t, VFX_DUR_SANCTUARY, "r4m2");           t += 4.5;
    Lit(oPC, t, VFX_DUR_ELEMENTAL_SHIELD, "r2");      t += 4.5;
    Lit(oPC, t, VFX_DUR_GLOBE_MINOR, "r4h25");        t += 4.5;
    Lit(oPC, t, VFX_DUR_PROTECTION_ELEMENTS, "pair"); t += 4.5;
    DelayCommand(t, VDone());
}
