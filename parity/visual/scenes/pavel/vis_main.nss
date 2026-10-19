// Pavel in the Prelude's senior barracks (m0q1a), as the module places him: the academy uniform and a torch
// in his off hand. Views from his front, side and back; then his uniform copied onto a human, an elf (female) and
// a dwarf, to see which texture each race's part models get.
#include "visual_inc"

void Report(object o)
{
    object oWorn = GetItemInSlot(INVENTORY_SLOT_CHEST, o);
    WriteTimestampedLogEntry("VISUAL|note|" + GetName(o) + " wears " + GetTag(oWorn) + " torso "
        + IntToString(GetItemAppearance(oWorn, ITEM_APPR_TYPE_ARMOR_MODEL, ITEM_APPR_ARMOR_MODEL_TORSO)));
}

// Part-based looks (these blueprints use whole-body NPC models), and plain clothes remade with the uniform's torso and colours, worn at once.
void Dress(object o, int nAppearance)
{
    SetCreatureAppearanceType(o, nAppearance);
    ChangeToStandardFaction(o, STANDARD_FACTION_MERCHANT);
    SetPlotFlag(o, TRUE);
    object oArmour = CreateItemOnObject("nw_cloth001", o);
    object oNew = CopyItemAndModify(oArmour, ITEM_APPR_TYPE_ARMOR_MODEL, ITEM_APPR_ARMOR_MODEL_TORSO, 12, TRUE);
    oNew = CopyItemAndModify(oNew, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_CLOTH1, 25, TRUE);
    oNew = CopyItemAndModify(oNew, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_CLOTH2, 20, TRUE);
    oNew = CopyItemAndModify(oNew, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_LEATHER1, 3, TRUE);
    oNew = CopyItemAndModify(oNew, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_LEATHER2, 23, TRUE);
    oNew = CopyItemAndModify(oNew, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_METAL1, 0, TRUE);
    oNew = CopyItemAndModify(oNew, ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_METAL2, 8, TRUE);
    if (!GetIsObjectValid(oNew)) WriteTimestampedLogEntry("VISUAL|note|uniform copy failed on " + GetName(o));
    AssignCommand(o, ActionEquipItem(oNew, INVENTORY_SLOT_CHEST));
    DelayCommand(3.0, Report(o));
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    // Pavel stands at (18.1, 8.9) facing south-west; the camera's anchor (the hidden player) stands beside him.
    object oPavel = GetObjectByTag("M1Q0APavel");
    AssignCommand(oPC, ClearAllActions(TRUE));
    AssignCommand(oPC, JumpToLocation(Location(GetArea(oPC), GetPosition(oPavel) + Vector(-0.8, 0.0, 0.0), 0.0)));
    DelayCommand(4.0, VCamera(oPC, 35.0, 4.0, 80.0));
    DelayCommand(6.0, VShot("a_front"));
    DelayCommand(8.0, VCamera(oPC, 125.0, 4.0, 80.0));
    DelayCommand(10.0, VShot("b_side"));
    DelayCommand(12.0, VCamera(oPC, 215.0, 4.0, 80.0));
    DelayCommand(14.0, VShot("c_back"));
    DelayCommand(16.0, VCamera(oPC, 35.0, 9.0, 65.0));
    DelayCommand(18.0, VShot("d_far"));
    // Beside Pavel, in the open: the same torso on a human, an elf (female) and a dwarf.
    vector v = GetPosition(oPavel);
    object oHuman = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(oPC), v + Vector(-3.3, 2.5, 0.0), 270.0));
    object oElf = CreateObject(OBJECT_TYPE_CREATURE, "nw_elfmage001", Location(GetArea(oPC), v + Vector(-1.8, 2.5, 0.0), 270.0));
    object oDwarf = CreateObject(OBJECT_TYPE_CREATURE, "nw_dwarfmerc001", Location(GetArea(oPC), v + Vector(-0.3, 2.5, 0.0), 270.0));
    Dress(oHuman, APPEARANCE_TYPE_HUMAN);
    Dress(oElf, APPEARANCE_TYPE_ELF);
    Dress(oDwarf, APPEARANCE_TYPE_DWARF);
    DelayCommand(21.0, VCamera(oPC, 90.0, 5.0, 78.0));
    DelayCommand(23.0, VShot("e_races"));
    DelayCommand(25.0, VDone());
}
