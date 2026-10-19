#include "visual_inc"
// difficultypc: does the game difficulty change the damage a player deals ("PC does a minimum of 50% of damage" in
// the Easy description)? Run once per difficulty (visual.py --difficulty N); chat.txt has every attack and damage line.
// She hits a hostile goblin (AC lowered by 20, 3000 temporary hit points, immune to critical hits, no scripts), six
// attacks a round (EffectModifyAttacks): with a dagger (1d4) for 12 s, a club (1d6) for 12 s, then unarmed (1d3) for 12 s. Also shows
// whether the goblin fights back and takes attacks of opportunity, and its AC (notes).

void Mark(string s) { SendMessageToPC(GetFirstPC(), "DIFF " + s); }

void Equip(object pc, string sResRef, int nSlot, int nStack = 1)
{
    object o = CreateItemOnObject(sResRef, pc, nStack);
    SetIdentified(o, TRUE);
    AssignCommand(pc, ActionEquipItem(o, nSlot));
}

void Dagger(object pc, object t)
{
    Mark("dagger");
    AssignCommand(pc, ActionAttack(t));
}

void Club(object pc, object t)
{
    AssignCommand(pc, ClearAllActions(TRUE));
    Mark("club");
    object w = GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, pc);
    if (GetIsObjectValid(w)) DestroyObject(w);
    Equip(pc, "nw_wblcl001", INVENTORY_SLOT_RIGHTHAND);
    AssignCommand(pc, ActionAttack(t));
}

void Unarmed(object pc, object t)
{
    AssignCommand(pc, ClearAllActions(TRUE));
    Mark("unarmed");
    object w = GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, pc);
    if (GetIsObjectValid(w)) DestroyObject(w);
    DelayCommand(0.3, AssignCommand(pc, ActionAttack(t)));
}

void End(object pc)
{
    AssignCommand(pc, ClearAllActions(TRUE));
    Mark("end");
    VDone();
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    DelayCommand(1.0, VCamera(pc, 270.0, 18.0, 40.0));
    SetPlotFlag(pc, TRUE);
    vector v = GetPosition(pc);
    object t = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", Location(GetArea(pc), Vector(v.x + 1.5, v.y, v.z), 180.0), FALSE, "diff_dummy");
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(t, ev, "");
    AssignCommand(t, ClearAllActions(TRUE));
    ChangeToStandardFaction(t, STANDARD_FACTION_HOSTILE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectTemporaryHitpoints(3000), t);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACDecrease(20), t);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectImmunity(IMMUNITY_TYPE_CRITICAL_HIT), t);
    SetImmortal(t, TRUE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectModifyAttacks(5), pc);
    object w = GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, pc);
    if (GetIsObjectValid(w)) DestroyObject(w);
    Equip(pc, "nw_wswdg001", INVENTORY_SLOT_RIGHTHAND);
    DelayCommand(1.5, WriteTimestampedLogEntry("VISUAL|note|goblin.ac." + IntToString(GetAC(t)) + ".pc.weapon." + GetTag(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, pc))));
    DelayCommand(2.0, Dagger(pc, t));
    DelayCommand(14.0, Club(pc, t));
    DelayCommand(26.0, Unarmed(pc, t));
    DelayCommand(38.0, End(pc));
}
