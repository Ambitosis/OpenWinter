#include "parity_inc"
void main()
{
    PLog("nest.outer", "[" + GetScriptParam("nested") + "]");
    ExecuteScript("ex_nest2");
    PLog("nest.outer.after", "[" + GetScriptParam("nested") + "]");
}
