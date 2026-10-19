// Random numbers: ranges and distributions of Random and the dice functions, checked statistically.
#include "parity_inc"

int Roll(int nMode, int n)
{
    switch (nMode)
    {
        case 0: return Random(n);
        case 2: return d2(n);
        case 3: return d3(n);
        case 4: return d4(n);
        case 6: return d6(n);
        case 8: return d8(n);
        case 10: return d10(n);
        case 12: return d12(n);
        case 20: return d20(n);
        case 100: return d100(n);
    }
    return -999;
}

// Exact min and max for small ranges; mean and per-value rates within statistical tolerance.
void Stat(string k, int nMode, int n, int nTrials, int nLow, int nHigh, int bExact = TRUE)
{
    int i, v, lo = 2147483647, hi = -2147483647 - 1;
    float sum = 0.0;
    object m = GetModule();
    for (i = 0; i < nTrials; i++)
    {
        v = Roll(nMode, n);
        if (v < lo) lo = v;
        if (v > hi) hi = v;
        sum += IntToFloat(v);
        if (v >= nLow && v <= nHigh)
            SetLocalInt(m, k + IntToString(v), GetLocalInt(m, k + IntToString(v)) + 1);
    }
    PLogI(k + ".inrange", lo >= nLow && hi <= nHigh);
    if (bExact)
    {
        PLogI(k + ".min", lo);
        PLogI(k + ".max", hi);
    }
    float range = IntToFloat(nHigh - nLow + 1);
    // Standard error of the mean of a uniform-ish range, times 5.
    PLogF(k + ".mean", sum / IntToFloat(nTrials), 5.0 * range / 3.4 / sqrt(IntToFloat(nTrials)) + 0.001);
    for (v = nLow; v <= nHigh; v++)
    {
        float p = IntToFloat(GetLocalInt(m, k + IntToString(v))) / IntToFloat(nTrials);
        PLogF(k + ".p" + IntToString(v), p, 5.0 * sqrt(0.25 / IntToFloat(nTrials)));
    }
}

// Large ranges: only the shape (mean relative to n, and whether values reach the top).
void Wide(string k, int n, int nTrials)
{
    int i, v, hi = 0, bad = 0;
    float sum = 0.0;
    for (i = 0; i < nTrials; i++)
    {
        v = Random(n);
        if (v < 0 || v >= n) bad++;
        if (v > hi) hi = v;
        sum += IntToFloat(v) / IntToFloat(n);
    }
    PLogI(k + ".outofrange", bad);
    PLogI(k + ".top1pct", hi >= n - n / 100);
    PLogF(k + ".mean", sum / IntToFloat(nTrials), 0.03);
}

void Edges()
{
    int i;
    for (i = 0; i < 3; i++)
    {
        string s = "." + IntToString(i);
        PLogI("edge.random0" + s, Random(0));
        PLogI("edge.random-1" + s, Random(-1));
        PLogI("edge.random1" + s, Random(1));
    }
    // Every value seen across many d6(0) / Random(-5) rolls.
    int a, lo = 1000, hi = -1000;
    for (i = 0; i < 300; i++) { a = d6(0); if (a < lo) lo = a; if (a > hi) hi = a; }
    PLogI("edge.d6.0.min", lo);
    PLogI("edge.d6.0.max", hi);
    lo = 1000; hi = -1000;
    for (i = 0; i < 300; i++) { a = d4(-3); if (a < lo) lo = a; if (a > hi) hi = a; }
    PLogI("edge.d4.-3.min", lo);
    PLogI("edge.d4.-3.max", hi);
    lo = 1000; hi = -1000;
    for (i = 0; i < 400; i++) { a = Random(-3); if (a < lo) lo = a; if (a > hi) hi = a; }
    PLogI("edge.random-3.min", lo);
    PLogI("edge.random-3.max", hi);
    lo = 1000; hi = -1000;
    for (i = 0; i < 400; i++) { a = Random(-2); if (a < lo) lo = a; if (a > hi) hi = a; }
    PLogI("edge.random-2.min", lo);
    PLogI("edge.random-2.max", hi);
    lo = 1000000; hi = -1000000;
    float sum = 0.0;
    for (i = 0; i < 2000; i++) { a = Random(-100); sum += IntToFloat(a); if (a < lo) lo = a; if (a > hi) hi = a; }
    PLogI("edge.random-100.min", lo);
    PLogI("edge.random-100.max", hi);
    PLogF("edge.random-100.mean", sum / 2000.0, 5.0);
    lo = 1000000; hi = -1000000;
    sum = 0.0;
    for (i = 0; i < 2000; i++) { a = Random(-40000); sum += IntToFloat(a); if (a < lo) lo = a; if (a > hi) hi = a; }
    PLogI("edge.random-40000.min>-40000", lo > -40000);
    PLogI("edge.random-40000.max<=0", hi <= 0);
    PLogF("edge.random-40000.mean", sum / 2000.0, 1500.0);
}

void main()
{
    DelayCommand(1.0, Edges());
    DelayCommand(1.1, Stat("r2", 0, 2, 4000, 0, 1));
    DelayCommand(1.2, Stat("r10", 0, 10, 4000, 0, 9));
    DelayCommand(1.3, Stat("r37", 0, 37, 4000, 0, 36));
    DelayCommand(1.4, Stat("d2", 2, 1, 3000, 1, 2));
    DelayCommand(1.5, Stat("d3", 3, 1, 3000, 1, 3));
    DelayCommand(1.6, Stat("d4", 4, 1, 3000, 1, 4));
    DelayCommand(1.7, Stat("d6", 6, 1, 3000, 1, 6));
    DelayCommand(1.8, Stat("d8", 8, 1, 3000, 1, 8));
    DelayCommand(1.9, Stat("d10", 10, 1, 3000, 1, 10));
    DelayCommand(2.0, Stat("d12", 12, 1, 3000, 1, 12));
    DelayCommand(2.1, Stat("d20", 20, 1, 4000, 1, 20));
    DelayCommand(2.2, Stat("d100", 100, 1, 4000, 1, 100));
    DelayCommand(2.3, Stat("3d6", 6, 3, 3000, 3, 18));
    DelayCommand(2.4, Stat("2d4", 4, 2, 3000, 2, 8));
    DelayCommand(2.5, Stat("10d10", 10, 10, 2000, 10, 100, FALSE));
    DelayCommand(2.6, Wide("w1000", 1000, 3000));
    DelayCommand(2.7, Wide("w32768", 32768, 3000));
    DelayCommand(2.8, Wide("w100000", 100000, 3000));
    DelayCommand(2.9, Wide("w2^30", 1073741824, 3000));
    DelayCommand(3.0, Wide("wmax", 2147483647, 3000));
    DelayCommand(4.0, PDone());
}
