// Language features end to end. Run with --own-compiler too: the module is then compiled by OpenWinter's own
// compiler (the one behind ExecuteScriptChunk) and must behave exactly as the official compiler's output.
#include "parity_inc"

const int LIMIT = 3;
int gCounter = LIMIT + 2;
int gTwice = gCounter * 2;
string gName = "glob" + IntToString(gTwice);
vector gVec = [1.0, 2.0, 3.0];

struct Pair { int a; float b; string c; vector v; };
struct Outer { struct Pair p; int n; };

int Fib(int n);
int Fib(int n) { return n < 2 ? n : Fib(n - 1) + Fib(n - 2); }

string Defaults(int a = 1, string s = "x", float f = 2.5, object o = OBJECT_SELF, vector v = [0.0, 1.0, 2.0])
{
    return IntToString(a) + s + FloatToString(f, 0, 1) + IntToString(o == GetModule()) + FloatToString(v.y, 0, 1);
}

struct Pair MakePair(int a, float b)
{
    struct Pair p;
    p.a = a;
    p.b = b;
    p.c = "c" + IntToString(a);
    p.v = Vector(b, b * 2.0, b * 3.0);
    return p;
}

void Report(string k, int n) { PLogI(k, n); }

void Captured(int nParam, string sParam)
{
    int nLocal = nParam * 10;
    DelayCommand(0.1, PLog("cap.param", IntToString(nParam) + sParam + IntToString(nLocal) + IntToString(gCounter)));
    nLocal = -1;
}

void Loops()
{
    int i, sum = 0;
    for (i = 0; i < 10; i++)
    {
        if (i == 2) continue;
        if (i == 7) break;
        sum += i;
    }
    PLogI("for", sum);
    i = 0;
    do { i += 3; if (i > 100) break; } while (i < 20);
    PLogI("do", i);
    int j = 0, k = 0;
    while (j < 5)
    {
        j++;
        int inner = j * 2;
        if (inner == 6) continue;
        k += inner;
    }
    PLogI("while", k);
    string s = "";
    for (i = 0; i < 6; i++)
    {
        switch (i)
        {
            case 0: s += "z"; break;
            case 1:
            case 2: s += "o"; break;
            case LIMIT: s += "l";
            case 4: s += "f"; break;
            default: s += "d";
        }
    }
    PLog("switch", s);
    switch (99) { case 1: s = "bad"; }
    PLog("switch.nomatch", s);
    switch (-2) { case -2: s = "neg"; break; default: s = "def"; }
    PLog("switch.neg", s);
}

void Expressions()
{
    int a = 7, b = 3;
    PLogI("arith", a + b * 2 - (a % b) / 1);
    PLogI("prec.shift", 1 + 2 << 3);
    PLogI("prec.bit", 6 & 3 | 8 ^ 1);
    PLogI("prec.cmp", a > b == 1);
    PLogI("prec.logic", 0 || 1 && 0);
    PLogI("unary", -a + ~b + !0);
    PLogI("ternary.nested", a > 5 ? b > 5 ? 1 : 2 : 3);
    float f = 1.5;
    f *= 2.0;
    f += 1;
    PLog("float.compound", FloatToString(f, 0, 2));
    PLog("float.mixed", FloatToString(a / 2.0 + b, 0, 2));
    a += b; a -= 1; a *= 3; a /= 2; a %= 7; a <<= 2; a >>= 1; a |= 16; a &= 30; a ^= 5;
    PLogI("int.compound", a);
    int c = 5;
    int d = c++ + ++c;
    PLogI("incdec", c * 100 + d);
    gCounter++;
    --gCounter;
    gCounter += 10;
    PLogI("global.incdec", gCounter);
    string s = "a";
    s += "b" + "c";
    PLog("string.concat", s);
    vector v = gVec;
    v.x = 4.0;
    v.z += 1.5;
    v = v * 2.0;
    vector w = v + [1.0, 1.0, 1.0] - gVec;
    PLog("vector", FloatToString(w.x, 0, 1) + "," + FloatToString(w.y, 0, 1) + "," + FloatToString(w.z, 0, 1));
    PLogI("vector.eq", v == v);
    PLogI("vector.ne", v != w);
    int x = x = 9;
    PLogI("selfinit", x);
}

void Structs()
{
    struct Pair p = MakePair(3, 1.5);
    PLog("struct.fields", IntToString(p.a) + p.c + FloatToString(p.b, 0, 1) + FloatToString(p.v.z, 0, 1));
    PLogI("struct.member.call", MakePair(5, 0.5).a);
    PLog("struct.member.vec", FloatToString(MakePair(5, 0.5).v.y, 0, 1));
    struct Pair q = p;
    q.a++;
    q.v.y = 9.0;
    PLogI("struct.copy", p.a * 10 + q.a);
    PLogI("struct.eq", p == q);
    q = p;
    PLogI("struct.eq2", p == q);
    struct Outer o;
    o.p = p;
    o.p.a += 10;
    o.n = o.p.a * 2;
    PLogI("struct.nested", o.n + o.p.a);
    PLog("struct.nested.vec", FloatToString(o.p.v.x, 0, 2));
}

int StartingConditionalLike(int n) { if (n > 0) { return 1; } else { return 0; } }

void Calls()
{
    PLogI("fib", Fib(15));
    PLog("defaults.none", Defaults());
    PLog("defaults.some", Defaults(4, "y"));
    PLog("defaults.all", Defaults(9, "q", 0.5, OBJECT_INVALID, [3.0, 4.0, 5.0]));
    PLogI("returns", StartingConditionalLike(5) + StartingConditionalLike(-5));
    PLog("globals", gName + FloatToString(gVec.z, 0, 1));
    Captured(4, "s");
    int n = 2;
    DelayCommand(0.1, Report("cap.local", n));
    AssignCommand(GetModule(), Report("assign", n + 1));
    PLog("engine.defaults", GetName(GetModule()) + IntToString(GetIsObjectValid(GetFirstObjectInArea())));
    ExecuteScript("cmp_cond");
    PLogI("cond.global", GetLocalInt(GetModule(), "cond"));
}

void main()
{
    DelayCommand(1.0, Loops());
    DelayCommand(1.1, Expressions());
    DelayCommand(1.2, Structs());
    DelayCommand(1.3, Calls());
    DelayCommand(2.0, PDone());
}
