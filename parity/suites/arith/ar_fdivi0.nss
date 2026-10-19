#include "parity_inc"
int I(int n) { return n; }
float F(float f) { return f; }
void main()
{
    PLog("err.ar_fdivi0.before", "1");
    float x = F(1.0) / I(0);
    PLog("err.ar_fdivi0.after", FloatToString(x));
}
