// Observe the actual post-association callbacks rather than assume AddHenchman
// leaves the pre-association clear intact. No event is silently cleared here.
int BlankBindings(object h)
{
    int eventId;
    for (eventId = 5000; eventId <= 5012; eventId++)
    {
        if (GetEventScript(h, eventId) != "")
        {
            WriteTimestampedLogEntry("VISUAL|note|INVALID_BINDING." + IntToString(eventId));
            return FALSE;
        }
    }
    return TRUE;
}
