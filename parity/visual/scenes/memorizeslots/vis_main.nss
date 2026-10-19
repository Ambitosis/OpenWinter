// The Memorize Spells window with empty slots: a ring of bonus cantrip slots (ItemPropertyBonusLevelSpell)
// leaves slots nothing is memorized in, and B opens the spellbook with the memorize panel beside it.
#include "visual_inc"
void Tap(int key) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(key) + "|tap"); }
void Note(object pc, string tag)
{
    WriteTimestampedLogEntry("VISUAL|note|state|" + tag + "|class0=" + IntToString(GetClassByPosition(1, pc))
        + "|level=" + IntToString(GetHitDice(pc)) + "|ring=" + IntToString(GetIsObjectValid(GetItemInSlot(INVENTORY_SLOT_LEFTRING, pc))));
}
void Ring(object pc)
{
    object ring = CreateItemOnObject("nw_it_mring001", pc);
    int cls = GetClassByPosition(1, pc) == CLASS_TYPE_SORCERER ? IP_CONST_CLASS_SORCERER : IP_CONST_CLASS_WIZARD;
    int i;
    for (i = 0; i < 3; i++)
    {
        AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyBonusLevelSpell(cls, IP_CONST_SPELLLEVEL_0), ring);
        AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyBonusLevelSpell(cls, IP_CONST_SPELLLEVEL_1), ring);
    }
    SetIdentified(ring, TRUE);
    AssignCommand(pc, ActionEquipItem(ring, INVENTORY_SLOT_LEFTRING));
}
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0, VCamera(pc, 90.0, 8.0, 60.0));
    DelayCommand(1.5, Ring(pc));
    DelayCommand(4.0, Note(pc, "equipped"));
    DelayCommand(5.0, Tap(66));
    DelayCommand(7.0, VShot("01_memorize_empty"));
    DelayCommand(9.0, Tap(66));
    DelayCommand(10.0, VShot("02_closed"));
    DelayCommand(11.0, VDone());
}
