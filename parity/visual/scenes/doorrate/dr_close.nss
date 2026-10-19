void main() { WriteTimestampedLogEntry("VISUAL|note|closed|t=" + FloatToString(IntToFloat(GetTimeSecond())+IntToFloat(GetTimeMillisecond())/1000.0,0,3)); }
