// Authored audio isolation: keep the real prepared cast, omit script missiles/impact audio.
void main(){WriteTimestampedLogEntry("VISUAL|note|"+GetLocalString(OBJECT_SELF,"hp_case")+".impact|"+IntToString(GetLocalInt(GetModule(),"hp_clock"))+".spell."+IntToString(GetSpellId()));}
