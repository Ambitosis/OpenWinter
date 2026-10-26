using System.Numerics;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Creature over the creature's rules state. Settings the rules read (movement, criticals, caster levels,
/// bonus spells, ...) live in <see cref="CreatureOverrides"/>; the level history in the template's LvlStatList.
/// Behaviour measured on nwserver with NWNX:EE (parity suites nwnx_creature_*).
/// </summary>
static partial class CreaturePlugin
{
    const string P = "Creature";

    static CreatureClass? ClassOf(Creature? c, int classId) => c?.Classes.FirstOrDefault(k => k.Class == classId);

    static void Changed(NwnxCall n, Creature c) => n.World.MarkChanged(c);

    static CreatureOverrides O(Creature c) => CreatureOverrides.Of(c);

    static int StateInt(NwnxCall n, GameObject o, string key, int fallback = 0) =>
        n.State(o).TryGetValue(key, out var v) && v.Value is int i ? i : fallback;

    // ---- Feats ----------------------------------------------------------------

    [NwnxFunc(P, "AddFeat")]
    static void AddFeat(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int feat = n.PopInt();
        if (c != null && !c.Feats.Contains(feat)) c.Feats.Add(feat);
    }
    [NwnxFunc(P, "RemoveFeat")]
    static void RemoveFeat(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int feat = n.PopInt();
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

    /// <summary>Uses per day for the NWNX readings: 100 stands for unlimited, 0 for a feat the creature lacks.</summary>
    static int TotalUses(NwnxCall n, Creature c, int feat)
    {
        if (!c.Feats.Contains(feat)) return 0;
        int max = CreatureRules.MaxFeatUses(n.World, c, feat);
        return max < 0 ? 100 : max;
    }
    [NwnxFunc(P, "GetFeatRemainingUses")]
    static void FeatRemainingUses(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int feat = n.PopInt();
        if (c == null || !c.Feats.Contains(feat)) { n.Push(0); return; }
        int left = CreatureRules.RemainingFeatUses(n.World, c, feat);
        n.Push(left == int.MaxValue ? 100 : left);
    }
    [NwnxFunc(P, "GetFeatTotalUses")]
    static void FeatTotalUses(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int feat = n.PopInt();
        n.Push(c != null ? TotalUses(n, c, feat) : 0);
    }
    /// <summary>Sets a limited feat's uses left, up to its uses per day (nwserver: 9 on a once-a-day feat reads 1).</summary>
    [NwnxFunc(P, "SetFeatRemainingUses")]
    static void SetFeatRemainingUses(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int feat = n.PopInt(); int uses = n.PopInt();
        if (c == null || !c.Feats.Contains(feat)) return;
        int max = CreatureRules.MaxFeatUses(n.World, c, feat);
        if (max < 0) return;
        CreatureRules.EnsureInit(n.World, c);
        c.FeatUses[feat] = Math.Clamp(uses, 0, max);
    }
    [NwnxFunc(P, "RestoreFeats")]
    static void RestoreFeats(NwnxCall n)
    {
        if (n.PopObject<Creature>() is { } c) CreatureRules.RestoreFeatUses(n.World, c);
    }

    /// <summary>Whether the creature could take a feat now: feat.2da's requirements against its current state.</summary>
    [NwnxFunc(P, "GetMeetsFeatRequirements")]
    static void MeetsFeatRequirements(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int feat = n.PopInt();
        if (c == null) { n.Push(0); return; }
        CreatureRules.EnsureInit(n.World, c);
        var have = CreatureRules.AllFeats(n.World, c).ToHashSet();
        n.Push(Progression.ProgressionRules.FeatPrerequisiteErrors(n.World, c, feat, have).Count == 0);
    }

    // ---- Abilities, skills, saves, AC -------------------------------------

    [NwnxFunc(P, "SetRawAbilityScore")]
    static void SetAbility(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int ability = n.PopInt(); int value = n.PopInt();
        if (c != null && ability is >= 0 and < 6) { c.Abilities[ability] = Math.Clamp(value, 3, 255); SyncHitPoints(n, c); }
    }
    [NwnxFunc(P, "GetRawAbilityScore")]
    static void GetAbility(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int ability = n.PopInt();
        n.Push(c != null && ability is >= 0 and < 6 ? c.Abilities[ability] : -1);
    }
    /// <summary>Strength, Dexterity or Constitution before a polymorph; 0 when the creature isn't polymorphed.</summary>
    [NwnxFunc(P, "GetPrePolymorphAbilityScore")]
    static void GetPrePolyAbility(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int ability = n.PopInt();
        n.Push(c?.PolymorphForm != null && ability is NwRules.AbilityStr or NwRules.AbilityDex or NwRules.AbilityCon ? c.Abilities[ability] : 0);
    }
    [NwnxFunc(P, "ModifyRawAbilityScore")]
    static void ModifyAbility(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int ability = n.PopInt(); int mod = n.PopInt();
        if (c != null && ability is >= 0 and < 6) { c.Abilities[ability] = Math.Clamp(c.Abilities[ability] + mod, 3, 255); SyncHitPoints(n, c); }
    }

    static void SyncHitPoints(NwnxCall n, Creature c)
    {
        CreatureRules.SyncHitPoints(n.World, c);
        Changed(n, c);
    }

    /// <summary>The base ranks in a skill; the rank has to be within -127..128 (nwserver refuses 200).</summary>
    [NwnxFunc(P, "SetSkillRank")]
    static void SetSkillRank(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int skill = n.PopInt(); int rank = n.PopInt();
        if (c == null || skill < 0 || skill > 255 || rank is < -127 or > 128) return;
        if (skill >= c.Skills.Length) Array.Resize(ref c.Skills, skill + 1);
        c.Skills[skill] = rank;
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

    // ---- Appearance, alignment, money, odds and ends --------------------------

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
        if (c == null || a == null || a.IsPC || type <= 0) return;
        Society.AssociateService.Add(n.World, c, a, type);
    }

    /// <summary>Takes the creature out of its area, keeping it valid (nwserver: valid, no area, not found nearby).</summary>
    [NwnxFunc(P, "JumpToLimbo")]
    static void JumpToLimbo(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        if (c == null || c.IsPC || c.Area == null) return;
        c.Actions.Clear();
        Movement.Stop(c);
        n.World.ToLimbo(c);
    }

    /// <summary>Nearest walkable point (by ground height under the position); the position itself when none is found.</summary>
    [NwnxFunc(P, "ComputeSafeLocation")]
    static void SafeLocation(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var pos = n.PopVector();
        n.PopFloat(); n.PopInt();
        if (c == null) { n.Push(new NwnxValue(NwnxType.Vector, Vector3.Zero)); return; }
        if (c.Area?.Walkmesh.HighestGround(pos.X, pos.Y) is { } ground) pos.Z = ground;
        n.Push(new NwnxValue(NwnxType.Vector, pos));
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
}
