// Footstep gain and falloff (run with only the effects volume up and the stone footstep samples replaced by
// tones: tools/visual/audio_scene.py run footfall --effects 1): a human (footstep type normal) and then a badger
// (a non-humanoid, type soft) run straight away from the player on stone, their positions logged four times a second.
#include "visual_inc"

void Phase(string s) { WriteTimestampedLogEntry("VISUAL|note|footfall|phase|" + s); }

void Track(object o, string sTag, int n)
{
    if (!GetIsObjectValid(o) || n <= 0) return;
    vector v = GetPosition(o);
    WriteTimestampedLogEntry("VISUAL|note|footfall|pos|" + sTag + "|" + FloatToString(v.x, 0, 2) + "|" + FloatToString(v.y, 0, 2));
    DelayCommand(0.25, Track(o, sTag, n - 1));
}

void Runner(string sResRef, string sTag)
{
    Phase(sTag);
    object pc = GetFirstPC();
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetArea(pc), Vector(11.5, 40.0, 0.0), 0.0), FALSE, sTag);
    ChangeToStandardFaction(o, STANDARD_FACTION_MERCHANT);
    SetPlotFlag(o, TRUE);
    WriteTimestampedLogEntry("VISUAL|note|footfall|type|" + sTag + "|" + IntToString(GetFootstepType(o)) + "|surface="
        + IntToString(GetSurfaceMaterial(GetLocation(o))));
    AssignCommand(o, ActionWait(1.0));
    AssignCommand(o, ActionMoveToLocation(Location(GetArea(pc), Vector(76.0, 40.0, 0.0), 0.0), TRUE));
    Track(o, sTag, 80);
}

void Gone(string sTag) { DestroyObject(GetObjectByTag(sTag)); }

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    Phase("enter");
    SetPlotFlag(pc, TRUE);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    DelayCommand(0.5, VCamera(pc, 0.0, 8.0, 60.0));
    DelayCommand(2.0, Runner("ff_man", "FF_MAN"));
    DelayCommand(4.0, VShot("man"));
    DelayCommand(23.0, Gone("FF_MAN"));
    DelayCommand(24.0, Runner("ff_badger", "FF_BADGER"));
    DelayCommand(26.0, VShot("badger"));
    DelayCommand(46.0, Gone("FF_BADGER"));
    DelayCommand(47.0, VDone());
}
