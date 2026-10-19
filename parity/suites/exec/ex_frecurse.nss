int Deep(int n)
{
    SetLocalInt(GetModule(), "fdepth", n);
    if (n >= 100000) return n;
    return Deep(n + 1) + 0;
}
void main() { Deep(1); }
