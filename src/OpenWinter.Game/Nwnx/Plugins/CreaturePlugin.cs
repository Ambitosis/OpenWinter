using System.Numerics;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Creature over the creature's rules fields. State the engine has no
/// field for yet (walk animation, damage level override, spell slots, ...)
/// is kept per creature and reported to clients where it is visible.
/// </summary>
static class CreaturePlugin
{
    const string P = "Creature";

    static CreatureClass? ClassOf(Creature? c, int classId) => c?.Classes.FirstOrDefault(k => k.Class == classId);

    static void Changed(NwnxCall n, Creature c) => n.World.MarkChanged(c);

    static int StateInt(NwnxCall n, GameObject o, string key, int fallback = 0) =>
        n.State(o).TryGetValue(key, out var v) && v.Value is int i ? i : fallback;
    static float StateFloat(NwnxCall n, GameObject o, string key, float fallback = 0) =>
        n.State(o).TryGetValue(key, out var v) && v.Value is float f ? f : fallback;

    // ---- Feats ----------------------------------------------------------------

    [NwnxFunc(P, "AddFeat")]
    static void AddFeat(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int feat = n.PopInt();
        if (c != null && !c.Feats.Contains(feat)) c.Feats.Add(feat);
    }
    [NwnxFunc(P, "AddFeatByLevel")]
    static void AddFeatByLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int feat = n.PopInt(); int level = n.PopInt();
        if (c == null) return;
        if (!c.Feats.Contains(feat)) c.Feats.Add(feat);
        n.State(c)["featlevel:" + feat] = NwnxValue.Int(level);
    }
    [NwnxFunc(P, "RemoveFeat")]
    static void RemoveFeat(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int feat = n.PopInt();
        c?.Feats.Remove(feat);
    }
    [NwnxFunc(P, "RemoveFeatByLevel")]
    static void RemoveFeatByLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int feat = n.PopInt(); n.PopInt();
        c?.Feats.Remove(feat);
    }
    [NwnxFunc(P, "GetKnowsFeat")]
    static void KnowsFeat(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int feat = n.PopInt();
        n.Push(c != null && c.Feats.Contains(feat));
    }
    [NwnxFunc(P, "GetFeatCount")] static void FeatCount(NwnxCall n) => n.Push(n.PopObject<Creature>()?.Feats.Count ?? 0);
    [NwnxFunc(P, "GetFeatByIndex")]
    static void FeatByIndex(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int i = n.PopInt();
        n.Push(c != null && i >= 0 && i < c.Feats.Count ? c.Feats[i] : -1);
    }
    [NwnxFunc(P, "GetFeatGrantLevel")]
    static void FeatGrantLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int feat = n.PopInt();
        n.Push(c != null && c.Feats.Contains(feat) ? StateInt(n, c, "featlevel:" + feat, 1) : 0);
    }
    [NwnxFunc(P, "GetHighestLevelOfFeat")]
    static void HighestLevelOfFeat(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int feat = n.PopInt();
        if (c == null) { n.Push(-1); return; }
        // Follow feat.2da SUCCESSOR links to the highest one the creature has.
        var table = n.World.Resources.TwoDA("feat");
        int best = c.Feats.Contains(feat) ? feat : -1;
        for (int f = feat, guard = 0; guard < 64; guard++)
        {
            int next = table.GetInt(f, "SUCCESSOR", -1);
            if (next < 0) break;
            if (c.Feats.Contains(next)) best = next;
            f = next;
        }
        n.Push(best);
    }
    [NwnxFunc(P, "GetFeatRemainingUses")]
    static void FeatRemainingUses(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int feat = n.PopInt();
        if (c == null || !c.Feats.Contains(feat)) { n.Push(0); return; }
        n.Push(c.FeatUses.TryGetValue(feat, out var u) ? u : TotalUses(n, feat));
    }
    [NwnxFunc(P, "GetFeatTotalUses")]
    static void FeatTotalUses(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int feat = n.PopInt();
        n.Push(c != null && c.Feats.Contains(feat) ? TotalUses(n, feat) : 0);
    }
    static int TotalUses(NwnxCall n, int feat)
    {
        int perDay = n.World.Resources.TwoDA("feat").GetInt(feat, "USESPERDAY", -1);
        return perDay > 0 ? perDay : 100; // unlimited feats report 100
    }
    [NwnxFunc(P, "SetFeatRemainingUses")]
    static void SetFeatRemainingUses(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int feat = n.PopInt(); int uses = n.PopInt();
        if (c != null && c.Feats.Contains(feat)) c.FeatUses[feat] = Math.Max(0, uses);
    }

    // ---- Abilities, skills, saves, AC -------------------------------------

    [NwnxFunc(P, "SetRawAbilityScore")]
    static void SetAbility(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int ability = n.PopInt(); int value = n.PopInt();
        if (c != null && ability is >= 0 and < 6) c.Abilities[ability] = Math.Clamp(value, 3, 255);
    }
    [NwnxFunc(P, "GetRawAbilityScore")]
    static void GetAbility(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int ability = n.PopInt();
        n.Push(c != null && ability is >= 0 and < 6 ? c.Abilities[ability] : -1);
    }
    [NwnxFunc(P, "GetPrePolymorphAbilityScore")] static void GetPrePolyAbility(NwnxCall n) => GetAbility(n);
    [NwnxFunc(P, "ModifyRawAbilityScore")]
    static void ModifyAbility(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int ability = n.PopInt(); int mod = n.PopInt();
        if (c != null && ability is >= 0 and < 6) c.Abilities[ability] = Math.Clamp(c.Abilities[ability] + mod, 3, 255);
    }
    [NwnxFunc(P, "SetSkillRank")]
    static void SetSkillRank(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int skill = n.PopInt(); int rank = n.PopInt();
        if (c == null || skill < 0 || skill > 255) return;
        if (skill >= c.Skills.Length) Array.Resize(ref c.Skills, skill + 1);
        c.Skills[skill] = Math.Clamp(rank, -127, 127);
    }
    [NwnxFunc(P, "GetSkillRankByLevel")]
    static void SkillRankByLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int skill = n.PopInt(); n.PopInt();
        n.Push(c != null && skill >= 0 && skill < c.Skills.Length ? c.Skills[skill] : 0);
    }
    [NwnxFunc(P, "SetBaseSavingThrow")]
    static void SetBaseSave(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int which = n.PopInt(); int value = n.PopInt();
        if (c == null) return;
        switch (which)
        {
            case 1: c.FortBonus = value; break;
            case 2: c.RefBonus = value; break;
            case 3: c.WillBonus = value; break;
        }
    }
    [NwnxFunc(P, "GetBaseSavingThrow")]
    static void GetBaseSave(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int which = n.PopInt();
        n.Push(c == null ? 0 : which switch { 1 => c.FortBonus, 2 => c.RefBonus, 3 => c.WillBonus, _ => 0 });
    }
    [NwnxFunc(P, "SetBaseAC")]
    static void SetBaseAC(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int ac = n.PopInt();
        if (c != null) c.NaturalAC = ac;
    }
    [NwnxFunc(P, "GetBaseAC")] static void GetBaseAC(NwnxCall n) => n.Push(n.PopObject<Creature>()?.NaturalAC ?? -1);
    [NwnxFunc(P, "SetBaseAttackBonus")]
    static void SetBab(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int bab = n.PopInt();
        if (c != null) c.BaseAttackOverride = bab;
    }

    /// <summary>Base attack bonus from the class attack tables (or the override).</summary>
    static int BaseAttack(NwnxCall n, Creature c)
    {
        if (c.BaseAttackOverride >= 0) return c.BaseAttackOverride;
        var classes = n.World.Resources.TwoDA("classes");
        int bab = 0;
        foreach (var k in c.Classes)
        {
            var table = classes.Get(k.Class, "AttackBonusTable");
            if (table.Length == 0 || k.Level <= 0) continue;
            bab += n.World.Resources.TwoDA(table).GetInt(Math.Min(k.Level, 60) - 1, "BAB");
        }
        return bab;
    }
    static int Mod(int score) => (int)Math.Floor((score - 10) / 2.0);

    [NwnxFunc(P, "GetAttackBonus", UnsupportedReason = "returns base attack plus ability modifier only; effect, feat and item bonuses are not included")]
    static void AttackBonus(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        int melee = n.PopInt(); n.PopInt(); n.PopInt(); int includeBase = n.PopInt();
        if (c == null) { n.Push(-1); return; }
        // TODO(creature): add effect, feat and item bonuses once the rules layer computes them.
        int ab = (includeBase != 0 ? BaseAttack(n, c) : 0) + Mod(melee != 0 ? c.Abilities[0] : c.Abilities[1]);
        n.Push(ab);
    }

    // ---- Spells ---------------------------------------------------------------

    [NwnxFunc(P, "GetMemorisedSpell")]
    static void GetMemorised(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt(); int index = n.PopInt();
        var k = ClassOf(c, cls);
        if (k != null && level is >= 0 and < 10 && index >= 0 && index < k.Memorized[level].Count)
        {
            var m = k.Memorized[level][index];
            n.Push(m.Spell); n.Push(m.Ready); n.Push(m.Meta); n.Push(0);
        }
        else { n.Push(-1); n.Push(0); n.Push(0); n.Push(0); }
    }
    [NwnxFunc(P, "SetMemorisedSpell")]
    static void SetMemorised(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt(); int index = n.PopInt();
        n.PopInt(); // domain
        int meta = n.PopInt(); bool ready = n.PopInt() != 0; int id = n.PopInt();
        var k = ClassOf(c, cls);
        if (k == null || level is < 0 or >= 10 || index < 0) return;
        var list = k.Memorized[level];
        while (list.Count <= index) list.Add((-1, 0, false));
        list[index] = (id, meta, ready);
    }
    [NwnxFunc(P, "ClearMemorisedSpell")]
    static void ClearMemorised(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt(); int index = n.PopInt();
        var k = ClassOf(c, cls);
        if (k != null && level is >= 0 and < 10 && index >= 0 && index < k.Memorized[level].Count) k.Memorized[level][index] = (-1, 0, false);
    }
    [NwnxFunc(P, "GetMemorisedSpellCountByLevel")]
    static void MemorisedCount(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt();
        var k = ClassOf(c, cls);
        n.Push(k != null && level is >= 0 and < 10 ? k.Memorized[level].Count : 0);
    }
    [NwnxFunc(P, "GetKnownSpell")]
    static void KnownSpell(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt(); int index = n.PopInt();
        var k = ClassOf(c, cls);
        n.Push(k != null && level is >= 0 and < 10 && index >= 0 && index < k.KnownSpells[level].Count ? k.KnownSpells[level][index] : -1);
    }
    [NwnxFunc(P, "GetKnownSpellCount")]
    static void KnownSpellCount(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt();
        var k = ClassOf(c, cls);
        n.Push(k != null && level is >= 0 and < 10 ? k.KnownSpells[level].Count : 0);
    }
    [NwnxFunc(P, "AddKnownSpell")]
    static void AddKnownSpell(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt(); int spell = n.PopInt();
        var k = ClassOf(c, cls);
        if (k != null && level is >= 0 and < 10 && !k.KnownSpells[level].Contains(spell)) k.KnownSpells[level].Add(spell);
    }
    [NwnxFunc(P, "RemoveKnownSpell")]
    static void RemoveKnownSpell(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt(); int spell = n.PopInt();
        var k = ClassOf(c, cls);
        if (k != null && level is >= 0 and < 10) k.KnownSpells[level].Remove(spell);
    }
    [NwnxFunc(P, "GetRemainingSpellSlots")]
    static void GetSlots(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt();
        n.Push(c != null ? StateInt(n, c, $"slots:{cls}:{level}") : 0);
    }
    [NwnxFunc(P, "SetRemainingSpellSlots")]
    static void SetSlots(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt(); int slots = n.PopInt();
        if (c != null) n.State(c)[$"slots:{cls}:{level}"] = NwnxValue.Int(slots);
    }
    [NwnxFunc(P, "GetMaxSpellSlots")]
    static void MaxSlots(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt();
        var k = ClassOf(c, cls);
        if (k == null || level is < 0 or >= 10) { n.Push(0); return; }
        var table = n.World.Resources.TwoDA("classes").Get(cls, "SpellGainTable");
        n.Push(table.Length == 0 ? 0 : Math.Max(0, n.World.Resources.TwoDA(table).GetInt(Math.Max(0, k.Level - 1), $"SpellLevel{level}")));
    }
    [NwnxFunc(P, "SetDomain")]
    static void SetDomain(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int index = n.PopInt(); int domain = n.PopInt();
        var k = ClassOf(c, cls);
        if (k == null) return;
        if (index == 1) k.Domain1 = domain;
        else if (index == 2) k.Domain2 = domain;
    }
    [NwnxFunc(P, "SetSpecialization")]
    static void SetSpecialization(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int school = n.PopInt();
        if (ClassOf(c, cls) is { } k) k.School = school;
    }

    // ---- Hit points, movement, appearance ----------------------------------

    [NwnxFunc(P, "GetMaxHitPointsByLevel")]
    static void GetHpByLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int level = n.PopInt();
        n.Push(c != null ? StateInt(n, c, "hp:" + level) : 0);
    }
    [NwnxFunc(P, "SetMaxHitPointsByLevel")]
    static void SetHpByLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int level = n.PopInt(); int value = n.PopInt();
        if (c == null || level < 1) return;
        // TODO(creature): fold per-level hit points into the rules' max HP calculation.
        bool known = n.State(c).ContainsKey("hp:" + level);
        int old = StateInt(n, c, "hp:" + level);
        n.State(c)["hp:" + level] = NwnxValue.Int(value);
        if (known) c.MaxHitPoints = Math.Max(1, c.MaxHitPoints + value - old);
    }

    static void ApplyMovement(NwnxCall n, Creature c)
    {
        var speeds = n.World.Resources.TwoDA("creaturespeed");
        int rate = c.IsPC ? 0 : c.MovementRateIndex;
        float factor = StateFloat(n, c, "moveFactor", 1f);
        c.WalkRate = speeds.GetFloat(rate, "WALKRATE", c.WalkRate) * factor;
        c.RunRate = speeds.GetFloat(rate, "RUNRATE", c.RunRate) * factor;
        Changed(n, c);
    }
    [NwnxFunc(P, "SetMovementRate")]
    static void SetMovementRate(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int rate = n.PopInt();
        if (c == null) return;
        c.MovementRateIndex = rate;
        ApplyMovement(n, c);
    }
    [NwnxFunc(P, "GetMovementRateFactor")]
    static void GetMoveFactor(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        n.Push(c != null ? StateFloat(n, c, "moveFactor", 1f) : 0f);
    }
    [NwnxFunc(P, "SetMovementRateFactor")]
    static void SetMoveFactor(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); float factor = n.PopFloat();
        if (c == null) return;
        n.State(c)["moveFactor"] = NwnxValue.Float(Math.Max(0, factor));
        ApplyMovement(n, c);
    }
    [NwnxFunc(P, "GetWalkAnimation")]
    static void GetWalkAnimation(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        n.Push(c != null ? StateInt(n, c, "walkAnimation") : -1);
    }
    [NwnxFunc(P, "SetWalkAnimation")]
    static void SetWalkAnimation(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int anim = n.PopInt();
        if (c == null) return;
        n.State(c)["walkAnimation"] = NwnxValue.Int(anim);
        n.World.Output?.Notify("creature-override", c, new { walkAnimation = anim });
    }
    [NwnxFunc(P, "OverrideDamageLevel")]
    static void OverrideDamageLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int level = n.PopInt();
        if (c == null) return;
        n.State(c)["damageLevel"] = NwnxValue.Int(level);
        n.World.Output?.Notify("creature-override", c, new { damageLevel = level });
    }
    [NwnxFunc(P, "SetGender")]
    static void SetGender(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int gender = n.PopInt();
        if (c == null) return;
        c.Gender = (byte)gender;
        Changed(n, c);
    }
    [NwnxFunc(P, "SetSize")]
    static void SetSize(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int size = n.PopInt();
        if (c != null) c.Size = size;
    }
    [NwnxFunc(P, "SetRacialType")]
    static void SetRace(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int race = n.PopInt();
        if (c != null) c.Race = (byte)race;
    }
    [NwnxFunc(P, "SetGold")]
    static void SetGold(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int gold = n.PopInt();
        if (c != null) c.Gold = Math.Max(0, gold);
    }
    [NwnxFunc(P, "SetCorpseDecayTime")]
    static void SetDecay(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int t = n.PopInt();
        if (c != null) c.DecayTime = t;
    }
    [NwnxFunc(P, "SetChallengeRating")]
    static void SetCR(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); float cr = n.PopFloat();
        if (c != null) c.ChallengeRating = cr;
    }
    [NwnxFunc(P, "SetAlignmentGoodEvil")]
    static void SetGoodEvil(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int v = n.PopInt();
        if (c != null) c.GoodEvil = Math.Clamp(v, 0, 100);
    }
    [NwnxFunc(P, "SetAlignmentLawChaos")]
    static void SetLawChaos(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int v = n.PopInt();
        if (c != null) c.LawfulChaotic = Math.Clamp(v, 0, 100);
    }
    [NwnxFunc(P, "GetSoundset")] static void GetSoundset(NwnxCall n) => n.Push(n.PopObject<Creature>()?.SoundSet ?? -1);
    [NwnxFunc(P, "SetSoundset")]
    static void SetSoundset(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int s = n.PopInt();
        if (c != null) c.SoundSet = s;
    }
    [NwnxFunc(P, "GetDisarmable")] static void GetDisarmable(NwnxCall n) => n.Push(n.PopObject<Creature>()?.Disarmable == true);
    [NwnxFunc(P, "SetDisarmable")]
    static void SetDisarmable(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); bool d = n.PopInt() != 0;
        if (c != null) c.Disarmable = d;
    }
    [NwnxFunc(P, "GetNoPermanentDeath")] static void GetNoPermDeath(NwnxCall n) => n.Push(n.PopObject<Creature>()?.NoPermDeath == true);
    [NwnxFunc(P, "SetNoPermanentDeath")]
    static void SetNoPermDeath(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); bool v = n.PopInt() != 0;
        if (c != null) c.NoPermDeath = v;
    }

    [NwnxFunc(P, "SetFaction")]
    static void SetFaction(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int faction = n.PopInt();
        if (c != null) c.Faction = faction;
    }
    [NwnxFunc(P, "GetFaction")] static void GetFaction(NwnxCall n) => n.Push(n.PopObject<Creature>()?.Faction ?? -1);

    [NwnxFunc(P, "SetOriginalName")]
    static void SetOriginalName(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var name = n.PopString(); bool last = n.PopInt() != 0;
        if (c != null) n.State(c)[last ? "origLast" : "origFirst"] = NwnxValue.String(name);
    }
    [NwnxFunc(P, "GetOriginalName")]
    static void GetOriginalName(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); bool last = n.PopInt() != 0;
        if (c == null) { n.Push(""); return; }
        if (n.State(c).TryGetValue(last ? "origLast" : "origFirst", out var v) && v.Value is string s) { n.Push(s); return; }
        var loc = c.Template.GetLocString(last ? "LastName" : "FirstName");
        n.Push(loc.Resolve(n.World.Resources.Talk));
    }

    [NwnxFunc(P, "AddAssociate")]
    static void AddAssociate(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var a = n.PopObject<Creature>(); int type = n.PopInt();
        if (c == null || a == null) return;
        a.Master = c.Id;
        a.AssociateType = type;
    }

    [NwnxFunc(P, "JumpToLimbo", UnsupportedReason = "removes the creature from its area but does not keep it alive in limbo")]
    static void JumpToLimbo(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        if (c == null || c.IsPC || c.Area == null) return;
        // TODO(world): keep limbo creatures alive outside any area instead of only clearing the area link.
        c.Area.Objects.Remove(c);
        n.World.Output?.Notify("creature-override", c, new { limbo = true });
    }

    /// <summary>Nearest walkable point (by ground height under the position); the position itself when none is found.</summary>
    [NwnxFunc(P, "ComputeSafeLocation")]
    static void SafeLocation(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        float z = n.PopFloat(), y = n.PopFloat(), x = n.PopFloat();
        n.PopFloat(); n.PopInt();
        var pos = new Vector3(x, y, z);
        if (c?.Area?.Walkmesh.HighestGround(x, y) is { } ground) pos.Z = ground;
        n.Push(pos.X); n.Push(pos.Y); n.Push(pos.Z);
    }

    // ---- Quickbar (shared with NWNX_Player's quickbar slots) ---------------

    [NwnxFunc(P, "SerializeQuickbar")]
    static void SerializeQuickbar(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        n.Push(c != null ? PlayerPlugin.SerializeQuickbar(n, c) : "");
    }
    [NwnxFunc(P, "DeserializeQuickbar")]
    static void DeserializeQuickbar(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var data = n.PopString();
        n.Push(c != null && PlayerPlugin.DeserializeQuickbar(n, c, data));
    }

    // ---- Special abilities --------------------------------------------------

    static List<(int Id, int Ready, int Level)> Abilities(NwnxCall n, Creature c)
    {
        var list = new List<(int, int, int)>();
        foreach (var s in c.Template.GetList("SpecAbilityList"))
            list.Add(((int)s.GetInt("Spell"), s.GetInt("SpellFlags", 1) != 0 ? 1 : 0, (int)s.GetInt("SpellCasterLevel")));
        return list;
    }
    [NwnxFunc(P, "GetSpecialAbilityCount")]
    static void SpecialAbilityCount(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        n.Push(c != null ? Abilities(n, c).Count : 0);
    }
    [NwnxFunc(P, "GetSpecialAbility")]
    static void SpecialAbility(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int index = n.PopInt();
        var list = c != null ? Abilities(n, c) : new();
        var (id, ready, level) = index >= 0 && index < list.Count ? list[index] : (-1, 0, 0);
        n.Push(id); n.Push(ready); n.Push(level);
    }
}
