#include "visual_inc"

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    DelayCommand(1.0, VCamera(pc, 20.0, 5.5, 70.0));
    DelayCommand(3.0, VShot("a_flat"));
    DelayCommand(3.2, SoundObjectPlay(GetObjectByTag("flat")));
    DelayCommand(9.0, VShot("b_spatial"));
    DelayCommand(9.2, SoundObjectPlay(GetObjectByTag("spatial")));
    DelayCommand(15.0, VDone());
}
