void main()
{
    object o = GetLastDisarmed();
    WriteTimestampedLogEntry("VISUAL|note|ev|disarm|" + GetTag(OBJECT_SELF) + "|" + (GetIsObjectValid(o) ? GetName(o) + "/" + GetTag(o) : "invalid") + "|trapped=" + IntToString(GetIsTrapped(OBJECT_SELF)));
}
