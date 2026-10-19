#include "parity_inc"
void main()
{
    PLogI("empty.remaining.big", GetScriptInstructionsRemaining() > 100000);
}
