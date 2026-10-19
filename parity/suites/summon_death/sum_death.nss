#include "parity_inc"
void Report(string phase)
{
    string key = GetLocalString(OBJECT_SELF, "KEY") + "." + phase;
    PLogI(key + ".master", GetMaster() == GetLocalObject(OBJECT_SELF, "OWNER"));
    PLogI(key + ".type", GetAssociateType(OBJECT_SELF));
    PLogI(key + ".samefaction", GetFactionEqual(OBJECT_SELF, GetLocalObject(OBJECT_SELF, "OWNER")));
}
void main()
{
    Report("scriptbefore");
    string stock = GetLocalString(OBJECT_SELF, "STOCK");
    if (stock != "") ExecuteScript(stock, OBJECT_SELF);
    Report("scriptafter");
    DelayCommand(0.1, Report("delayed"));
}
