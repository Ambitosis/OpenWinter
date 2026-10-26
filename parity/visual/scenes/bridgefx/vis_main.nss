// Presentation probe: on entering, a timeline of script animations, conditions, placeable and door animations,
// visual effects, spells, a ranged attack, sounds and music. The player announces each step in chat first
// ("<phase> <what>"), so a recording of the session reads step by step.
#include "visual_inc"

void Mark(object oPC, string s) { AssignCommand(oPC, SpeakString(s)); }

object Actor(object oPC, float fDx, float fDy, string sName)
{
    object o = VSpawn(oPC, "nw_bandit001", fDx, fDy, 270.0);
    int i;
    for (i = 5000; i <= 5012; i++) SetEventScript(o, i, "");
    AssignCommand(o, ClearAllActions(TRUE));
    SetName(o, sName);
    return o;
}

int AnimAt(int i)
{
    if (i <= 31) return i;
    if (i <= 48) return 100 + i - 32;
    switch (i)
    {
        case 49: return 32;
        case 50: return 40;
        case 51: return 43;
        case 52: return 79;
        case 53: return 41;
        case 54: return 42;
    }
    return -1;
}

void Anim(object oPC, object o, int n, float fSpeed, float fDuration)
{
    Mark(oPC, "A " + IntToString(n) + " " + FloatToString(fSpeed, 0, 1) + " " + FloatToString(fDuration, 0, 1));
    AssignCommand(o, ClearAllActions());
    AssignCommand(o, ActionPlayAnimation(n, fSpeed, fDuration));
}

void State(object oPC, object o, string sName, effect e, float fDuration)
{
    Mark(oPC, "S " + sName);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, e, o, fDuration);
}

void Kill(object oPC, object o)
{
    Mark(oPC, "S death");
    SetImmortal(o, FALSE);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), o);
}

void Raise(object oPC, object o)
{
    Mark(oPC, "S raise");
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectResurrection(), o);
}

void PlcAnim(object oPC, object p, string sName, int n)
{
    Mark(oPC, "P " + sName + " " + IntToString(n));
    AssignCommand(p, PlayAnimation(n));
}

void DoorStep(object oPC, object d, string sName)
{
    Mark(oPC, "D " + sName);
    if (sName == "open") AssignCommand(d, ActionOpenDoor(d));
    else if (sName == "close") AssignCommand(d, ActionCloseDoor(d));
    else if (sName == "lock") SetLocked(d, TRUE);
    else if (sName == "unlock") SetLocked(d, FALSE);
    else if (sName == "destroy") { SetPlotFlag(d, FALSE); ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(500), d); }
}

void OpenFrom(object oPC, object o, object d)
{
    Mark(oPC, "D open-by-npc");
    AssignCommand(o, ActionOpenDoor(d));
}

void Fx(object oPC, string sName, object o, int nVfx, int nDurationType, float fDuration)
{
    Mark(oPC, "V " + sName + " " + IntToString(nVfx));
    ApplyEffectToObject(nDurationType, EffectVisualEffect(nVfx), o, fDuration);
}

void FxAt(object oPC, string sName, int nVfx, float fDx, float fDy)
{
    Mark(oPC, "V " + sName + " " + IntToString(nVfx));
    VFxAt(oPC, nVfx, fDx, fDy);
}

void FxPermanent(object oPC, object o)
{
    Mark(oPC, "V permanent 225");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectVisualEffect(VFX_DUR_PROTECTION_GOOD_MINOR), "BFX"), o);
}

void FxRemove(object oPC, object o)
{
    Mark(oPC, "V remove 225");
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        if (GetEffectTag(e) == "BFX") RemoveEffect(o, e);
        e = GetNextEffect(o);
    }
}

void FxOnPlaceable(object oPC, object p)
{
    Mark(oPC, "V placeable 411");
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectVisualEffect(VFX_DUR_GLOW_RED), p, 4.0);
}

void Beam(object oPC, object oFrom, object oTo, float fDuration)
{
    Mark(oPC, "V beam 73 " + FloatToString(fDuration, 0, 1));
    if (fDuration > 0.0) ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectBeam(VFX_BEAM_LIGHTNING, oFrom, BODY_NODE_HAND), oTo, fDuration);
    else ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectBeam(VFX_BEAM_LIGHTNING, oFrom, BODY_NODE_HAND), oTo);
}

void Aoe(object oPC, float fDx, float fDy)
{
    Mark(oPC, "V aoe fogacid");
    vector v = GetPosition(oPC);
    ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY, EffectAreaOfEffect(AOE_PER_FOGACID, "****", "****", "****"),
        Location(GetArea(oPC), Vector(v.x + fDx, v.y + fDy, v.z), 0.0), 8.0);
}

