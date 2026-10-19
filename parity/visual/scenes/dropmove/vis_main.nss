// Does keyboard movement interrupt a put-down (ACTION_DROPITEM), and does putting down worn armour bend? The player
// puts down the outfit she wears, a dagger left alone, a dagger with W held from 0.6 s into it, a dagger right after
// that cancelled one, and one more left alone. Every 0.1 s the scene notes her current action, whether the item is on
// the ground and where she is. An earlier version
// held W from 0.1 s, 0.6 s and 1.0 s.
#include "visual_inc"

void Key(int nKey, string sState)
{
    WriteTimestampedLogEntry("VISUAL|key|" + IntToString(nKey) + "|" + sState);
}

void Sample(string trial, int n)
{
    object pc = GetFirstPC();
    object o = GetLocalObject(pc, trial);
    vector v = GetPosition(pc);
    WriteTimestampedLogEntry("VISUAL|note|" + trial + "|t=" + FloatToString(n * 0.1, 0, 1)
        + "|action=" + IntToString(GetCurrentAction(pc))
        + "|onGround=" + IntToString(GetIsObjectValid(o) && !GetIsObjectValid(GetItemPossessor(o)))
        + "|worn=" + IntToString(GetItemInSlot(INVENTORY_SLOT_CHEST, pc) == o)
        + "|pos=" + FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2));
    if (n < 25) DelayCommand(0.1, Sample(trial, n + 1));
}

void Drop(string trial, int worn)
{
    object pc = GetFirstPC();
    object o = worn ? GetItemInSlot(INVENTORY_SLOT_CHEST, pc) : CreateItemOnObject("nw_wswdg001", pc);
    SetLocalObject(pc, trial, o);
    WriteTimestampedLogEntry("VISUAL|note|" + trial + "|start|item=" + GetResRef(o));
    AssignCommand(pc, ActionPutDownItem(o));
    Sample(trial, 0);
}

void Home()
{
    object pc = GetFirstPC();
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(Location(GetArea(pc), Vector(20.0, 20.0, 0.0), 90.0)));
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    SetPlotFlag(pc, TRUE);
    AssignCommand(pc, ClearAllActions(TRUE));
    DelayCommand(1.0, VCamera(pc, 90.0, 8.0, 50.0));
    WriteTimestampedLogEntry("VISUAL|note|chest|" + GetResRef(GetItemInSlot(INVENTORY_SLOT_CHEST, pc)));
    DelayCommand(3.0, Drop("worn_first", TRUE));
    DelayCommand(3.7, VShot("worn_first_bend"));
    DelayCommand(6.5, Home());
    DelayCommand(8.0, Drop("alone", FALSE));
    DelayCommand(11.5, Home());
    DelayCommand(13.0, Drop("w_at_0.6", FALSE));
    DelayCommand(13.6, Key(87, "down"));
    DelayCommand(14.1, Key(87, "up"));
    DelayCommand(16.5, Home());
    DelayCommand(18.0, Drop("after_cancel", FALSE));
    DelayCommand(18.7, VShot("after_cancel_bend"));
    DelayCommand(21.5, Home());
    DelayCommand(23.0, Drop("alone_again", FALSE));
    DelayCommand(26.5, VDone());
}
