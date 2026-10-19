#include "visual_inc"

object Armored(object pc, float x, int vfx)
{
    object actor = VSpawn(pc, "nw_bandit001", x, 1.0, 270.0);
    SetEventScript(actor, EVENT_SCRIPT_CREATURE_ON_HEARTBEAT, "");
    object item = GetFirstItemInInventory(actor);
    while (GetIsObjectValid(item))
    {
        DestroyObject(item);
        item = GetNextItemInInventory(actor);
    }
    WriteTimestampedLogEntry("VISUAL|note|stock." + IntToString(vfx) + ".model."
        + Get2DAString("visualeffects", "Imp_Root_M_Node", vfx));
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectVisualEffect(vfx), actor, 10.0);
    return actor;
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc); VHidePC(pc);
    Armored(pc, -1.8, 495);
    Armored(pc, 1.8, 497);
    DelayCommand(1.0, VCamera(pc, 90.0, 9.0, 65.0));
    DelayCommand(3.0, VShot("stock_dark"));
    DelayCommand(5.0, SetAreaLightColor(AREA_LIGHT_COLOR_SUN_AMBIENT, 8421504, GetArea(pc), 0.0));
    DelayCommand(7.0, VShot("stock_ambient"));
    DelayCommand(11.0, VShot("stock_expired"));
    DelayCommand(12.0, VDone());
}