void Transform(object oPC, object o, string sName, int nTransform, float fValue, float fLerp)
{
    Mark(oPC, "T " + sName + " " + FloatToString(fValue, 0, 2));
    SetObjectVisualTransform(o, nTransform, fValue, fLerp > 0.0 ? OBJECT_VISUAL_TRANSFORM_LERP_LINEAR : OBJECT_VISUAL_TRANSFORM_LERP_NONE, fLerp);
}

void Hilite(object oPC, object o, int nColor)
{
    Mark(oPC, "H " + IntToString(nColor));
    SetObjectHiliteColor(o, nColor);
}

void Cast(object oPC, object oCaster, object oTarget, string sName, int nSpell)
{
    Mark(oPC, "C " + sName + " " + IntToString(nSpell));
    AssignCommand(oCaster, ClearAllActions());
    AssignCommand(oCaster, ActionCastSpellAtObject(nSpell, oTarget, METAMAGIC_ANY, TRUE));
}

void CastAt(object oPC, object oCaster, string sName, int nSpell, float fDx, float fDy)
{
    Mark(oPC, "C " + sName + " " + IntToString(nSpell));
    vector v = GetPosition(oPC);
    AssignCommand(oCaster, ClearAllActions());
    AssignCommand(oCaster, ActionCastSpellAtLocation(nSpell, Location(GetArea(oPC), Vector(v.x + fDx, v.y + fDy, v.z), 0.0), METAMAGIC_ANY, TRUE));
}

void Shoot(object oPC, object oArcher, object oTarget)
{
    Mark(oPC, "R shoot");
    SetIsTemporaryEnemy(oTarget, oArcher);
    SetIsTemporaryEnemy(oArcher, oTarget);
    AssignCommand(oArcher, ActionAttack(oTarget));
}

void Cease(object oPC, object oArcher, object oTarget)
{
    Mark(oPC, "R stop");
    AssignCommand(oArcher, ClearAllActions(TRUE));
    SetIsTemporaryFriend(oTarget, oArcher);
    SetIsTemporaryFriend(oArcher, oTarget);
}

void Arm(object oArcher)
{
    object bow = CreateItemOnObject("nw_wbwsh001", oArcher);
    object arrows = CreateItemOnObject("nw_wamar001", oArcher, 99);
    AssignCommand(oArcher, ActionEquipItem(bow, INVENTORY_SLOT_RIGHTHAND));
    AssignCommand(oArcher, ActionEquipItem(arrows, INVENTORY_SLOT_ARROWS));
}

