// A player's items and the drop flag: what a player picking an item up from the ground holds (ground items read
// GetDroppableFlag 0 on nwserver, corpse_drops), what CreateItemOnObject gives a player, and whether a player can
// still put an item down after SetDroppableFlag(FALSE). Notes only; one shot to close the run.
#include "visual_inc"

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }

void Flags(object o, string tag)
{
    Note(tag + " drop " + IntToString(GetDroppableFlag(o)) + " pick " + IntToString(GetPickpocketableFlag(o))
        + " held " + IntToString(GetItemPossessor(o) == GetFirstPC()) + " valid " + IntToString(GetIsObjectValid(o)));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    SetCutsceneMode(pc, FALSE);
    object g = CreateObject(OBJECT_TYPE_ITEM, "nw_it_gem001", Location(GetArea(pc), GetPosition(pc) + Vector(1.0, 0.0, 0.0), 0.0));
    Flags(g, "ground");
    AssignCommand(pc, ActionPickUpItem(g));
    object c = CreateItemOnObject("nw_it_gem002", pc);
    Flags(c, "created");
    object u = CreateItemOnObject("nw_it_gem003", pc);
    SetDroppableFlag(u, FALSE);
    Flags(u, "cleared");
    DelayCommand(4.0, Flags(g, "pickedup"));
    DelayCommand(5.0, AssignCommand(pc, ActionPutDownItem(u)));
    DelayCommand(9.0, Flags(u, "putdown_undroppable"));
    DelayCommand(10.0, AssignCommand(pc, ActionPutDownItem(g)));
    DelayCommand(14.0, Flags(g, "putdown_pickedup"));
    DelayCommand(15.0, VShot("end"));
    DelayCommand(17.0, VDone());
}
