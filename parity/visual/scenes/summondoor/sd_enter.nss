#include "sd_inc"
void main() { object o=GetEnteringObject(); SDNote("enter|"+GetTag(OBJECT_SELF)+"|"+GetTag(o)+"|pc="+IntToString(GetIsPC(o))+"|"+Vec(GetPosition(o))); }
