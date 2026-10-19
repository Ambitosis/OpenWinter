// The cave-reverb room (EnvAudio 8): the same click as in the first area (EnvAudio 0).
#include "so_inc"
#include "visual_inc"

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    Phase("env8");
    SoundObjectPlay(GetObjectByTag("S_CLICK2"));
    DelayCommand(1.0, VCamera(pc, 180.0, 8.0, 60.0));
    DelayCommand(8.0, Phase("env8_stop"));
    DelayCommand(8.0, SoundObjectStop(GetObjectByTag("S_CLICK2")));
    DelayCommand(10.0, VDone());
}
