#include "parity_inc"
int I(int n) { return n; }
float F(float f) { return f; }
void main()
{
    PLog("err.ar_div0.before", "1");
    int x = I(5) / I(0);
    PLog("err.ar_div0.after", IntToString(x));
}
