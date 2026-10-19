using OpenWinter.Aurora.Formats;

namespace OpenWinter.Game.Objects;

/// <summary>A class and level a creature has (ClassList entry).</summary>
public sealed class CreatureClass
{
    public int Class;
    public int Level;
    /// <summary>Known spells by spell level (spontaneous casters, wizard spellbooks).</summary>
    public readonly List<int>[] KnownSpells = Enumerable.Range(0, 10).Select(_ => new List<int>()).ToArray();
    /// <summary>Memorised spells by spell level: (spell id, metamagic, ready).</summary>
    public readonly List<(int Spell, int Meta, bool Ready)>[] Memorized = Enumerable.Range(0, 10).Select(_ => new List<(int, int, bool)>()).ToArray();
    public int Domain1 = -1, Domain2 = -1, School = -1;
}

/// <summary>Rules data for creatures (from UTC/BIC fields). Derived values (AC, saves, attack) are computed by the rules layer.</summary>
public sealed partial class Creature
{
    /// <summary>Base ability scores: STR, DEX, CON, INT, WIS, CHA (ABILITY_* order).</summary>
    public readonly int[] Abilities = { 10, 10, 10, 10, 10, 10 };
    public readonly List<CreatureClass> Classes = new();
    /// <summary>Skill ranks indexed by skills.2da row.</summary>
    public int[] Skills = new int[32];
    public readonly List<int> Feats = new();
    /// <summary>Remaining uses of limited-use feats (feat id to uses).</summary>
    public readonly Dictionary<int, int> FeatUses = new();
    public int LawfulChaotic = 50, GoodEvil = 50;
    public int Gold, Experience;
    public int NaturalAC;
    public int FortBonus, RefBonus, WillBonus;
    public int Age;
    public string Subrace = "", Deity = "";
    public int SoundSet;
    public int Size = 3;
    public float ChallengeRating;
    public bool Disarmable = true, Lootable, IsDestroyable = true, IsRaiseable = true, NoPermDeath, Interruptable = true;
    public int DecayTime = 5000;
    public bool SpectacularDeath;
    public int BodyBag;
    public int PerceptionRange = 11;
    public int MovementRateIndex;
    /// <summary>SetBaseAttackBonus override, or -1.</summary>
    public int BaseAttackOverride = -1;
    /// <summary>The level SetAILevel locked (AI_LEVEL_VERY_LOW..HIGH), or -1 for the automatic level.</summary>
    public int AILevel = -1;
    /// <summary>
    /// Runtime: the AI level the creature was last seen at (-2 before any) and when it joined it, in
    /// <see cref="World.NoteAILevel"/>'s count. Creatures due in the same frame are served by level, then in the order
    /// they joined it (nwserver sched_percept).
    /// </summary>
    public int AILevelNow = -2;
    public long AILevelJoin;
    /// <summary>Master of an associate (henchman, summon, familiar, companion, dominated).</summary>
    public uint Master = GameObject.InvalidId;
    public int AssociateType;
    public bool StealthMode, DetectMode;
    /// <summary>
    /// Runtime: the creature has cast a spell or been hurt, so GetDetectMode reports 2 (measured on nwserver,
    /// stealth_modes). Damage takes effect at the creature's next perception update (DetectAlertPending).
    /// </summary>
    public bool DetectAlert, DetectAlertPending;
    /// <summary>
    /// Runtime: standing in the plain idle stance (an observer standing idle out of combat counts as in detect mode).
    /// A creature placed in the toolset or loaded from a save starts in it; one made while the module runs
    /// (CreateObject, encounters) starts in the combat-ready stance and keeps it until it first comes to a standing
    /// stop (the end of a move, a jump), when it takes the idle stance out of combat or the combat-ready one in it
    /// (nwserver stealth_check_stance).
    /// </summary>
    public bool IdleStance = true;
    public int TotalLevel => Classes.Sum(c => c.Level);

