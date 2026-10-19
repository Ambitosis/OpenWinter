#include "parity_inc"
int I(int n) { return n; }
float F(float f) { return f; }
void main()
{
    PLog("err.ar_vdiv0.before", "1");
    vector x = Vector(1.0, 2.0, 3.0) / F(0.0);
    PLog("err.ar_vdiv0.after", FloatToString(x.x));
}
