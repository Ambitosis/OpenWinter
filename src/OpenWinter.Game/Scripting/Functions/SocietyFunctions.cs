using OpenWinter.Game.Objects;
using OpenWinter.Game.Society;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>Factions, reputation, parties and associates.</summary>
static partial class SocietyFunctions
{
    static uint Id(GameObject? o) => o?.Id ?? GameObject.InvalidId;
    static FactionTable Factions(Call c) => c.World.Factions;
    static SocietyService Service(Call c) => SocietyService.Of(c.World);

    // ---- faction membership ----

    [NwFunc("GetFactionEqual")]
    static object? FactionEqual(Call c)
    {
        var a = c.Obj(0);
        var b = c.Obj(1);
        if (a == null || b == null) return 0;
        int? fa = Factions(c).FactionOf(a), fb = Factions(c).FactionOf(b);
        return fa != null && fa == fb ? 1 : 0;
    }

    [NwFunc("ChangeFaction")]
    static object? ChangeFaction(Call c)
    {
        var o = c.Obj(0);
        var member = c.Obj(1);
        // Only NPCs (and other faction-bearing objects) can be moved; PCs stay in their parties.
        if (o == null || member == null || o is Creature { IsPC: true }) return null;
        if (Factions(c).FactionOf(member) is int f && Factions(c).FactionOf(o) != null)
        {
            Factions(c).SetObjectFaction(o, f);
            if (o is Creature cr) PartyService.Of(c.World).Refresh(cr);
        }
        return null;
    }

    [NwFunc("ChangeToStandardFaction")]
    static object? ChangeToStandard(Call c)
    {
        int f = Factions(c).StandardFaction(c.I(1));
        if (c.Obj<Creature>(0) is { IsPC: false } cr && f >= 0 && Factions(c).IsValidFaction(f)) cr.Faction = f;
        return null;
    }

    /// <summary>Members of an object's faction, or null when it has no valid faction.</summary>
    static List<Creature>? Members(Call c, GameObject? member, bool pcOnly = false)
    {
        if (member is not Creature) return null;
        if (Factions(c).FactionOf(member) is not int f) return null;
        return Service(c).FactionMembers(f, pcOnly);
    }

    [NwFunc("GetFirstFactionMember")]
    static object? FirstMember(Call c)
    {
        var member = c.Obj(0);
        if (member is not Creature || Factions(c).FactionOf(member) is not int f) return GameObject.InvalidId;
        var list = Service(c).FactionMembers(f, c.I(1) != 0).Select(m => m.Id).ToList();
        Service(c).FactionCursors[f] = (list, 0);
        return list.Count > 0 ? list[0] : GameObject.InvalidId;
    }

    [NwFunc("GetNextFactionMember")]
    static object? NextMember(Call c)
    {
        var member = c.Obj(0);
        if (member is not Creature || Factions(c).FactionOf(member) is not int f) return GameObject.InvalidId;
        var cursors = Service(c).FactionCursors;
        if (!cursors.TryGetValue(f, out var cur)) return GameObject.InvalidId;
        int i = cur.Index + 1;
        while (i < cur.List.Count && c.World.Get(cur.List[i]) == null) i++;
        cursors[f] = (cur.List, i);
        return i < cur.List.Count ? cur.List[i] : GameObject.InvalidId;
    }

    [NwFunc("GetFactionLeader")]
    static object? Leader(Call c)
    {
        if (c.Obj(0) is not Creature cr || Factions(c).FactionOf(cr) is not int f || !FactionTable.IsPartyFaction(f)) return GameObject.InvalidId;
        return Id(PartyService.Of(c.World).Leader(f));
    }

    // ---- faction statistics ----

    /// <summary>bMustBeVisible: the caller (when it is a creature) must currently see the member.</summary>
    static bool Visible(Call c, Creature m, bool mustBeVisible)
    {
        if (!mustBeVisible || c.Self is not Creature self || self == m) return true;
        return self.Society.Seen.Contains(m.Id);
    }

