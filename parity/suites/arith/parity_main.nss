// Arithmetic: overflow, division, bitwise operators and shifts, float edge cases, maths functions.
// Values go through I()/F() so the compiler can't fold them. Not covered: int.MinValue / -1 and
// int.MinValue % -1, which crash nwserver (OpenWinter aborts the script instead).
#include "parity_inc"

int I(int n) { return n; }
float F(float f) { return f; }
void Fl(string k, float f) { PLog(k, FloatToString(f, 0, 9)); }

void Ints()
{
    int max = I(2147483647), min = I(-2147483647 - 1);
    PLogI("ovf.max+1", max + I(1));
    PLogI("ovf.min-1", min - I(1));
    PLogI("ovf.max*2", max * I(2));
    PLogI("ovf.min*-1", min * I(-1));
    PLogI("ovf.negmin", -min);
    PLogI("ovf.absmin", abs(min));
    int i = max;
    i++;
    PLogI("ovf.inc", i);
    i = min;
    i--;
    PLogI("ovf.dec", i);
    i = max;
    i += 10;
    PLogI("ovf.addassign", i);
    PLogI("div.-7/2", I(-7) / I(2));
    PLogI("div.7/-2", I(7) / I(-2));
    PLogI("div.-7/-2", I(-7) / I(-2));
    PLogI("mod.-7%3", I(-7) % I(3));
    PLogI("mod.7%-3", I(7) % I(-3));
    PLogI("mod.-7%-3", I(-7) % I(-3));
    PLogI("mod.0%5", I(0) % I(5));
    PLogI("mod.min%max", min % max);

    int s;
    for (s = -2; s <= 34; s++)
    {
        if (s > 2 && s < 30 && s != 16) continue;
        string k = IntToString(s);
        PLogI("shl.1." + k, I(1) << s);
        PLogI("shl.-3." + k, I(-3) << s);
        PLogI("shr.-16." + k, I(-16) >> s);
        PLogI("shr.min." + k, min >> s);
        PLogI("shr.1000." + k, I(1000) >> s);
        PLogI("ushr.-16." + k, I(-16) >>> s);
        PLogI("ushr.min." + k, min >>> s);
        PLogI("ushr.-1." + k, I(-1) >>> s);
        PLogI("ushr.1000." + k, I(1000) >>> s);
    }
    PLogI("bit.not0", ~I(0));
    PLogI("bit.not-1", ~I(-1));
    PLogI("bit.and", I(12) & I(10));
    PLogI("bit.or", I(12) | I(10));
    PLogI("bit.xor", I(12) ^ I(10));
    PLogI("bit.xorneg", I(-1) ^ I(5));
    PLogI("bit.andneg", min & I(-1));
    PLogI("log.and", I(5) && I(3));
    PLogI("log.or", I(0) || I(-4));
    PLogI("log.not", !I(7));
    PLogI("log.notneg", !I(-1));
    PLogI("log.not0", !I(0));
    PLogI("cmp.lt", I(-1) < I(1));
    PLogI("cmp.minmax", min < max);
}

void Floats()
{
    float inf = StringToFloat("inf"), nan = StringToFloat("nan");
    Fl("f.0.1+0.2", F(0.1) + F(0.2));
    Fl("f.1/3", F(1.0) / F(3.0));
    Fl("f.big+1", F(16777216.0) + F(1.0));
    Fl("f.ovf", F(340000000000000000000000000000000000000.0) * F(10.0));
    Fl("f.uflow", F(0.0000000000000000000000000000000000001) / F(100000000000.0));
    Fl("f.int+f", I(7) + F(0.5));
    Fl("f.f+int", F(0.5) + I(7));
    Fl("f.int/f", I(7) / F(2.0));
    Fl("f.f/int", F(7.0) / I(2));
    Fl("f.bigint", IntToFloat(I(2147483647)) + F(0.0));
    Fl("f.neg0", -F(0.0));
    Fl("f.inf-inf", inf - inf);
    Fl("f.inf*0", inf * F(0.0));
    PLogI("f.nan==nan", nan == nan);
    PLogI("f.nan!=nan", nan != nan);
    PLogI("f.nan<1", nan < F(1.0));
    PLogI("f.nan>1", nan > F(1.0));
    PLogI("f.nan<=1", nan <= F(1.0));
    PLogI("f.nan>=1", nan >= F(1.0));
    PLogI("f.0==-0", F(0.0) == -F(0.0));
    PLogI("f.inf>max", inf > F(340000000000000000000000000000000000000.0));
    PLogI("f.eqeps", F(0.1) + F(0.2) == F(0.3));
    Fl("f.ftoi", IntToFloat(FloatToInt(F(-0.5))));
}

