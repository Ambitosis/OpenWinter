// After the temporary enmity has decayed.
void main()
{
    object m = GetModule();
    SetLocalInt(m, "temp_enemy_later", GetIsEnemy(GetLocalObject(m, "pc2"), OBJECT_SELF));
}
