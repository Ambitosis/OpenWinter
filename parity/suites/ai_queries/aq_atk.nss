#include "aq_inc"
void main()
{
    object o = OBJECT_SELF;
    int n = GetLocalInt(o, "AQ_SWN");
    SetLocalInt(o, "AQ_SWN", n + 1);
    if (n < 12) SetLocalFloat(o, "AQ_SW" + IntToString(n), PSince());
}
