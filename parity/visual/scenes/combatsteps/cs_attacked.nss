// The dummy's OnPhysicalAttacked: when each swing is resolved.
void main() {
 object a = GetLastAttacker(); vector p = GetPosition(a);
 WriteTimestampedLogEntry("VISUAL|note|combatsteps|attacked|" + GetLocalString(GetModule(), "phase") + "|t=" + FloatToString(IntToFloat(GetTimeSecond()) + IntToFloat(GetTimeMillisecond()) / 1000.0, 0, 3)
  + "|at=" + FloatToString(p.x, 0, 2) + "," + FloatToString(p.y, 0, 2));
}
