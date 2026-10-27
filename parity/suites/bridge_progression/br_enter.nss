// Gives the entering player the experience for fifth level (10000) a few seconds after it enters, once; levels
// taken keep it.
void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC) || GetXP(oPC) >= 10000) return;
    DelayCommand(6.0, SetXP(oPC, 10000));
}
