// Run on a transition trigger or door: reports its target.
void main()
{
    object m = GetModule();
    object oTarget = GetTransitionTarget(OBJECT_SELF);
    SetLocalObject(m, "target", oTarget);
    SetLocalString(m, "target_tag", GetTag(oTarget));
}
