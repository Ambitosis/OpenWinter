// Close-up of a light mace and a morning star: idle, walking, then attacking a dummy with
// several attacks a round, so the swing and the morning star's chain show. Shots every 0.4 s while attacking.
#include "weaponstance_inc"
void Walk(int far)
{
    int i;
    for (i = 0; i < WSN(); i++)
    {
        vector v = WSStart(i); if (far) v.y -= 3.0;
        AssignCommand(WSA(i), ClearAllActions());
        AssignCommand(WSA(i), ActionMoveToLocation(WSAt(v, far ? 270.0 : 90.0), FALSE));
    }
}
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    VBegin(pc); VHidePC(pc);
    SetLocalInt(GetModule(), "ws_single", 1);
    vector p = GetPosition(pc);
    SetLocalLocation(GetModule(), "ws_pc", Location(GetArea(pc), Vector(p.x + 4.3, p.y, p.z), 90.0));
    DelayCommand(0.2, WSSpawn(0, "nw_bandit001,nw_wblml001,,"));
    DelayCommand(0.2, WSSpawn(1, "nw_bandit001,nw_wblms001,,"));
    SetLocalInt(GetModule(), "ws_n", 2);
    DelayCommand(0.8, WSArm(0)); DelayCommand(0.8, WSArm(1));
    DelayCommand(1.0, VCamera(pc, 90.0, 4.5, 70.0));
    DelayCommand(4.5, WSReport());
    DelayCommand(5.0, VShot("a0_idle"));
    DelayCommand(6.0, Walk(TRUE));
    DelayCommand(7.0, VShot("a1_walk"));
    DelayCommand(7.6, VShot("a2_walk"));
    DelayCommand(9.0, Walk(FALSE));
    DelayCommand(12.0, WSDummies());
    DelayCommand(12.5, WSManyAttacks(TRUE));
    DelayCommand(13.0, WSActorsAttack());
    float t = 15.0; int k;
    for (k = 0; k < 12; k++) DelayCommand(t + IntToFloat(k) * 0.4, VShot("b_attack_" + (k < 10 ? "0" : "") + IntToString(k)));
    DelayCommand(21.0, VDone());
}
