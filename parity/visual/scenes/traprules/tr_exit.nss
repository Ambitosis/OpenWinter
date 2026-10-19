void main()
{
    object o = GetExitingObject();
    WriteTimestampedLogEntry("VISUAL|note|ev|exit|" + GetTag(OBJECT_SELF) + "|" + (GetIsObjectValid(o) ? GetName(o) : "invalid") + "|trapped=" + IntToString(GetIsTrapped(OBJECT_SELF)) + "|hp=" + IntToString(GetCurrentHitPoints(o)));
}
