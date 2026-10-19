void main(){SetLocalInt(GetModule(),"cue_hooks",GetLocalInt(GetModule(),"cue_hooks")+1);WriteTimestampedLogEntry("VISUAL|note|impact."+IntToString(GetSpellId()));}
