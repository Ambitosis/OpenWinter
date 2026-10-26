// nwnx_reveal: NWNX_Reveal on NPC observers (scripts stripped, so nobody fights). Hiders far beyond the observers'
// Spot and Listen stand 3 m from them; RevealTo (seen or heard) and SetRevealToParty change what each observer
// perceives, until the hider stealths again. H1/H2 are revealed to O1, H3/H4 to their party (O2 is only a faction
// mate), H5 is the control.
// Also NWNX_Visibility's stored overrides as read back.
#include "pstealth_inc"
#include "nwnx_reveal"
#include "nwnx_visibility"

object Hider(float x, float y, int nFaction, string sTag)
{
    object h = CreateObject(OBJECT_TYPE_CREATURE, "nr_bandit", Location(GetObjectByTag("PARITY_AREA"), Vector(x, y, 0.0), 0.0), FALSE, sTag);
    ChangeToStandardFaction(h, nFaction);
    SetAILevel(h, AI_LEVEL_NORMAL);
    PsSkill(h, SKILL_HIDE, 80);
    PsSkill(h, SKILL_MOVE_SILENTLY, 80);
    SetActionMode(h, ACTION_MODE_STEALTH, TRUE);
    return h;
}

object Observer(float x, float y, int nFaction, string sTag)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nr_bandit", Location(GetObjectByTag("PARITY_AREA"), Vector(x, y, 0.0), 0.0), FALSE, sTag);
    ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

void Read(string p)
{
    object o1 = GetObjectByTag("O1"), o2 = GetObjectByTag("O2");
    string s = "";
    int i;
    for (i = 1; i <= 5; i++)
    {
        object h = GetObjectByTag("H" + IntToString(i));
        s += " h" + IntToString(i) + "=" + IntToString(GetObjectSeen(h, o1)) + IntToString(GetObjectHeard(h, o1))
            + "/" + IntToString(GetObjectSeen(h, o2)) + IntToString(GetObjectHeard(h, o2))
            + (GetActionMode(h, ACTION_MODE_STEALTH) ? "s" : "-");
    }
    PLog(p, s);
}

void Reveal()
{
    object o1 = GetObjectByTag("O1");
    NWNX_Reveal_RevealTo(GetObjectByTag("H1"), o1, NWNX_REVEAL_SEEN);
    NWNX_Reveal_RevealTo(GetObjectByTag("H2"), o1, NWNX_REVEAL_HEARD);
    NWNX_Reveal_SetRevealToParty(GetObjectByTag("H3"), TRUE, NWNX_REVEAL_SEEN);
    NWNX_Reveal_SetRevealToParty(GetObjectByTag("H4"), TRUE, NWNX_REVEAL_HEARD);
}

void Restealth()
{
    object h1 = GetObjectByTag("H1"), h3 = GetObjectByTag("H3");
    SetActionMode(h1, ACTION_MODE_STEALTH, FALSE);
    SetActionMode(h3, ACTION_MODE_STEALTH, FALSE);
    DelayCommand(0.5, SetActionMode(h1, ACTION_MODE_STEALTH, TRUE));
    DelayCommand(0.5, SetActionMode(h3, ACTION_MODE_STEALTH, TRUE));
}

void main()
{
    // O1 hostile to every hider (Prelude's Hostile vs Commoner); O2 is a Commoner, a faction mate of H3..H5.
    object o1 = Observer(10.0, 20.0, STANDARD_FACTION_HOSTILE, "O1");
    object o2 = Observer(30.0, 20.0, STANDARD_FACTION_COMMONER, "O2");
    Hider(13.0, 20.0, STANDARD_FACTION_COMMONER, "H1");
    Hider(10.0, 23.0, STANDARD_FACTION_COMMONER, "H2");
    Hider(33.0, 20.0, STANDARD_FACTION_COMMONER, "H3");
    Hider(30.0, 23.0, STANDARD_FACTION_COMMONER, "H4");
    Hider(27.0, 20.0, STANDARD_FACTION_COMMONER, "H5");
    PLogI("enemy.o1h1", GetIsEnemy(GetObjectByTag("H1"), o1));
    DelayCommand(5.0, Read("before"));
    DelayCommand(5.5, Reveal());
    DelayCommand(10.5, Read("revealed"));
    DelayCommand(11.0, Restealth());
    DelayCommand(16.5, Read("restealthed"));

    // Visibility overrides as stored.
    object v = GetObjectByTag("O2");
    PLogI("vis.default", NWNX_Visibility_GetVisibilityOverride(OBJECT_INVALID, v));
    NWNX_Visibility_SetVisibilityOverride(OBJECT_INVALID, v, NWNX_VISIBILITY_HIDDEN);
    PLogI("vis.global", NWNX_Visibility_GetVisibilityOverride(OBJECT_INVALID, v));
    NWNX_Visibility_SetVisibilityOverride(o1, v, NWNX_VISIBILITY_ALWAYS_VISIBLE);
    PLogI("vis.npcplayer", NWNX_Visibility_GetVisibilityOverride(o1, v));
    NWNX_Visibility_SetVisibilityOverride(OBJECT_INVALID, v, 9);
    PLogI("vis.global9", NWNX_Visibility_GetVisibilityOverride(OBJECT_INVALID, v));
    NWNX_Visibility_SetVisibilityOverride(OBJECT_INVALID, v, NWNX_VISIBILITY_DEFAULT);
    PLogI("vis.cleared", NWNX_Visibility_GetVisibilityOverride(OBJECT_INVALID, v));
    PLogI("vis.invalidtarget", NWNX_Visibility_GetVisibilityOverride(OBJECT_INVALID, OBJECT_INVALID));
    DelayCommand(17.0, PDone());
}
