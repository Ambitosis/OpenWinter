// What starts battle music (run with only the music volume up: tools/visual/audio_scene.py run musicbattle2 --music 1).
// musicbattle showed that a plot hostile attacking the plot player does not. Here: scripted battle music with a long
// MusicDelay (when the background comes back), then three fights, each ended by the foe's death and followed by
// a wait: (A) the player attacks a scriptless foe, (B) a scriptless foe attacks the (non-plot, immortal) player,
// (C) a goblin with its stock scripts notices and attacks her. The player's GetIsInCombat is logged every second.
#include "visual_inc"

void Phase(string s) { WriteTimestampedLogEntry("VISUAL|note|musicbattle2|phase|" + s); }

void Watch(int n)
{
    if (n <= 0) return;
    WriteTimestampedLogEntry("VISUAL|note|musicbattle2|combat|" + IntToString(GetIsInCombat(GetFirstPC())));
    DelayCommand(1.0, Watch(n - 1));
}

void Battle(int bPlay)
{
    object a = GetArea(GetFirstPC());
    if (bPlay) { Phase("battle_script"); MusicBattlePlay(a); }
    else { Phase("battle_script_stop"); MusicBattleStop(a); }
}

object Foe(string sResRef, string sTag, float fDx)
{
    object pc = GetFirstPC();
    vector v = GetPosition(pc);
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetArea(pc), Vector(v.x + fDx, v.y, v.z), 180.0), FALSE, sTag);
    ChangeToStandardFaction(o, STANDARD_FACTION_HOSTILE);
    return o;
}

void PcAttacks()
{
    Phase("pc_attacks");
    object o = Foe("mb2_foe", "FOE_A", 2.5);
    AssignCommand(GetFirstPC(), ActionAttack(o));
}

void FoeAttacks()
{
    Phase("foe_attacks");
    object pc = GetFirstPC();
    AssignCommand(pc, ClearAllActions(TRUE));
    SetPlotFlag(pc, FALSE);
    SetImmortal(pc, TRUE);
    object o = Foe("mb2_foe", "FOE_B", 2.5);
    AssignCommand(o, ActionAttack(pc));
}

void StockGoblin()
{
    Phase("stock_goblin");
    Foe("nw_goblina", "FOE_C", 10.0);
}

void Kill(string sTag)
{
    Phase("kill");
    object o = GetObjectByTag(sTag);
    AssignCommand(GetFirstPC(), ClearAllActions(TRUE));
    SetPlotFlag(o, FALSE);
    SetImmortal(o, FALSE);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), o);
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    Phase("enter");
    SetPlotFlag(pc, TRUE);
    DelayCommand(0.5, VCamera(pc, 90.0, 10.0, 60.0));
    DelayCommand(1.0, Watch(180));
    DelayCommand(3.0, Battle(TRUE));
    DelayCommand(11.0, Battle(FALSE));
    DelayCommand(30.0, PcAttacks());
    DelayCommand(33.0, VShot("a"));
    DelayCommand(45.0, Kill("FOE_A"));
    DelayCommand(80.0, FoeAttacks());
    DelayCommand(83.0, VShot("b"));
    DelayCommand(95.0, Kill("FOE_B"));
    DelayCommand(130.0, StockGoblin());
    DelayCommand(135.0, VShot("c"));
    DelayCommand(145.0, Kill("FOE_C"));
    DelayCommand(182.0, VDone());
}
