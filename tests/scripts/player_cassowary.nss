// Player package: cassowary constraint solver.
void main()
{
    object m = GetModule();
    cassowary c;
    SetLocalString(m, "err1", CassowaryConstrain(c, "left + width == right"));
    CassowaryConstrain(c, "left >= 10");
    CassowaryConstrain(c, "width == 2 * 50");
    CassowaryConstrain(c, "right <= 500");
    CassowaryConstrain(c, "left == 0", CASSOWARY_STRENGTH_WEAK);
    SetLocalFloat(m, "left", CassowaryGetValue(c, "left"));
    SetLocalFloat(m, "right", CassowaryGetValue(c, "right"));
    CassowarySuggestValue(c, "left", 200.0);
    SetLocalFloat(m, "left2", CassowaryGetValue(c, "left"));
    SetLocalFloat(m, "right2", CassowaryGetValue(c, "right"));
    SetLocalString(m, "err_bad", CassowaryConstrain(c, "left * right == 4"));
    SetLocalString(m, "err_unsat", CassowaryConstrain(c, "right >= 1000"));
    SetLocalCassowary(m, "solver", c);
    cassowary d = GetLocalCassowary(m, "solver");
    SetLocalFloat(m, "shared", CassowaryGetValue(d, "width"));
    CassowaryReset(c);
    SetLocalFloat(m, "reset", CassowaryGetValue(d, "width"));
}