    static object? Pick(Call c, Func<Creature, double> score, bool highest, bool partyPlayersOnly = false)
    {
        var members = Members(c, c.Obj(0), partyPlayersOnly && c.Obj(0) is Creature member && FactionTable.IsPartyFaction(member.Faction));
        if (members == null) return GameObject.InvalidId;
        bool mustSee = c.I(1, 1) != 0;
        Creature? best = null;
        double bestScore = 0;
        foreach (var m in members)
        {
            if (SocietyService.IsDead(m) || !Visible(c, m, mustSee)) continue;
            double s = score(m);
            if (best == null || (highest ? s > bestScore : s < bestScore)) { best = m; bestScore = s; }
        }
        return Id(best);
    }

    // Strength: hit dice, then current hit points to break ties.
    static double Strength(Creature m) => m.TotalLevel * 10000.0 + m.CurrentHitPoints;

    // A player's party: the players only. The original returned the player, never her henchmen, for both, called from
    // her or from a henchman (loadtips: a level 1 sorceress with a bandit and a goblin henchman).
    [NwFunc("GetFactionWeakestMember")] static object? Weakest(Call c) => Pick(c, Strength, highest: false, partyPlayersOnly: true);
    [NwFunc("GetFactionStrongestMember")] static object? Strongest(Call c) => Pick(c, Strength, highest: true, partyPlayersOnly: true);
    [NwFunc("GetFactionMostDamagedMember")] static object? MostDamaged(Call c) => Pick(c, m => m.MaxHitPoints - m.CurrentHitPoints, highest: true);
    [NwFunc("GetFactionLeastDamagedMember")] static object? LeastDamaged(Call c) => Pick(c, m => m.MaxHitPoints - m.CurrentHitPoints, highest: false);
    [NwFunc("GetFactionWorstAC")] static object? WorstAC(Call c) => PickAC(c, highest: false);
    [NwFunc("GetFactionBestAC")] static object? BestAC(Call c) => PickAC(c, highest: true);

    /// <summary>
    /// GetFactionBestAC / GetFactionWorstAC (measured by parity suite faction_bestac): every member in
    /// membership order, the reference itself, the dead, the invisible and members in other areas included. Best keeps a
    /// member only above the best so far starting from 0, Worst only below the worst so far starting from 999, so ties go
    /// to the member that joined first. bMustBeVisible is a walkmesh line of sight from the reference (members in another
    /// area still count); a reference with no area finds nobody.
    /// </summary>
    static object? PickAC(Call c, bool highest)
    {
        var reference = c.Obj(0);
        var members = Members(c, reference);
        if (members == null) return GameObject.InvalidId;
        bool mustSee = c.I(1, 1) != 0;
        if (mustSee && reference!.Area == null) return GameObject.InvalidId;
        Creature? best = null;
        int bestAC = highest ? 0 : 999;
        foreach (var m in members)
        {
            if (mustSee && m != reference && m.Area == reference!.Area && !LineOfSight.Between(c.World, reference, m)) continue;
            int ac = SocietyService.FactionArmorClass(c.World, m);
            if (highest ? ac > bestAC : ac < bestAC) { best = m; bestAC = ac; }
        }
        return Id(best);
    }

    [NwFunc("GetFactionGold")]
    static object? Gold(Call c)
    {
        var members = Members(c, c.Obj(0));
        return members == null ? -1 : members.Sum(m => m.Gold);
    }

    static object? Average(Call c, Func<Creature, int> value)
    {
        var members = Members(c, c.Obj(0));
        if (members == null || members.Count == 0) return -1;
        return (int)(members.Sum(m => (long)value(m)) / members.Count);
    }

    [NwFunc("GetFactionAverageGoodEvilAlignment")] static object? AvgGoodEvil(Call c) => Average(c, m => m.GoodEvil);
    [NwFunc("GetFactionAverageLawChaosAlignment")] static object? AvgLawChaos(Call c) => Average(c, m => m.LawfulChaotic);
    [NwFunc("GetFactionAverageLevel")] static object? AvgLevel(Call c) => Average(c, m => m.TotalLevel);
    [NwFunc("GetFactionAverageXP")] static object? AvgXP(Call c) => Average(c, m => m.Experience);

    [NwFunc("GetFactionAverageReputation")]
    static object? AvgReputation(Call c)
    {
        var members = Members(c, c.Obj(0));
        var target = c.Obj(1);
        if (members == null || members.Count == 0 || target == null) return -1;
        return (int)Math.Round(members.Average(m => (double)Factions(c).ReputationOf(m, target)));
    }

