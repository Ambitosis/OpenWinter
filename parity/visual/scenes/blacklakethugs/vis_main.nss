// Blacklake's diseased thugs (Chapter 1 map_m1q3i, nw_disthug placed in the area with clothes and a club the toolset
// didn't mark droppable): killed where they stand, do they leave a bag? Notes list each kill and every bag near it.
#include "visual_inc"

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }

void Bags(object pc)
{
    int n;
    for (n = 0; n < GetLocalInt(GetModule(), "thugs"); n++)
    {
        location l = GetLocalLocation(GetModule(), "at" + IntToString(n));
        int bags = 0;
        object o = GetFirstObjectInArea(GetArea(pc));
        while (GetIsObjectValid(o))
        {
            if (GetObjectType(o) == OBJECT_TYPE_PLACEABLE && GetTag(o) == "BodyBag" && GetDistanceBetweenLocations(GetLocation(o), l) < 1.5)
            {
                bags++;
                object it = GetFirstItemInInventory(o);
                while (GetIsObjectValid(it)) { Note("thug " + IntToString(n) + " bag holds " + GetResRef(it)); it = GetNextItemInInventory(o); }
            }
            o = GetNextObjectInArea(GetArea(pc));
        }
        Note("thug " + IntToString(n) + " corpse " + IntToString(GetIsObjectValid(GetLocalObject(GetModule(), "thug" + IntToString(n)))) + " bags " + IntToString(bags));
    }
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    int n = 0, i = 0;
    object o = GetFirstObjectInArea(GetArea(pc));
    while (GetIsObjectValid(o))
    {
        string r = GetResRef(o);
        if (GetObjectType(o) == OBJECT_TYPE_CREATURE && (r == "nw_disthug" || r == "nw_disthug001") && n < 4)
        {
            SetLocalObject(GetModule(), "thug" + IntToString(n), o);
            SetLocalLocation(GetModule(), "at" + IntToString(n), GetLocation(o));
            Note("thug " + IntToString(n) + " " + r + " chest " + GetResRef(GetItemInSlot(INVENTORY_SLOT_CHEST, o)) + " drop "
                + IntToString(GetDroppableFlag(GetItemInSlot(INVENTORY_SLOT_CHEST, o))) + " weapon " + GetResRef(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, o))
                + " drop " + IntToString(GetDroppableFlag(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, o))));
            DelayCommand(3.0, AssignCommand(pc, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), o)));
            n++;
        }
        o = GetNextObjectInArea(GetArea(pc));
    }
    SetLocalInt(GetModule(), "thugs", n);
    location l = GetLocalLocation(GetModule(), "at0");
    vector v = GetPositionFromLocation(l);
    DelayCommand(0.5, VMovePC(pc, v.x - 4.0, v.y - 4.0, v.z, 45.0));
    DelayCommand(2.0, VCamera(pc, 45.0, 12.0, 50.0));
    DelayCommand(2.5, VShot("alive"));
    DelayCommand(12.0, Bags(pc));
    DelayCommand(12.0, VShot("after"));
    DelayCommand(15.0, VDone());
}
