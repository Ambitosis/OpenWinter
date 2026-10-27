// Logs each OnPlayerGuiEvent as a note: type, integer, object (tag, or PC), vector.
void main()
{
    object oPC = GetLastGuiEventPlayer();
    object o = GetLastGuiEventObject();
    vector v = GetLastGuiEventVector();
    string sObj = !GetIsObjectValid(o) ? "INVALID" : o == oPC ? "PC" : GetTag(o) + "/" + IntToString(GetObjectType(o));
    WriteTimestampedLogEntry("VISUAL|note|gui|" + IntToString(GetLastGuiEventType()) + "|" + IntToString(GetLastGuiEventInteger()) + "|" + sObj
        + "|" + FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2) + "," + FloatToString(v.z, 0, 2));
}