void Maths()
{
    float nan = StringToFloat("nan");
    Fl("m.sqrt-1", sqrt(F(-1.0)));
    Fl("m.sqrt0", sqrt(F(0.0)));
    Fl("m.sqrt2", sqrt(F(2.0)));
    Fl("m.sqrtnan", sqrt(nan));
    Fl("m.log0", log(F(0.0)));
    Fl("m.lognan", log(nan));
    Fl("m.loginf", log(StringToFloat("inf")));
    Fl("m.pow0-0.5", pow(F(0.0), F(-0.5)));
    Fl("m.pow-0-1", pow(-F(0.0), F(-1.0)));
    Fl("m.pownan0", pow(nan, F(0.0)));
    Fl("m.pow1nan", pow(F(1.0), nan));
    Fl("m.pow2inf", pow(F(2.0), StringToFloat("inf")));
    Fl("m.pow2-inf", pow(F(2.0), -StringToFloat("inf")));
    Fl("m.cosinf", cos(StringToFloat("inf")));
    Fl("m.cosnan", cos(nan));
    Fl("m.atannan", atan(nan));
    Fl("m.asinnan", asin(nan));
    Fl("m.fabsnan", fabs(nan));
    Fl("m.fabs-nan", fabs(-nan));
    Fl("m.log-1", log(F(-1.0)));
    Fl("m.log1", log(F(1.0)));
    Fl("m.loge", log(F(2.718281828)));
    Fl("m.log1e30", log(F(1000000000000000000000000000000.0)));
    Fl("m.pow00", pow(F(0.0), F(0.0)));
    Fl("m.pow0-1", pow(F(0.0), F(-1.0)));
    Fl("m.pow-8third", pow(F(-8.0), F(1.0) / F(3.0)));
    Fl("m.pow-2.2", pow(F(-2.0), F(2.0)));
    Fl("m.pow-2.3", pow(F(-2.0), F(3.0)));
    Fl("m.pow2-1", pow(F(2.0), F(-1.0)));
    Fl("m.pow10.38", pow(F(10.0), F(38.0)));
    Fl("m.pow10.39", pow(F(10.0), F(39.0)));
    Fl("m.pow1.5.2.5", pow(F(1.5), F(2.5)));
    float a;
    for (a = -360.0; a <= 720.0; a += 45.0)
    {
        string k = IntToString(FloatToInt(a));
        Fl("m.cos." + k, cos(a));
        Fl("m.sin." + k, sin(a));
        Fl("m.tan." + k, tan(a));
    }
    Fl("m.cos1e6", cos(F(1000000.0)));
    Fl("m.sin1e6", sin(F(1000000.0)));
    Fl("m.cos30", cos(F(30.0)));
    Fl("m.sin30", sin(F(30.0)));
    Fl("m.tan89.9", tan(F(89.9)));
    for (a = -2.0; a <= 2.0; a += 0.5)
    {
        string k = FloatToString(a, 0, 1);
        Fl("m.acos." + k, acos(a));
        Fl("m.asin." + k, asin(a));
        Fl("m.atan." + k, atan(a));
    }
    Fl("m.atan1e30", atan(F(1000000000000000000000000000000.0)));
    Fl("m.atan-inf", atan(-StringToFloat("inf")));
    Fl("m.acosnan", acos(nan));
    Fl("m.fabs-0", fabs(-F(0.0)));
    Fl("m.fabs-inf", fabs(-StringToFloat("inf")));
    PLogI("m.abs-5", abs(I(-5)));
    Fl("m.feet-3", FeetToMeters(F(-3.0)));
    Fl("m.yards1.5", YardsToMeters(F(1.5)));
    Fl("m.rounds-1", RoundsToSeconds(I(-1)));
    Fl("m.turns0", TurnsToSeconds(I(0)));
}

void Errors()
{
    // Each child script logs before and after the faulting operation.
    string s = "ar_div0,ar_mod0,ar_fdiv0,ar_fdivi0,ar_vdiv0";
    int n = 0, e;
    while (n < GetStringLength(s))
    {
        e = FindSubString(s, ",", n);
        if (e < 0) e = GetStringLength(s);
        string scr = GetSubString(s, n, e - n);
        ExecuteScript(scr);
        PLog("err." + scr + ".parent", "continues");
        PLogI("err." + scr + ".remaining", GetScriptInstructionsRemaining() > 0);
        n = e + 1;
    }
}

void main()
{
    DelayCommand(1.0, Ints());
    DelayCommand(1.1, Floats());
    DelayCommand(1.2, Maths());
    DelayCommand(1.5, Errors());
    DelayCommand(2.5, PDone());
}
