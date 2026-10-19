// The Training Halls' OnEnter: who arrives, when and where.
#include "tf_inc"
void main() {
    object o=GetEnteringObject();
    TFNote("enter|"+GetTag(OBJECT_SELF)+"|"+GetTag(o)+"|pc="+IntToString(GetIsPC(o))+"|"+Vec(GetPosition(o)));
}
