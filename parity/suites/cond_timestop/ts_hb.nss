// Heartbeat counter for the time stop suite.
void main()
{
    SetLocalInt(OBJECT_SELF, "beats", GetLocalInt(OBJECT_SELF, "beats") + 1);
}
