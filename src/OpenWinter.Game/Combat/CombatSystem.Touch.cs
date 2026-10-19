using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Combat;

/// <summary>Touch attacks (TouchAttackMelee / TouchAttackRanged) for spells.</summary>
public sealed partial class CombatSystem
{
    /// <summary>
    /// A scripted touch attack: BAB + ability + effects against touch AC (size, deflection and negative
    /// Dexterity only), with concealment and no weapon-combat situation modifiers. Returns 0 miss, 1 hit, 2 critical (natural 20 confirmed).
    /// </summary>
    public int TouchAttack(Creature attacker, GameObject target, bool ranged, bool feedback)
    {
        var a = Rules.Context(attacker, target, null, false, Dice);
        a.Touch = true;
        a.Ranged = ranged;
        Rules.Prepare(a);
        int ab = Rules.AttackBonus(a);
        int ac = Rules.ArmorClass(a);
        int roll = Dice.Roll(20);
        bool hit = roll == 20 || (roll != 1 && roll + ab >= ac);
        string result = hit ? "*hit*" : "*miss*";
        if (hit && Rules.Concealment(a) is var pct and > 0)
        {
            int r = Dice.Roll(100);
            if (r <= pct && HasFeat(attacker, CombatK.FeatBlindFight)) r = Dice.Roll(100);
            if (r <= pct) { hit = false; result = $"*target concealed: {pct}%*"; }
        }
        int outcome = hit ? 1 : 0;
        if (hit && roll == 20 && !Rules.IsCriticalImmune(a))
        {
            int confirm = Dice.Roll(20);
            if (confirm == 20 || confirm + ab >= ac) { outcome = 2; result = "*critical hit*"; }
        }
        if (feedback)
        {
            var line = $"{Name(attacker)} attempts {(ranged ? "Touch Attack (Ranged)" : "Touch Attack (Melee)")} on {Name(target)} : {result} : ({roll} + {ab} = {roll + ab})";
            foreach (var who in new[] { attacker, target as Creature }.Distinct())
                if (who is { IsPC: true }) _world.Output?.SendMessage(who, line);
        }
        return outcome;
    }
}
