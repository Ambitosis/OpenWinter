// Run on a creature: makes it listen for two patterns.
void main()
{
    SetListening(OBJECT_SELF, TRUE);
    SetListenPattern(OBJECT_SELF, "**gold**", 7);
    SetListenPattern(OBJECT_SELF, "hello **", 3);
    SetLocalInt(OBJECT_SELF, "listening", GetIsListening(OBJECT_SELF));
}
