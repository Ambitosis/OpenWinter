#include "visual_inc"
void Note(object pc,string name) {
 WriteTimestampedLogEntry("VISUAL|note|"+name+"|parry="+IntToString(GetActionMode(pc,ACTION_MODE_PARRY))+"|search="+IntToString(GetActionMode(pc,ACTION_MODE_DETECT))+"|parryrank="+IntToString(GetSkillRank(SKILL_PARRY,pc,TRUE))+"|keen="+IntToString(GetHasFeat(FEAT_KEEN_SENSE,pc)));
}
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0)); DelayCommand(3.0,Note(pc,"before"));
 DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|key|96|tap"));
 DelayCommand(5.0,WriteTimestampedLogEntry("VISUAL|key|99|tap"));
 DelayCommand(6.0,WriteTimestampedLogEntry("VISUAL|key|104|tap"));
 DelayCommand(7.0,Note(pc,"parry_selected")); DelayCommand(7.1,VShot("a_parry"));
 DelayCommand(8.0,SetActionMode(pc,ACTION_MODE_PARRY,FALSE));
 DelayCommand(9.0,WriteTimestampedLogEntry("VISUAL|key|96|tap"));
 DelayCommand(10.0,WriteTimestampedLogEntry("VISUAL|key|100|tap"));
 DelayCommand(11.0,WriteTimestampedLogEntry("VISUAL|key|102|tap"));
 DelayCommand(12.0,Note(pc,"search_selected")); DelayCommand(12.1,VShot("b_search"));
 DelayCommand(13.0,VDone());
}
