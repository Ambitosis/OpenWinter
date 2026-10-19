#include "parity_inc"

void main()
{
    object o = OBJECT_SELF;
    PLog("order", "ud:" + (o == GetModule() ? "module" : GetTag(o)) + ":" + IntToString(GetUserDefinedEventNumber()));
}
