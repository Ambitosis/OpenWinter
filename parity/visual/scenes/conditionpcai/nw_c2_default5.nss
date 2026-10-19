// Stand-in that reports when it runs on the player (conditionpcai: which scripts drive a player under a condition).
void main()
{
    if (GetIsPC(OBJECT_SELF)) WriteTimestampedLogEntry("VISUAL|note|pcai|script|nw_c2_default5");
}