    [NwFunc("GetFactionMostFrequentClass")]
    static object? FrequentClass(Call c)
    {
        var members = Members(c, c.Obj(0));
        if (members == null || members.Count == 0) return -1;
        var counts = new Dictionary<int, int>();
        foreach (var m in members)
            foreach (var k in m.Classes)
                if (k.Level > 0) counts[k.Class] = counts.GetValueOrDefault(k.Class) + 1;
        if (counts.Count == 0) return -1;
        return counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
    }

    // ---- reputation ----

    [NwFunc("GetReputation")] static object? Reputation(Call c) => Factions(c).ReputationOf(c.Obj(0), c.Obj(1));

    [NwFunc("AdjustReputation")]
    static object? Adjust(Call c)
    {
        if (c.Obj(0) is { } target && c.Obj(1) is { } source) Factions(c).Adjust(target, source, c.I(2));
        return null;
    }

    // GetIsEnemy(oTarget, oSource): how oSource feels about oTarget.
    static object? Rep(Call c, Func<int, bool> test)
    {
        var target = c.Obj(0);
        var source = c.ObjOrSelf(1);
        if (target == null || source == null) return 0;
        int r = Factions(c).ReputationOf(source, target);
        return r >= 0 && test(r) ? 1 : 0;
    }

    static bool Enemy(int r) => r <= FactionTable.HostileTo;
    static bool Friend(int r) => r >= FactionTable.FriendFrom;
    static bool Neutral(int r) => r > FactionTable.HostileTo && r < FactionTable.FriendFrom;

    [NwFunc("GetIsEnemy")] static object? IsEnemy(Call c) => Rep(c, Enemy);
    [NwFunc("GetIsFriend")] static object? IsFriend(Call c) => Rep(c, Friend);
    [NwFunc("GetIsNeutral")] static object? IsNeutral(Call c) => Rep(c, Neutral);

    // Native friendly reaction requires shared faction for NPC contexts, unlike GetIsFriend.
    // PC-to-PC behavior keeps its existing reputation path; PvP contexts remain to be measured.
    static bool FriendlyReaction(Call c)
    {
        var target = c.Obj(0); var source = c.ObjOrSelf(1);
        if (source == null || target == null) return false;
        bool pcPair = source is Creature { IsPC: true } && target is Creature { IsPC: true };
        return (pcPair || Factions(c).FactionOf(source) == Factions(c).FactionOf(target))
            && Friend(Factions(c).ReputationOf(source, target));
    }
    [NwFunc("GetIsReactionTypeFriendly")] static object? ReactFriendly(Call c) => FriendlyReaction(c) ? 1 : 0;
    [NwFunc("GetIsReactionTypeNeutral")]
    static object? ReactNeutral(Call c) =>
        Rep(c, r => !Enemy(r)) is 1 && !FriendlyReaction(c) ? 1 : 0;
    [NwFunc("GetIsReactionTypeHostile")] static object? ReactHostile(Call c) => Rep(c, Enemy);

    [NwFunc("ClearPersonalReputation")]
    static object? ClearPersonal(Call c)
    {
        if (c.Obj(0) is { } target && c.ObjOrSelf(1) is { } source) Factions(c).ClearPersonal(source.Id, target.Id);
        return null;
    }

    static object? Temporary(Call c, int value)
    {
        // oTarget, oSource, bDecays, fDurationInSeconds
        if (c.Obj(0) is { } target && c.ObjOrSelf(1) is { } source)
            Factions(c).SetScriptedTemporary(source, target, value, c.I(2) != 0, c.F(3, 180f));
        return null;
    }

    [NwFunc("SetIsTemporaryFriend")] static object? TempFriend(Call c) => Temporary(c, 100);
    [NwFunc("SetIsTemporaryEnemy")] static object? TempEnemy(Call c) => Temporary(c, 0);
    [NwFunc("SetIsTemporaryNeutral")] static object? TempNeutral(Call c) => Temporary(c, 50);

    [NwFunc("SetStandardFactionReputation")]
    static object? SetStandard(Call c)
    {
        int f = Factions(c).StandardFaction(c.I(0));
        if (f >= 0 && Factions(c).IsValidFaction(f) && c.ObjOrSelf(2) is Creature cr) Factions(c).SetStanding(f, cr, c.I(1), c.World);
        return null;
    }

