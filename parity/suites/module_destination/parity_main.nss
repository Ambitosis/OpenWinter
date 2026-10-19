#include "parity_inc"
void main()
{
    PLogI("destination.loaded", 1);
    PLogI("destination.sourceLocal", GetLocalInt(OBJECT_SELF, "sourceMarker"));
    DelayCommand(4.0, PDone());
}
