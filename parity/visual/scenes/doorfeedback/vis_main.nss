#include "visual_inc"
void Mark(string label) { WriteTimestampedLogEntry("VISUAL|note|door_feedback|" + label); }
void Open(object pc, object door) { Mark("open_key_required"); AssignCommand(pc, ActionOpenDoor(door)); }
void Unlock(object pc, object door) { Mark("unlock_key_required"); AssignCommand(pc, ActionUnlockObject(door)); }
void Plain(object pc, object door) { Mark("open_plain_locked"); SetLockKeyRequired(door, FALSE); AssignCommand(pc, ActionOpenDoor(door)); }
void Custom(object pc, object door) { Mark("unlock_custom_feedback"); SetLockKeyRequired(door, TRUE); SetKeyRequiredFeedback(door, "Fixture key required."); AssignCommand(pc, ActionUnlockObject(door)); }
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    object door = GetObjectByTag("M1Q0A_M1Q0B");
    // Source-area visual fixtures clear hooks; restore this door's installed callback.
    SetEventScript(door, 10014, "df_fail");
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    DelayCommand(2.0, VCamera(pc, 90.0, 10.0, 60.0));
    DelayCommand(4.0, VShot("a_before"));
    DelayCommand(6.0, Open(pc, door));
    DelayCommand(7.0, VShot("b_open_key_required"));
    DelayCommand(16.0, Unlock(pc, door));
    DelayCommand(17.0, VShot("c_unlock_key_required"));
    DelayCommand(28.0, Plain(pc, door));
    DelayCommand(29.0, VShot("d_open_plain_locked"));
    DelayCommand(39.0, Custom(pc, door));
    DelayCommand(40.0, VShot("e_unlock_custom"));
    DelayCommand(50.0, Open(pc, door));
    DelayCommand(51.0, VShot("f_open_custom"));
    DelayCommand(58.0, VDone());
}
