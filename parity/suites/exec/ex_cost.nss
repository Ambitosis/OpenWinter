#include "parity_inc"
// Instruction costs of common constructs, measured with GetScriptInstructionsRemaining.
int F(int n) { return n + 1; }
void main()
{
    int a = GetScriptInstructionsRemaining();
    int b = GetScriptInstructionsRemaining();
    PLogI("cost.call", a - b);
    int x = 0;
    a = GetScriptInstructionsRemaining();
    x = x + 1;
    b = GetScriptInstructionsRemaining();
    PLogI("cost.add", a - b);
    a = GetScriptInstructionsRemaining();
    x = F(x);
    b = GetScriptInstructionsRemaining();
    PLogI("cost.fcall", a - b);
    a = GetScriptInstructionsRemaining();
    int i;
    for (i = 0; i < 10; i++) x++;
    b = GetScriptInstructionsRemaining();
    PLogI("cost.loop10", a - b);
    a = GetScriptInstructionsRemaining();
    ExecuteScript("ex_empty");
    b = GetScriptInstructionsRemaining();
    PLogI("cost.exec", a - b);
}
