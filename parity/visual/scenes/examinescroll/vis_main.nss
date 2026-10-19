// The examine windows' scroll bars: an item and a creature with a one-line description, then the same with a
// description far longer than the text box. ActionExamine opens the windows without pointer input.
#include "visual_inc"

string Long()
{
    string s = "";
    int i;
    for (i = 1; i <= 40; i++) s += "Line " + IntToString(i) + " of a long description that needs a scroll bar.\n";
    return s;
}

void Examine(object pc, object o) { AssignCommand(pc, ClearAllActions()); AssignCommand(pc, ActionExamine(o)); }

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetPlotFlag(pc, TRUE);
    object shortItem = CreateItemOnObject("nw_wswdg001", pc);
    SetIdentified(shortItem, TRUE);
    SetDescription(shortItem, "A short blade.");
    object longItem = CreateItemOnObject("nw_wswdg001", pc);
    SetIdentified(longItem, TRUE);
    SetDescription(longItem, Long());
    object npc = VSpawn(pc, "nw_bandit001", 0.0, 3.0, 270.0);
    DelayCommand(2.0, VCamera(pc, 90.0, 8.0, 60.0));
    DelayCommand(4.0, Examine(pc, shortItem));
    DelayCommand(6.0, VShot("a_item_short"));
    DelayCommand(9.0, Examine(pc, longItem));
    DelayCommand(11.0, VShot("b_item_long"));
    DelayCommand(14.0, SetDescription(npc, "A bandit."));
    DelayCommand(14.5, Examine(pc, npc));
    DelayCommand(16.5, VShot("c_creature_short"));
    DelayCommand(19.0, SetDescription(npc, Long()));
    DelayCommand(19.5, Examine(pc, npc));
    DelayCommand(21.5, VShot("d_creature_long"));
    DelayCommand(24.0, VDone());
}
