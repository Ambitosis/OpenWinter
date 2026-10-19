void main() { WriteTimestampedLogEntry("VISUAL|note|door|onclick|clicker=" + IntToString(GetIsPC(GetClickingObject())) + "|open=" + IntToString(GetIsOpen(OBJECT_SELF))); }
