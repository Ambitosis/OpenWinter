void Note(string key,string value){WriteTimestampedLogEntry("VISUAL|note|"+key+"|"+value);}
void ClockSet(int n){SetLocalInt(GetModule(),"cad_clock",n);}
void ClockStart(){int i;for(i=0;i<1740;i++)DelayCommand(IntToFloat(i)/20.0,ClockSet(i));}
void Event(object c,string kind){Note(GetLocalString(c,"cad_key")+"."+GetLocalString(c,"cad_phase")+"."+kind,IntToString(GetLocalInt(GetModule(),"cad_clock")));}
