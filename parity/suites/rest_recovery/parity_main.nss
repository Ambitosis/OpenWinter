// Rest recovery and the enemy check on NPCs (docs/DEATH_AND_DYING.md): what ForceRest and ActionRest give back (hit
// points, memorised spells, feat uses), and whether ActionRest goes ahead with a hostile 25 m or 35 m away
// (ruleset.2da REST_ENEMY_CHECK_DISTANCE is 30). Event scripts are cleared so the AI stays out of it; the rester is
// made a defender, so the goblin (hostile faction) is its enemy.
#include "parity_inc"

object Make(string sResRef, float fX, float fY)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetObjectByTag("PARITY_AREA"), Vector(fX, fY, 0.0), 0.0));
    int i;
    for (i = 5000; i <= 5012; i++) SetEventScript(o, i, "");
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

// The first of these the cleric has memorised.
int Memorised(object o)
{
    int nSpells = 7;
    int i;
    for (i = 0; i < nSpells; i++)
    {
        int n = i == 0 ? SPELL_CURE_LIGHT_WOUNDS : i == 1 ? SPELL_BLESS : i == 2 ? SPELL_SHIELD_OF_FAITH : i == 3 ? SPELL_BARKSKIN
            : i == 4 ? SPELL_CURE_MODERATE_WOUNDS : i == 5 ? SPELL_AID : SPELL_RESISTANCE;
        if (GetHasSpell(n, o) > 0) return n;
    }
    return -1;
}

int TurnUses(object o)
{
    int n = 0;
    while (GetHasFeat(FEAT_TURN_UNDEAD, o) && n < 50) { DecrementRemainingFeatUses(o, FEAT_TURN_UNDEAD); n++; }
    return n;
}

void Report(string sKey, object o, int nSpell)
{
    PLogI(sKey + ".hp", GetCurrentHitPoints(o) == GetMaxHitPoints(o) ? -1 : GetCurrentHitPoints(o));
    PLogI(sKey + ".spell", GetHasSpell(nSpell, o));
    PLogI(sKey + ".turn", GetHasFeat(FEAT_TURN_UNDEAD, o));
    PLogI(sKey + ".resting", GetIsResting(o));
    PLogI(sKey + ".dead", GetIsDead(o));
}

// A cleric loses hit points, one memorised spell (cast for real) and every turn undead use, then rests.
void Recovery(string sKey, float fX, int bAction)
{
    object o = Make("nw_drowcler010", fX, 20.0);
    int nSpell = Memorised(o);
    PLogI(sKey + ".spellid", nSpell);
    PLogI(sKey + ".spell.before", GetHasSpell(nSpell, o));
    AssignCommand(o, ActionCastSpellAtObject(nSpell, o, METAMAGIC_ANY, FALSE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    DelayCommand(3.0, PLogI(sKey + ".spell.cast", GetHasSpell(nSpell, o)));
    DelayCommand(3.0, PLogI(sKey + ".turn.uses", TurnUses(o)));
    DelayCommand(3.1, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(10), o));
    DelayCommand(3.2, PLogI(sKey + ".hp.hurt", GetMaxHitPoints(o) - GetCurrentHitPoints(o)));
    if (bAction) DelayCommand(3.5, AssignCommand(o, ActionRest()));
    else DelayCommand(3.5, ForceRest(o));
    DelayCommand(4.5, Report(sKey + ".after", o, nSpell));
    DelayCommand(5.0, DestroyObject(o));
}

// A hurt bandit rests with a hostile goblin fDistance metres east.
void Range(string sKey, float fY, float fDistance)
{
    object o = Make("nw_bandit001", 20.0, fY);
    object g = Make("nw_goblina", 20.0 + fDistance, fY);
    ChangeToStandardFaction(o, STANDARD_FACTION_DEFENDER);
    SetIsTemporaryEnemy(o, g); SetIsTemporaryEnemy(g, o);
    SetPlotFlag(g, TRUE);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(3), o);
    DelayCommand(1.0, PLogI(sKey + ".hurt", GetMaxHitPoints(o) - GetCurrentHitPoints(o)));
    DelayCommand(1.0, PLogI(sKey + ".enemy", GetIsEnemy(g, o)));
    DelayCommand(1.5, AssignCommand(o, ActionRest()));
    DelayCommand(1.6, PLogI(sKey + ".action", GetCurrentAction(o)));
    DelayCommand(3.0, PLogI(sKey + ".rested", GetCurrentHitPoints(o) == GetMaxHitPoints(o)));
}

void main()
{
    Recovery("force", 20.0, FALSE);
    Recovery("action", 40.0, TRUE);
    // After the clerics are gone: the enemy check, far from everything else.
    DelayCommand(8.0, Range("enemy25", 70.0, 25.0));
    DelayCommand(8.0, Range("enemy35", 105.0, 35.0));
    // A dead creature can't be force-rested back.
    object d = Make("nw_bandit001", 80.0, 20.0);
    SetIsDestroyable(FALSE, TRUE, FALSE, d);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), d);
    DelayCommand(1.0, ForceRest(d));
    DelayCommand(2.0, PLogI("dead.rest.dead", GetIsDead(d)));
    DelayCommand(14.0, PDone());
}
