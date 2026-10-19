// Skills package test: OBJECT_SELF uses the skill described by its locals.
void main()
{
    object oTarget = GetLocalObject(OBJECT_SELF, "skill_target");
    object oItem = GetLocalObject(OBJECT_SELF, "skill_item");
    ActionUseSkill(GetLocalInt(OBJECT_SELF, "skill"), oTarget, GetLocalInt(OBJECT_SELF, "skill_sub"), oItem);
    SetLocalInt(OBJECT_SELF, "action", GetCurrentAction());
}
