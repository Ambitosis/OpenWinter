// Hit rates of weapon attacks against a known AC, measured over many attacks: iterative attacks, the
// attack penalties of extra attacks (haste, EffectModifyAttacks), attack effects, and how what the
// target is doing changes things: a target attacking nobody is flat-footed (no Dexterity bonus,
// Uncanny Dodge keeps it), one attacking someone else is flanked (+2), one attacking you is neither.
// Attackers are nw_drowfight010 (BAB 10, +14 with the bastard sword) against nw_bandit001 (AC 12,
// Dex 13) given deflection AC.
#include "pcombat_inc"

const string FIGHTER = "nw_drowfight010";
const string TARGET = "nw_bandit001";

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

// A squad of four attackers with the given attacks per round and extra attacks, around a target with nAC deflection.
object Squad(string g, int nAttacks, int nExtra, int nAC)
{
    object t = PSquad(g, FIGHTER, TARGET, 4);
    PSquadAttacks(t, nAttacks);
    if (nExtra > 0) PSquadEffect(t, EffectModifyAttacks(nExtra));
    if (nExtra < 0) PSquadEffect(t, EffectHaste());
    Perm(t, EffectACIncrease(nAC, AC_DEFLECTION_BONUS));
    return t;
}

// A target with +5 Dexterity AC fighting someone else: a passive dummy behind it.
void FightDummy(object t)
{
    vector v = GetPosition(t);
    object d = CreateObject(OBJECT_TYPE_CREATURE, TARGET, Location(GetArea(t), Vector(v.x + 1.2, v.y + 1.2, 0.0), 0.0));
    PStrip(d);
    SetImmortal(d, TRUE);
    ChangeToStandardFaction(d, STANDARD_FACTION_HOSTILE);
    ChangeToStandardFaction(t, STANDARD_FACTION_DEFENDER);
    DelayCommand(0.5, AssignCommand(t, ActionAttack(d)));
}

void Setup()
{
    int i;
    for (i = 0; i < 3; i++)
    {
        // Iterative attacks: +14/+9/+4 against AC 15 (14 flat-footed).
        Squad("iter3", 3, 0, 3);
        // One attack plus three from EffectModifyAttacks against AC 20: the extra attacks have their own
        // iterative penalties (0, -5, -10).
        Squad("extra3", 1, 3, 8);
        // Haste: one extra attack at full attack bonus.
        Squad("haste", 1, -1, 13);
        // +4 attack, and -3 attack.
        object t = Squad("atkup", 1, 1, 13);
        PSquadEffect(t, EffectAttackIncrease(4));
        t = Squad("atkdown", 1, 1, 8);
        PSquadEffect(t, EffectAttackDecrease(3));
        // Flat-footed: +5 Dexterity AC lost.
        t = Squad("flatfooted", 1, 1, 8);
        Perm(t, EffectAbilityIncrease(ABILITY_DEXTERITY, 10));
        // Uncanny Dodge keeps it.
        t = Squad("uncanny", 1, 1, 8);
        Perm(t, EffectAbilityIncrease(ABILITY_DEXTERITY, 10));
        Perm(t, EffectBonusFeat(FEAT_UNCANNY_DODGE_1));
        // Flanked: attacking someone else.
        t = Squad("flanked", 1, 1, 8);
        Perm(t, EffectAbilityIncrease(ABILITY_DEXTERITY, 10));
        FightDummy(t);
    }
    // Attacking you: neither (pairs, so the target fights its only attacker).
    for (i = 0; i < 12; i++)
    {
        object a = PPair("fighting", FIGHTER, TARGET);
        SetBaseAttackBonus(1, a);
        Perm(a, EffectModifyAttacks(1));
        object t = GetLocalObject(a, "T");
        Perm(t, EffectAbilityIncrease(ABILITY_DEXTERITY, 10));
        Perm(t, EffectACIncrease(8, AC_DEFLECTION_BONUS));
        ChangeToStandardFaction(t, STANDARD_FACTION_DEFENDER);
        SetBaseAttackBonus(1, t);
        DelayCommand(0.5, AssignCommand(t, ActionAttack(a)));
    }
    PStartAll(1.0);
}

void Report()
{
    string s = "iter3 extra3 haste atkup atkdown flatfooted uncanny flanked fighting ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        PReportHits(GetStringLeft(s, sp));
        s = GetSubString(s, sp + 1, 1000);
    }
    PDone();
}

void main()
{
    PClockStart(85.0);
    DelayCommand(1.0, Setup());
    DelayCommand(70.0, Report());
}
