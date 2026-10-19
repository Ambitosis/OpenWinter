// loadevents: one log line with the game clock (seconds since midnight, in game time) and a tag.
void LeNote(string s)
{
    WriteTimestampedLogEntry("VISUAL|note|le|" + s + "|clock=" + IntToString(GetTimeHour()) + ":" + IntToString(GetTimeMinute())
        + ":" + IntToString(GetTimeSecond()) + "." + IntToString(GetTimeMillisecond()));
}
