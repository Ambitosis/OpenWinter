// Ambient sound and sound objects with pure tones (tones.json; run with only the effects volume up:
// tools/visual/audio_scene.py run soundobj --effects 1): ambient volume setters, stop/play/change,
// distance falloff of a positional sound (min 3 m, max 15 m) and whether the camera or the player hears it,
// a non-positional sound's volume steps, a repeating sound's interval, interval/pitch/volume variation,
// a play-once sound list, day/night/hour-limited sounds and nightfall, and the area's EnvAudio reverb.
#include "visual_inc"
#include "so_inc"

object Snd(string s) { return GetObjectByTag(s); }
object A() { return GetArea(GetFirstPC()); }
void Move(float x, float y) { VMovePC(GetFirstPC(), x, y, 0.0, 180.0); }
void Cam(float dir, float dist) { VCamera(GetFirstPC(), dir, dist, 60.0); }

void AmbVol(string s, int n) { Phase(s); AmbientSoundSetDayVolume(A(), n); }
void AmbStop() { Phase("amb_stop"); AmbientSoundStop(A()); }
void AmbPlay() { Phase("amb_play"); AmbientSoundPlay(A()); }
void AmbChange(string s, int n) { Phase(s); AmbientSoundChangeDay(A(), n); }

void Dist(float d) { Phase("d_" + FloatToString(d, 0, 1)); Move(20.0 + d, 40.0); }
void FallStart() { SoundObjectPlay(Snd("S_FALL")); }
void CamPhase(string s, float dir, float dist) { Phase(s); Cam(dir, dist); }
void FallStop() { Phase("fall_stop"); SoundObjectStop(Snd("S_FALL")); Move(40.0, 40.0); Cam(180.0, 8.0); }

void FlatVol(string s, int n) { Phase(s); SoundObjectSetVolume(Snd("S_FLAT"), n); }
void FlatPlay() { Phase("flat_127"); SoundObjectPlay(Snd("S_FLAT")); }
void FlatMove() { Phase("flat_near"); Move(68.0, 70.0); }
void FlatStop() { Phase("flat_stop"); SoundObjectStop(Snd("S_FLAT")); Move(40.0, 40.0); }

void Play(string s, string tag) { Phase(s); SoundObjectPlay(Snd(tag)); }
void Stop(string s, string tag) { Phase(s); SoundObjectStop(Snd(tag)); }

void Night()
{
    Phase("night");
    SetTime(21, 0, 0, 0);
    DelayCommand(1.0, WriteTimestampedLogEntry("VISUAL|note|soundobj|night|" + IntToString(GetIsNight())));
}

void EnvGo()
{
    Phase("env_go");
    SoundObjectStop(Snd("S_CLICK"));
    object pc = GetFirstPC();
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(Location(GetObjectByTag("ENVROOM"), Vector(20.0, 20.0, 0.0), 180.0)));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    if (GetLocalInt(OBJECT_SELF, "STARTED")) return;
    SetLocalInt(OBJECT_SELF, "STARTED", 1);
    Phase("amb_start");
    SetPlotFlag(pc, TRUE);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    DelayCommand(0.5, Cam(180.0, 8.0));
    DelayCommand(2.0, VShot("start"));
    DelayCommand(6.0, AmbVol("amb_50", 50));
    DelayCommand(11.0, AmbVol("amb_100", 100));
    DelayCommand(16.0, AmbVol("amb_127", 127));
    DelayCommand(21.0, AmbStop());
    DelayCommand(25.0, AmbPlay());
    DelayCommand(29.0, AmbChange("amb_change", 2));
    DelayCommand(33.0, AmbChange("amb_back", 1));

    DelayCommand(36.0, FallStart());
    float t = 36.0;
    DelayCommand(t, Dist(0.5)); t += 1.6;
    DelayCommand(t, Dist(2.0)); t += 1.6;
    DelayCommand(t, Dist(3.0)); t += 1.6;
    DelayCommand(t, Dist(4.0)); t += 1.6;
    DelayCommand(t, Dist(6.0)); t += 1.6;
    DelayCommand(t, Dist(8.0)); t += 1.6;
    DelayCommand(t, Dist(11.0)); t += 1.6;
    DelayCommand(t, Dist(14.0)); t += 1.6;
    DelayCommand(t, Dist(16.0)); t += 1.6;
    DelayCommand(t, Dist(20.0)); t += 1.6;
    DelayCommand(t, Dist(30.0)); t += 1.6;
    DelayCommand(t, Dist(6.0)); // 53.6: the camera tests, 6 m from the sound
    DelayCommand(54.5, CamPhase("cam_near", 180.0, 3.0));
    DelayCommand(56.5, CamPhase("cam_far", 180.0, 25.0));
    DelayCommand(58.5, CamPhase("cam_behind", 0.0, 10.0));
    DelayCommand(60.5, FallStop());

    DelayCommand(61.5, FlatPlay());
    DelayCommand(63.5, FlatVol("flat_96", 96));
    DelayCommand(65.5, FlatVol("flat_64", 64));
    DelayCommand(67.5, FlatVol("flat_32", 32));
    DelayCommand(69.5, FlatVol("flat_0", 0));
    DelayCommand(71.5, FlatVol("flat_127b", 127));
    DelayCommand(73.5, FlatMove());
    DelayCommand(75.5, FlatStop());

    DelayCommand(76.5, Play("int", "S_INT"));
    DelayCommand(86.5, Stop("int_stop", "S_INT"));
    DelayCommand(87.0, Play("vary", "S_VARY"));
    DelayCommand(112.0, Stop("vary_stop", "S_VARY"));
    DelayCommand(112.5, Play("once", "S_ONCE"));
    DelayCommand(119.5, Play("once_again", "S_ONCE"));
    DelayCommand(126.5, Night());
    DelayCommand(134.0, Play("env0", "S_CLICK"));
    DelayCommand(141.0, EnvGo());
}
