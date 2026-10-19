// Instrument only the impact: all casting, slots and concentration remain engine-owned.
void main() {
    SetLocalInt(OBJECT_SELF, "impacts", GetLocalInt(OBJECT_SELF, "impacts") + 1);
}
