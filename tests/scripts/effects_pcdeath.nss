// Module OnPlayerDeath for the effects tests.
void main()
{
    SetLocalString(GetModule(), "pc_died", GetTag(GetLastPlayerDied()));
}