void Sound(object oPC, object o, string sName)
{
    object area = GetArea(oPC);
    object snd = GetObjectByTag("BFX_SOUND");
    Mark(oPC, "N " + sName);
    if (sName == "playsound") AssignCommand(o, PlaySound("as_cv_bell1"));
    else if (sName == "voicechat") PlayVoiceChat(VOICE_CHAT_HELLO, o);
    else if (sName == "object-play") SoundObjectPlay(snd);
    else if (sName == "object-volume") SoundObjectSetVolume(snd, 60);
    else if (sName == "object-position") SoundObjectSetPosition(snd, Vector(18.0, 18.0, 1.5));
    else if (sName == "object-stop") SoundObjectStop(snd);
    else if (sName == "music-change") MusicBackgroundChangeDay(area, 10);
    else if (sName == "music-delay") MusicBackgroundSetDelay(area, 5000);
    else if (sName == "music-play") MusicBackgroundPlay(area);
    else if (sName == "music-stop") MusicBackgroundStop(area);
    else if (sName == "battle-change") MusicBattleChange(area, 3);
    else if (sName == "battle-play") MusicBattlePlay(area);
    else if (sName == "battle-stop") MusicBattleStop(area);
    else if (sName == "ambient-change") AmbientSoundChangeDay(area, 5);
    else if (sName == "ambient-volume") AmbientSoundSetDayVolume(area, 50);
    else if (sName == "ambient-play") AmbientSoundPlay(area);
    else if (sName == "ambient-stop") AmbientSoundStop(area);
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    object a = Actor(pc, -2.0, 3.0, "BFX_A");
    object b = Actor(pc, 2.0, 3.0, "BFX_B");
    object c = Actor(pc, -3.0, -3.0, "BFX_C");
    object d = Actor(pc, 3.0, -3.0, "BFX_D");
    object e = Actor(pc, 9.0, 0.0, "BFX_E");
    object chest = VPlaceable(pc, "plc_chest1", -5.0, 0.0);
    object lever = VPlaceable(pc, "plc_flrlever1", -5.0, 2.0);
    object brazier = VPlaceable(pc, "plc_brazier", -5.0, -2.0);
    object door = GetObjectByTag("BFX_DOOR");
    Arm(c);
    // B takes conditions and D spells: mortal enough for effects, never killed by them.
    SetPlotFlag(b, FALSE);
    SetImmortal(b, TRUE);
    SetPlotFlag(d, FALSE);
    SetImmortal(d, TRUE);
    SetIsTemporaryEnemy(d, c, FALSE);
    SetIsTemporaryEnemy(c, d, FALSE);

    float t = 6.0;
    int i;
    // Animations, one every 2.5 seconds (looping ones for 2 seconds), then some at other speeds and lengths.
    for (i = 0; i <= 54; i++)
    {
        DelayCommand(t, Anim(pc, a, AnimAt(i), 1.0, 2.0));
        t += 2.5;
    }
    DelayCommand(t, Anim(pc, a, ANIMATION_LOOPING_WORSHIP, 2.0, 2.0)); t += 2.5;
    DelayCommand(t, Anim(pc, a, ANIMATION_FIREFORGET_BOW, 0.5, 0.0)); t += 3.0;
    DelayCommand(t, Anim(pc, a, ANIMATION_LOOPING_SIT_CROSS, 1.0, 6.0)); t += 2.0;
    DelayCommand(t, Anim(pc, a, ANIMATION_LOOPING_TALK_NORMAL, 1.0, 10000.0)); t += 3.0;
    DelayCommand(t, Anim(pc, a, ANIMATION_LOOPING_PAUSE, 1.0, 1.0));

    // Conditions on B, while A plays.
    t = 6.0;
    DelayCommand(t, State(pc, b, "knockdown", EffectKnockdown(), 4.0)); t += 6.0;
    DelayCommand(t, State(pc, b, "sleep", EffectSleep(), 4.0)); t += 6.0;
    DelayCommand(t, State(pc, b, "paralyze", EffectParalyze(), 4.0)); t += 6.0;
    DelayCommand(t, State(pc, b, "stun", EffectStunned(), 4.0)); t += 6.0;
    DelayCommand(t, State(pc, b, "daze", EffectDazed(), 4.0)); t += 6.0;
    DelayCommand(t, State(pc, b, "petrify", EffectPetrify(), 4.0)); t += 6.0;
    DelayCommand(t, State(pc, b, "entangle", EffectEntangle(), 4.0)); t += 6.0;
    DelayCommand(t, State(pc, b, "fear", EffectFrightened(), 4.0)); t += 6.0;
    DelayCommand(t, State(pc, b, "cutscene-paralyze", EffectCutsceneParalyze(), 4.0)); t += 6.0;
    DelayCommand(t, Kill(pc, b)); t += 6.0;
    DelayCommand(t, Raise(pc, b)); t += 6.0;

    // Placeables and the door.
    t = 8.0;
    DelayCommand(t, PlcAnim(pc, chest, "chest-open", ANIMATION_PLACEABLE_OPEN)); t += 3.0;
    DelayCommand(t, PlcAnim(pc, chest, "chest-close", ANIMATION_PLACEABLE_CLOSE)); t += 3.0;
    DelayCommand(t, PlcAnim(pc, lever, "lever-activate", ANIMATION_PLACEABLE_ACTIVATE)); t += 3.0;
    DelayCommand(t, PlcAnim(pc, lever, "lever-deactivate", ANIMATION_PLACEABLE_DEACTIVATE)); t += 3.0;
    DelayCommand(t, PlcAnim(pc, brazier, "brazier-deactivate", ANIMATION_PLACEABLE_DEACTIVATE)); t += 3.0;
    DelayCommand(t, PlcAnim(pc, brazier, "brazier-activate", ANIMATION_PLACEABLE_ACTIVATE)); t += 3.0;
    DelayCommand(t, DoorStep(pc, door, "open")); t += 3.0;
    DelayCommand(t, DoorStep(pc, door, "close")); t += 3.0;
    DelayCommand(t, OpenFrom(pc, e, door)); t += 5.0;
    DelayCommand(t, DoorStep(pc, door, "close")); t += 3.0;
    DelayCommand(t, DoorStep(pc, door, "lock")); t += 2.0;
    DelayCommand(t, DoorStep(pc, door, "unlock")); t += 2.0;
    DelayCommand(t, FxOnPlaceable(pc, chest)); t += 5.0;

    // Visual effects, transforms, highlights, spells and a ranged attack on C and D.
    t = 60.0;
    DelayCommand(t, Fx(pc, "instant", c, VFX_IMP_HEALING_S, DURATION_TYPE_INSTANT, 0.0)); t += 3.0;
    DelayCommand(t, FxAt(pc, "location", VFX_FNF_FIREBALL, 0.0, -6.0)); t += 4.0;
    DelayCommand(t, Fx(pc, "temporary", d, VFX_DUR_GLOW_RED, DURATION_TYPE_TEMPORARY, 4.0)); t += 6.0;
    DelayCommand(t, FxPermanent(pc, c)); t += 4.0;
    DelayCommand(t, FxRemove(pc, c)); t += 3.0;
    DelayCommand(t, Beam(pc, c, d, 3.0)); t += 5.0;
    DelayCommand(t, Beam(pc, d, c, 0.0)); t += 3.0;
    DelayCommand(t, Aoe(pc, 0.0, -8.0)); t += 10.0;
    DelayCommand(t, Transform(pc, d, "scale", OBJECT_VISUAL_TRANSFORM_SCALE, 1.6, 0.0)); t += 3.0;
    DelayCommand(t, Transform(pc, d, "rotate", OBJECT_VISUAL_TRANSFORM_ROTATE_Z, 45.0, 1.0)); t += 3.0;
    DelayCommand(t, Transform(pc, d, "scale", OBJECT_VISUAL_TRANSFORM_SCALE, 1.0, 0.0)); t += 1.0;
    DelayCommand(t, Transform(pc, d, "rotate", OBJECT_VISUAL_TRANSFORM_ROTATE_Z, 0.0, 0.0)); t += 2.0;
    DelayCommand(t, Hilite(pc, d, 0xFF0000)); t += 3.0;
    DelayCommand(t, Hilite(pc, d, -1)); t += 2.0;
    DelayCommand(t, Cast(pc, c, d, "missile", SPELL_MAGIC_MISSILE)); t += 7.0;
    DelayCommand(t, Cast(pc, c, d, "flamearrow", SPELL_FLAME_ARROW)); t += 7.0;
    DelayCommand(t, CastAt(pc, c, "fireball", SPELL_FIREBALL, 0.0, -9.0)); t += 7.0;
    DelayCommand(t, Cast(pc, c, d, "lightning", SPELL_LIGHTNING_BOLT)); t += 7.0;
    DelayCommand(t, Cast(pc, d, d, "bless", SPELL_BLESS)); t += 7.0;
    DelayCommand(t, Cast(pc, d, d, "endurance", SPELL_ENDURANCE)); t += 6.0;
    DelayCommand(t, Cast(pc, d, d, "light", SPELL_LIGHT)); t += 6.0;
    DelayCommand(t, Shoot(pc, c, d)); t += 8.0;
    DelayCommand(t, Cease(pc, c, d)); t += 3.0;

    // Sounds and music.
    DelayCommand(t, Sound(pc, a, "playsound")); t += 2.0;
    DelayCommand(t, Sound(pc, a, "voicechat")); t += 2.0;
    DelayCommand(t, Sound(pc, a, "object-play")); t += 2.0;
    DelayCommand(t, Sound(pc, a, "object-volume")); t += 1.0;
    DelayCommand(t, Sound(pc, a, "object-position")); t += 1.0;
    DelayCommand(t, Sound(pc, a, "object-stop")); t += 1.0;
    DelayCommand(t, Sound(pc, a, "music-change")); t += 1.0;
    DelayCommand(t, Sound(pc, a, "music-delay")); t += 1.0;
    DelayCommand(t, Sound(pc, a, "music-play")); t += 1.0;
    DelayCommand(t, Sound(pc, a, "music-stop")); t += 1.0;
    DelayCommand(t, Sound(pc, a, "battle-change")); t += 1.0;
    DelayCommand(t, Sound(pc, a, "battle-play")); t += 1.0;
    DelayCommand(t, Sound(pc, a, "battle-stop")); t += 1.0;
    DelayCommand(t, Sound(pc, a, "ambient-change")); t += 1.0;
    DelayCommand(t, Sound(pc, a, "ambient-volume")); t += 1.0;
    DelayCommand(t, Sound(pc, a, "ambient-play")); t += 1.0;
    DelayCommand(t, Sound(pc, a, "ambient-stop")); t += 1.0;
    DelayCommand(t, DoorStep(pc, door, "destroy")); t += 3.0;
    DelayCommand(t, VDone());
}