    /// <summary>Fills rules fields from a UTC/BIC struct.</summary>
    public void LoadStats(GffStruct s, TwoDA classes)
    {
        string[] names = { "Str", "Dex", "Con", "Int", "Wis", "Cha" };
        for (int i = 0; i < 6; i++) Abilities[i] = (int)s.GetInt(names[i], 10);
        Classes.Clear();
        foreach (var c in s.GetList("ClassList"))
        {
            var cc = new CreatureClass { Class = (int)c.GetInt("Class"), Level = (int)c.GetInt("ClassLevel") };
            // Native defaults omitted domain fields independently, but ignores
            // stored selections for classes that do not pick domains. Keep those
            // classes internally unselected so they gain no domain spell access.
            if (classes.GetInt(cc.Class, "PickDomains") == 1)
            {
                cc.Domain1 = (int)c.GetInt("Domain1", 0);
                cc.Domain2 = (int)c.GetInt("Domain2", 1);
            }
            cc.School = (int)c.GetInt("School", -1);
            for (int lvl = 0; lvl < 10; lvl++)
            {
                foreach (var k in c.GetList($"KnownList{lvl}")) cc.KnownSpells[lvl].Add((int)k.GetInt("Spell"));
                foreach (var m in c.GetList($"MemorizedList{lvl}"))
                    cc.Memorized[lvl].Add(((int)m.GetInt("Spell"), (int)m.GetInt("SpellMetaMagic"), m.GetInt("Ready", 1) != 0));
            }
            Classes.Add(cc);
        }
        var skills = s.GetList("SkillList");
        Skills = new int[Math.Max(32, skills.Count)];
        for (int i = 0; i < skills.Count; i++) Skills[i] = (sbyte)skills[i].GetInt("Rank");
        Feats.Clear();
        foreach (var f in s.GetList("FeatList")) Feats.Add((int)f.GetInt("Feat"));
        // A memorised spell with a metamagic the creature has no feat for is not prepared at all (nwserver
        // ai_talent_ties: no talent and GetHasSpell 0). The slot stays, empty, so slot indices keep their places.
        foreach (var cc in Classes)
            foreach (var list in cc.Memorized)
                for (int i = 0; i < list.Count; i++)
                    if (list[i].Meta != 0 && list[i].Spell >= 0 && Spells.SpellSlots.MetaFeat(list[i].Meta) is int mf && (mf < 0 || !Feats.Contains(mf)))
                        list[i] = (-1, 0, false);
        LawfulChaotic = (int)s.GetInt("LawfulChaotic", 50);
        GoodEvil = (int)s.GetInt("GoodEvil", 50);
        Gold = (int)s.GetInt("Gold");
        Experience = (int)s.GetInt("Experience");
        NaturalAC = (int)s.GetInt("NaturalAC");
        FortBonus = (int)s.GetInt("fortbonus", s.GetInt("FortBonus"));
        RefBonus = (int)s.GetInt("refbonus", s.GetInt("RefBonus"));
        WillBonus = (int)s.GetInt("willbonus", s.GetInt("WillBonus"));
        Age = (int)s.GetInt("Age");
        Subrace = s.GetString("Subrace");
        Deity = s.GetString("Deity");
        SoundSet = (int)s.GetInt("SoundSetFile");
        Size = (int)s.GetInt("CreatureSize", 3);
        ChallengeRating = s.GetFloat("ChallengeRating");
        Disarmable = s.GetInt("Disarmable", 1) != 0;
        Lootable = s.GetInt("Lootable") != 0;
        IsDestroyable = s.GetInt("IsDestroyable", 1) != 0;
        IsRaiseable = s.GetInt("IsRaiseable", Lootable ? 0 : 1) != 0;
        NoPermDeath = s.GetInt("NoPermDeath") != 0;
        Interruptable = s.GetInt("Interruptable", 1) != 0;
        DecayTime = (int)s.GetInt("DecayTime", 5000);
        BodyBag = (int)s.GetInt("BodyBag");
        PerceptionRange = (int)s.GetInt("PerceptionRange", 11);
        MovementRateIndex = (int)s.GetInt("WalkRate", s.GetInt("MovementRate"));
        if (s.GetInt("OverrideBAB") != 0) BaseAttackOverride = (int)s.GetInt("BaseAttackBonus");
        StealthMode = s.GetInt("StealthMode") != 0;
        DetectMode = s.GetInt("DetectMode") != 0;
    }
}
