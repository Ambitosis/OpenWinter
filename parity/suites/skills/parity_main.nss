// GetHasSkill and base skill ranks of standard creatures, every skill.
#include "parity_inc"
void Go()
{
    string s = "nw_bandit001,nw_dog,nw_oldman,nw_goblina,nw_skeleton,nw_orca,nw_kobold001";
    int n = 0, e, k;
    while (n < GetStringLength(s))
    {
        e = FindSubString(s, ",", n); if (e < 0) e = GetStringLength(s);
        string r = GetSubString(s, n, e - n);
        object o = CreateObject(OBJECT_TYPE_CREATURE, r, PCentre());
        string h = "", rk = "";
        for (k = 0; k < 30; k++)
        {
            h += IntToString(GetHasSkill(k, o));
            rk += IntToString(GetSkillRank(k, o, TRUE)) + ",";
        }
        PLog("has." + r, h);
        PLog("base." + r, rk);
        PLog("class." + r, IntToString(GetClassByPosition(1, o)) + "/" + IntToString(GetClassByPosition(2, o)) + " race " + IntToString(GetRacialType(o)));
        n = e + 1;
    }
}
void main() { DelayCommand(1.0, Go()); DelayCommand(2.0, PDone()); }
