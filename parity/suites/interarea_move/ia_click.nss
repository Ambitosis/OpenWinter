#include "pinterarea_inc"
// A transition's OnClick that doesn't move the clicker: logged with the clicker's current action inside it.
void main()
{
    object o = GetClickingObject();
    IAFirst(o, "click", GetTag(OBJECT_SELF) + " action " + IntToString(GetCurrentAction(o)));
}
