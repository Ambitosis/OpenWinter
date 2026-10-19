// VM conformance: results are written to module locals and checked by ServerTests.
int gCounter = 10;
string gName = "glob";
struct Pair { int a; float b; string c; vector v; };

struct Pair MakePair(int a) { struct Pair p; p.a = a; p.b = a * 1.5; p.c = "p" + IntToString(a); p.v = Vector(1.0, 2.0, 3.0) * IntToFloat(a); return p; }
int Fib(int n) { if (n < 2) return n; return Fib(n - 1) + Fib(n - 2); }
void Bump(object o, int by) { gCounter += by; SetLocalInt(o, "bumped", gCounter); }

void main()
{
    object m = GetModule();
    SetLocalInt(m, "add", 7 + 5 * 3 - 4 / 2);
    SetLocalInt(m, "mod", -17 % 5);
    SetLocalInt(m, "shift", (1 << 10) | (256 >> 4) ^ 3);
    SetLocalFloat(m, "float", 1.5 * 4.0 - 0.25 / 0.5);
    SetLocalString(m, "str", GetStringUpperCase(GetSubString("hello world", 6, 5)) + IntToString(FindSubString("abcabc", "ca")));
    int i; int sum = 0;
    for (i = 1; i <= 100; i++) { if (i % 2 == 0) continue; sum += i; if (i > 90) break; }
    SetLocalInt(m, "loop", sum);
    int w = 0; while (w < 5) w++; do { w += 10; } while (w < 30);
    SetLocalInt(m, "while", w);
    switch (sum % 7) { case 0: SetLocalInt(m, "switch", 100); break; case 1: SetLocalInt(m, "switch", 101); break; default: SetLocalInt(m, "switch", 199); }
    struct Pair p = MakePair(4);
    SetLocalString(m, "struct", p.c + "/" + FloatToString(p.b, 0, 1) + "/" + FloatToString(p.v.z, 0, 1));
    SetLocalInt(m, "fib", Fib(15));
    SetLocalInt(m, "logic", (1 && 0) + (0 || 2) * 2 + !0 * 4 + (~5 & 0xF));
    SetLocalInt(m, "valid", GetIsObjectValid(OBJECT_INVALID) * 10 + GetIsObjectValid(m));
    SetLocalString(m, "global", gName + IntToString(gCounter));
    location l = Location(OBJECT_INVALID, Vector(1.0, 2.0, 0.0), 90.0);
    SetLocalFloat(m, "locfacing", GetFacingFromLocation(l));
    int captured = 42;
    DelayCommand(0.5, SetLocalInt(m, "delayed", captured));
    DelayCommand(1.0, Bump(m, 5));
    AssignCommand(m, SetLocalString(OBJECT_SELF, "assigned", GetTag(OBJECT_SELF)));
    SetLocalInt(m, "done", 1);
}
