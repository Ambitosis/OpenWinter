// Persistent areas of effect (VFX_PER_*): web, grease and entangle ahead, darkness and stinking cloud behind.
#include "visual_inc"

void Aoe(object oPC, int nAoe, float fDx, float fDy)
{
    vector v = GetPosition(oPC);
    ApplyEffectAtLocation(DURATION_TYPE_PERMANENT, EffectAreaOfEffect(nAoe), Location(GetArea(oPC), Vector(v.x + fDx, v.y + fDy, v.z), 0.0));
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    Aoe(oPC, AOE_PER_WEB, -9.0, 9.0);
    Aoe(oPC, AOE_PER_GREASE, 0.0, 9.0);
    Aoe(oPC, AOE_PER_ENTANGLE, 9.0, 9.0);
    Aoe(oPC, AOE_PER_DARKNESS, -6.0, -9.0);
    Aoe(oPC, AOE_PER_FOGSTINK, 6.0, -9.0);
    DelayCommand(4.0, VCamera(oPC, 90.0, 18.0, 55.0));
    DelayCommand(7.0, VShot("a_web_grease_entangle"));
    DelayCommand(8.0, VCamera(oPC, 270.0, 18.0, 55.0));
    DelayCommand(10.0, VShot("b_darkness_stink"));
    DelayCommand(12.0, VDone());
}
