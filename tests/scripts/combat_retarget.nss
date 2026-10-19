// End of a combat round: counts it and goes back to the stored foe, like an AI that keeps picking a body that just died.
void main()
{
    SetLocalInt(OBJECT_SELF, "end_rounds", GetLocalInt(OBJECT_SELF, "end_rounds") + 1);
    ActionAttack(GetLocalObject(OBJECT_SELF, "foe"));
}
