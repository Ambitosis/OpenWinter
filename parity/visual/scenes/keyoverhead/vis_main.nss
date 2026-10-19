#include "visual_inc"

void View(object pc, float t, float distance, float pitch, string name)
{
    DelayCommand(t, VCamera(pc, 0.0, distance, pitch));
    DelayCommand(t + 2.0, VShot(name));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCutsceneMode(pc, TRUE, FALSE);
    View(pc, 3.0, 6.0, 0.0, "a_down_close");
    View(pc, 7.0, 12.0, 0.0, "b_down_mid");
    View(pc, 11.0, 22.0, 0.0, "c_down_far");
    View(pc, 15.0, 12.0, 10.0, "d_pitch10");
    View(pc, 19.0, 12.0, 20.0, "e_pitch20");
    View(pc, 23.0, 12.0, 30.0, "f_pitch30");
    View(pc, 27.0, 12.0, 40.0, "g_pitch40");
    View(pc, 31.0, 12.0, 55.0, "h_pitch55");
    DelayCommand(35.0, VDone());
}
