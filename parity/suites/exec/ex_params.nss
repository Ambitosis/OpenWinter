#include "parity_inc"
void main()
{
    PLog("param.a", "[" + GetScriptParam("a") + "]");
    PLog("param.b", "[" + GetScriptParam("b") + "]");
    PLog("param.missing", "[" + GetScriptParam("zz") + "]");
}
