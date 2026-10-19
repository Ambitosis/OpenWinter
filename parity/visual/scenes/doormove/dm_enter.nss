// The Training Halls' OnEnter: who arrives, when and where.
#include "dm_inc"
void main() {
    object o=GetEnteringObject();
    DMNote("enter|"+GetTag(OBJECT_SELF)+"|"+GetTag(o)+"|pc="+IntToString(GetIsPC(o))+"|"+Vec(GetPosition(o)));
}
