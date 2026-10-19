#include "ep_inc"
// Heartbeats are counted, not sequenced (their phase differs between engines).
void main() { SetLocalInt(OBJECT_SELF, "hb", GetLocalInt(OBJECT_SELF, "hb") + 1); }
