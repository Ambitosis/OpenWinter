// ExecuteScript target: reports OBJECT_SELF, keyed by its tag.
#include "parity_inc"

void main()
{
    string k = "self.exec." + GetTag(OBJECT_SELF);
    PLogI(k + ".type", GetObjectType(OBJECT_SELF));
    PLogI(k + ".ismodule", OBJECT_SELF == GetModule());
    PLogI(k + ".valid", GetIsObjectValid(OBJECT_SELF));
}
