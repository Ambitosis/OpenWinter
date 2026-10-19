void main()
{
    object o = GetEnteringObject();
    WriteTimestampedLogEntry("VISUAL|note|ev|trig|" + GetTag(OBJECT_SELF) + "|" + (GetIsObjectValid(o) ? GetName(o) : "invalid") + "|trapped=" + IntToString(GetIsTrapped(OBJECT_SELF)) + "|hp=" + IntToString(GetCurrentHitPoints(o)));
}
