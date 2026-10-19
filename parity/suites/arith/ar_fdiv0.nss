#include "parity_inc"
int I(int n) { return n; }
float F(float f) { return f; }
void main()
{
    PLog("err.ar_fdiv0.before", "1");
    float x = F(1.0) / F(0.0);
    PLog("err.ar_fdiv0.after", FloatToString(x));
}
