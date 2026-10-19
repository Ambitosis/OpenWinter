#include "parity_inc"

void main()
{
    PLog("order", "exec self=" + GetTag(OBJECT_SELF) + " param=" + GetScriptParam("p"));
}
