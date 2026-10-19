// CPU-heavy benchmark: arithmetic loops, calls, strings.
int Fib(int n) { if (n < 2) return n; return Fib(n - 1) + Fib(n - 2); }
void main()
{
    int i; int j; int acc = 0; float f = 0.0; string s = "";
    for (i = 0; i < 200; i++) { for (j = 0; j < 200; j++) { acc = (acc + i * j) % 100003; f += 0.5; } }
    for (i = 0; i < 300; i++) s = GetStringLeft(s + IntToString(i), 40);
    acc += Fib(20);
    SetLocalInt(GetModule(), "bench", acc);
}
