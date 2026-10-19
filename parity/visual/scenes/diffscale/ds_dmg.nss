// diffscale: a target's OnDamaged logs who hit it and for how much.
void main()
{
    object a = GetLastDamager();
    WriteTimestampedLogEntry("VISUAL|note|ds|dmg|" + GetTag(OBJECT_SELF) + "|" + GetTag(a) + "|" + IntToString(GetTotalDamageDealt()));
}
