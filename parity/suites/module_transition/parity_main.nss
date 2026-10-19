#include "parity_inc"
void Switch()
{
    PLogI("source.before", 1);
    StartNewModule("parity_module_destination");
    PLogI("source.after", 1);
    DelayCommand(3.0, PLogI("source.timerSurvived", 1));
}
void CheckFailed()
{
    PLogI("missing.stillInSource", GetLocalInt(OBJECT_SELF, "sourceMarker"));
    Switch();
}
void main()
{
    SetLocalInt(OBJECT_SELF, "sourceMarker", 73);
    PLogI("source.loaded", 1);
    StartNewModule("ely_missing_module_probe");
    DelayCommand(1.0, CheckFailed());
    DelayCommand(7.0, PDone());
}