    [NwFunc("GetStandardFactionReputation")]
    static object? GetStandard(Call c)
    {
        int f = Factions(c).StandardFaction(c.I(0));
        if (f < 0 || !Factions(c).IsValidFaction(f) || c.ObjOrSelf(1) is not Creature cr) return -1;
        return Factions(c).Standing(f, cr);
    }

    [NwFunc("GetLastHostileActor")]
    static object? LastHostile(Call c)
    {
        if (c.ObjOrSelf(0) is not Creature v) return GameObject.InvalidId;
        return c.World.Get<Creature>(v.Society.LastHostileActor) is { } a && !SocietyService.IsDead(a) ? a.Id : GameObject.InvalidId;
    }

    // ---- parties ----

    [NwFunc("AddToParty")]
    static object? AddToParty(Call c)
    {
        if (c.Obj<Creature>(0) is { IsPC: true } pc && c.Obj<Creature>(1) is { IsPC: true } leader) PartyService.Of(c.World).Join(pc, leader);
        return null;
    }

    [NwFunc("RemoveFromParty")]
    static object? RemoveFromParty(Call c)
    {
        if (c.Obj<Creature>(0) is { IsPC: true } pc) PartyService.Of(c.World).Leave(pc);
        return null;
    }

    // ---- associates ----

    [NwFunc("GetMaster")]
    static object? Master(Call c) =>
        c.ObjOrSelf(0) is Creature a && c.World.Get<Creature>(a.Master) is { } m ? m.Id : GameObject.InvalidId;

    [NwFunc("GetAssociateType")]
    static object? AssociateType(Call c) =>
        c.Obj(0) is Creature a && c.World.Get<Creature>(a.Master) != null ? a.AssociateType : AssociateService.None;

    [NwFunc("GetLastAssociateCommand")] static object? LastCommand(Call c) => c.ObjOrSelf(0) is Creature a ? a.Society.LastAssociateCommand : 0;

    [NwFunc("SetAssociateListenPatterns")]
    static object? AssociatePatterns(Call c)
    {
        if (c.ObjOrSelf(0) is Creature a)
        {
            a.Society.AssociatePatterns = true;
            a.Society.Listening = true;
        }
        return null;
    }

    [NwFunc("GetHenchman")]
    static object? Henchman(Call c) =>
        c.ObjOrSelf(0) is Creature m ? Id(AssociateService.Get(c.World, m, AssociateService.Henchman, c.I(1, 1))) : GameObject.InvalidId;

    [NwFunc("GetAssociate")]
    static object? Associate(Call c) =>
        c.ObjOrSelf(1) is Creature m ? Id(AssociateService.Get(c.World, m, c.I(0), c.I(2, 1))) : GameObject.InvalidId;

    [NwFunc("AddHenchman")]
    static object? AddHenchman(Call c)
    {
        if (c.Obj(0) is not Creature master || c.ObjOrSelf(1) is not Creature hench || hench == master) return null;
        if (hench.IsPC || hench.Player?.IsDM == true) return null;
        if (hench.Master == master.Id && hench.AssociateType == AssociateService.Henchman) return null;
        if (AssociateService.Count(c.World, master, AssociateService.Henchman) >= Service(c).MaxHenchmen) return null;
        AssociateService.Add(c.World, master, hench, AssociateService.Henchman);
        return null;
    }

    [NwFunc("RemoveHenchman")]
    static object? RemoveHenchman(Call c)
    {
        if (c.Obj(0) is Creature master && c.ObjOrSelf(1) is Creature hench && hench.AssociateType == AssociateService.Henchman)
            AssociateService.Remove(c.World, master, hench);
        return null;
    }

    [NwFunc("RemoveSummonedAssociate")]
    static object? RemoveSummoned(Call c)
    {
        if (c.Obj(0) is Creature master && c.ObjOrSelf(1) is Creature assoc) AssociateService.Remove(c.World, master, assoc);
        return null;
    }

    [NwFunc("SetMaxHenchmen")] static object? SetMax(Call c) { Service(c).MaxHenchmen = c.I(0); return null; }
    [NwFunc("GetMaxHenchmen")] static object? GetMax(Call c) => Service(c).MaxHenchmen;

    [NwFunc("LevelUpHenchman")]
    static object? LevelUp(Call c) =>
        c.Obj(0) is Creature cr ? HenchmanLevels.LevelUp(c.World, cr, c.I(1, 255), c.I(2) != 0, c.I(3, 255)) : 0;
}
