// Follow-ups to soundobj / musicbattle2 (run with music and effects up: tools/visual/audio_scene.py run audiofollow
// --music 1 --effects 1; stereo ambient tones): the ambient fade on stop and change, a positional sound's falloff
// (min 1 m, max 10 m) with the camera near and far, pitch variation on random sounds, whether a hit that does no
// damage starts battle music, how long battle music holds after the fight with MusicDelay 3 s, and nightfall
// (music, ambience and day/night sound objects). SetTime runs every pending DelayCommand at once, so it comes last.
#include "visual_inc"

void Phase(string s) { WriteTimestampedLogEntry("VISUAL|note|audiofollow|phase|" + s); }
object A() { return GetArea(GetFirstPC()); }
void Move(float x, float y) { VMovePC(GetFirstPC(), x, y, 0.0, 180.0); }
void Cam(float dir, float dist) { VCamera(GetFirstPC(), dir, dist, 60.0); }

void AmbStop() { Phase("amb_stop"); AmbientSoundStop(A()); }
void AmbPlay() { Phase("amb_play"); AmbientSoundPlay(A()); }
void AmbChange(string s, int n) { Phase(s); AmbientSoundChangeDay(A(), n); }

void Sweep(string sCam, float fDist, float d) { Phase("d" + sCam + "_" + FloatToString(d, 0, 1)); Move(20.0 + d, 40.0); }
void Play(string s, string tag) { Phase(s); SoundObjectPlay(GetObjectByTag(tag)); }
void Stop(string s, string tag) { Phase(s); SoundObjectStop(GetObjectByTag(tag)); }

void Watch(int n)
{
    if (n <= 0) return;
    WriteTimestampedLogEntry("VISUAL|note|audiofollow|combat|" + IntToString(GetIsInCombat(GetFirstPC())) + "|hp=" + IntToString(GetCurrentHitPoints(GetFirstPC())));
    DelayCommand(1.0, Watch(n - 1));
}

object Foe(string sTag)
{
    object pc = GetFirstPC();
    vector v = GetPosition(pc);
    object o = CreateObject(OBJECT_TYPE_CREATURE, "af_foe", Location(GetArea(pc), Vector(v.x + 2.5, v.y, v.z), 180.0), FALSE, sTag);
    ChangeToStandardFaction(o, STANDARD_FACTION_HOSTILE);
    return o;
}

void NoDamageFight()
{
    Phase("nodamage_attacks");
    object pc = GetFirstPC();
    Move(40.0, 40.0);
    SetPlotFlag(pc, FALSE);
    SetImmortal(pc, TRUE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectDamageReduction(50, DAMAGE_POWER_PLUS_TWENTY), "AF_DR"), pc);
    object o = Foe("FOE_A");
    AssignCommand(o, ActionAttack(pc));
    DelayCommand(1.0, Watch(60));
}

void Kill(string sTag)
{
    Phase("kill_" + sTag);
    object o = GetObjectByTag(sTag);
    SetImmortal(o, FALSE);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), o);
}

void DamageFight()
{
    Phase("damage_attacks");
    object pc = GetFirstPC();
    effect e = GetFirstEffect(pc);
    while (GetIsEffectValid(e))
    {
        if (GetEffectTag(e) == "AF_DR") RemoveEffect(pc, e);
        e = GetNextEffect(pc);
    }
    object o = Foe("FOE_B");
    AssignCommand(o, ActionAttack(pc));
}

void Night()
{
    Phase("night");
    SetTime(21, 0, 0, 0);
    DelayCommand(1.0, WriteTimestampedLogEntry("VISUAL|note|audiofollow|night|" + IntToString(GetIsNight())));
    DelayCommand(16.0, VDone());
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    Phase("enter");
    SetPlotFlag(pc, TRUE);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    DelayCommand(0.5, Cam(180.0, 8.0));
    DelayCommand(2.0, VShot("start"));
    DelayCommand(4.0, AmbStop());
    DelayCommand(10.0, AmbPlay());
    DelayCommand(14.0, AmbChange("amb_change", 2));
    DelayCommand(22.0, AmbChange("amb_back", 1));

    DelayCommand(27.5, Cam(180.0, 3.0));
    DelayCommand(28.0, Play("fall", "S_FALL"));
    float t = 28.0;
    float d = 0.0;
    while (d < 11.0)
    {
        DelayCommand(t, Sweep("c3", 3.0, d));
        t += 1.2;
        d += d < 9.0 ? 1.0 : 0.75;
    }
    DelayCommand(t, Cam(180.0, 20.0));
    t += 0.3;
    d = 0.0;
    while (d < 11.0)
    {
        DelayCommand(t, Sweep("c20", 20.0, d));
        t += 1.2;
        d += d < 9.0 ? 1.0 : 0.75;
    }
    DelayCommand(t, Stop("fall_stop", "S_FALL"));
    DelayCommand(t, Cam(180.0, 8.0));
    DelayCommand(t + 0.2, Move(40.0, 40.0));

    DelayCommand(62.0, Play("pitch", "S_PITCHPOS"));
    DelayCommand(62.0, SoundObjectPlay(GetObjectByTag("S_PITCHFLAT")));
    DelayCommand(74.0, Stop("pitch_stop", "S_PITCHPOS"));
    DelayCommand(74.0, SoundObjectStop(GetObjectByTag("S_PITCHFLAT")));

    DelayCommand(76.0, NoDamageFight());
    DelayCommand(78.0, VShot("nodamage"));
    DelayCommand(92.0, Kill("FOE_A"));
    DelayCommand(110.0, DamageFight());
    DelayCommand(122.0, Kill("FOE_B"));
    DelayCommand(145.0, Night());
}
