// RunScript effect handler for the effects tests: counts how often it fired.
void main()
{
    object oM = GetModule();
    SetLocalInt(oM, "rs_calls", GetLocalInt(oM, "rs_calls") + 1);
}
