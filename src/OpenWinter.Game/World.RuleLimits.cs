namespace OpenWinter.Game;

/// <summary>Bonus caps (Get/Set*BonusLimit) and the module XP scale.</summary>
public sealed class RuleLimits
{
    public int AbilityBonus = 12, AbilityPenalty = 30, AttackBonus = 20, DamageBonus = 100, SavingThrowBonus = 20, SkillBonus = 50;
    /// <summary>Module XP scale (0-200), from module.ifo Mod_XPScale until a script changes it.</summary>
    public int XPScale = -1;
}

public sealed partial class World
{
    public readonly RuleLimits Limits = new();

    /// <summary>Module XP scale (percent of standard kill XP).</summary>
    public int XPScale
    {
        get => Limits.XPScale >= 0 ? Limits.XPScale : (int)Module.Ifo.GetInt("Mod_XPScale", 10);
        set => Limits.XPScale = Math.Clamp(value, 0, 200);
    }
}
