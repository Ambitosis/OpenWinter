// Hak-added armour art in the original: the scene's hak carries part icons cloned under new numbers (chest 300 and
// 1000, pelvis 300, and the candidate names chest100 / chest000 a four-digit part might be looked up as), longer
// parts tables, a parts_robe.2da row 50 hiding only the chest (robe 50's icon is robe 30's), and base item 200, a
// copy of armour. Each built piece is examined on its own and the inventory shot.
#include "visual_inc"

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }
void Tap(int nVk) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(nVk) + "|tap"); }
void Examine(object pc, object o) { AssignCommand(pc, ClearAllActions()); AssignCommand(pc, ActionExamine(o)); }

object Part(object o, int nPart, int nModel)
{
    object n = CopyItemAndModify(o, ITEM_APPR_TYPE_ARMOR_MODEL, nPart, nModel, TRUE);
    if (!GetIsObjectValid(n)) { Note("modfail|" + IntToString(nPart) + "|" + IntToString(nModel)); return o; }
    DestroyObject(o);
    return n;
}

void Describe(object o, string label)
{
    string s = label + "|" + GetResRef(o) + "|base=" + IntToString(GetBaseItemType(o)) + "|parts=";
    int i;
    for (i = 0; i <= 18; i++) s += IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_ARMOR_MODEL, i)) + ",";
    Note("item|" + s);
}

void Build(object pc)
{
    object stage = GetObjectByTag("armour_stage");
    SetLocalObject(pc, "h1", Part(CreateItemOnObject("nw_aarcl004", stage), ITEM_APPR_ARMOR_MODEL_TORSO, 300));
    SetLocalObject(pc, "h2", Part(CreateItemOnObject("nw_aarcl004", stage), ITEM_APPR_ARMOR_MODEL_TORSO, 1000));
    SetLocalObject(pc, "h3", Part(CreateItemOnObject("nw_aarcl004", stage), ITEM_APPR_ARMOR_MODEL_PELVIS, 300));
    SetLocalObject(pc, "h4", Part(CreateItemOnObject("nw_aarcl004", stage), ITEM_APPR_ARMOR_MODEL_ROBE, 50));
    SetLocalObject(pc, "h5", CreateItemOnObject("armprobe", stage));
}

void Give(object pc)
{
    int i;
    for (i = 1; i <= 5; i++)
    {
        object o = CopyItem(GetLocalObject(pc, "h" + IntToString(i)), pc, TRUE);
        SetLocalObject(pc, "p" + IntToString(i), o);
        Describe(o, "h" + IntToString(i));
    }
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    Note("character-fixture|" + GetName(pc) + "|pc=" + IntToString(GetIsPC(pc)));
    Note("gender|" + IntToString(GetGender(pc)));
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    object o = GetFirstItemInInventory(pc);
    while (GetIsObjectValid(o)) { DestroyObject(o); o = GetNextItemInInventory(pc); }
    SetTag(VPlaceable(pc, "plc_chest1", 8.0, 8.0, 0.0), "armour_stage");
    DelayCommand(0.6, Build(pc));
    DelayCommand(2.4, Give(pc));
    DelayCommand(3.0, VCamera(pc, 90.0, 12.0, 60.0));
    DelayCommand(5.0, Tap(73));
    DelayCommand(7.0, VShot("a_inventory"));
    DelayCommand(7.5, Tap(73));
    int n;
    for (n = 1; n <= 5; n++)
    {
        float t = 6.0 + 2.5 * IntToFloat(n);
        DelayCommand(t, Examine(pc, GetLocalObject(pc, "p" + IntToString(n))));
        DelayCommand(t + 1.6, VShot("x_examine_h" + IntToString(n)));
    }
    DelayCommand(22.0, Tap(27));
    DelayCommand(24.0, VDone());
}
