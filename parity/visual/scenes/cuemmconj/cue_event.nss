void main(){SetLocalInt(GetModule(),"cue_events",GetLocalInt(GetModule(),"cue_events")+1);WriteTimestampedLogEntry("VISUAL|note|spell_event."+IntToString(GetLastSpell()));}
