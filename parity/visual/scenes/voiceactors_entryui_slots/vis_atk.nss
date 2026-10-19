void main(){SetLocalInt(OBJECT_SELF,"attacks",GetLocalInt(OBJECT_SELF,"attacks")+1);WriteTimestampedLogEntry("VISUAL|note|entry.ui.attack.attackerPC."+IntToString(GetIsPC(GetLastAttacker())));}
