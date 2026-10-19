#include "parity_inc"
void Report(string phase){string key=GetLocalString(OBJECT_SELF,"AUDIT_KEY");PLogI(key+"."+phase+".raiseable",GetIsRaiseable(OBJECT_SELF));PLogI(key+"."+phase+".master",GetIsObjectValid(GetMaster()));PLogI(key+"."+phase+".type",GetAssociateType(OBJECT_SELF));}
void main(){Report("scriptbefore");ExecuteScript("nw_ch_ac7",OBJECT_SELF);Report("scriptafter");DelayCommand(0.1,Report("delayed"));}
