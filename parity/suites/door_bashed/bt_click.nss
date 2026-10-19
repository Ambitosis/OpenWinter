#include "pbashed_inc"
// OnAreaTransitionClick: logged with the clicker's current action; the clicker is not moved.
void main()
{
    object o = GetClickingObject();
    BEv(OBJECT_SELF, "click", o);
    IAFirst(o, "click", GetTag(OBJECT_SELF) + " action " + IntToString(GetCurrentAction(o)));
}
