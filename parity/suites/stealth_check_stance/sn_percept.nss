// Observer OnPerception: the shared log, and whether the hider was on the move when the beacon was seen.
#include "pstealth_inc"
void main()
{
    PsOnNotice();
    object me = OBJECT_SELF;
    if (GetLastPerceived() == GetLocalObject(me, "B") && GetLastPerceptionSeen())
        SetLocalInt(me, "HMOV", GetCurrentAction(GetLocalObject(me, "H")) == ACTION_MOVETOPOINT);
}
