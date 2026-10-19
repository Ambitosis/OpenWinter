void main() { WriteTimestampedLogEntry("VISUAL|voice_damage|"+IntToString(GetLocalInt(OBJECT_SELF,"actor"))+"|"+IntToString(GetTotalDamageDealt())); }
