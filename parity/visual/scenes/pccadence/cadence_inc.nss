void CNote(string key,string value) { WriteTimestampedLogEntry("VISUAL|note|"+key+"|"+value); }
void CClockSet(int tick) { SetLocalInt(GetModule(),"cadence_clock",tick); }
void CClockStart() { int i; for(i=0;i<=2780;i++) DelayCommand(IntToFloat(i)/20.0,CClockSet(i)); }
void CEvent(string kind,object actor) {
 string key=GetLocalString(actor,"cadence_key"); if(key=="") return;
 string phase=GetLocalString(GetModule(),"cadence_phase");
 int tick=GetLocalInt(GetModule(),"cadence_clock");
 CNote(phase+"."+key+"."+kind,IntToString(tick));
}
