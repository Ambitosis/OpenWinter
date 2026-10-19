using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game;

/// <summary>
/// Factions and reputation from the module's repute.fac, plus personal
/// reputation overrides between individual creatures. Reputation is 0-100:
/// 0-10 hostile, 11-89 neutral, 90-100 friendly.
/// </summary>
public sealed partial class FactionTable
{
    public sealed record Faction(int Id, string Name, int Parent, bool Global);

    public readonly List<Faction> Factions = new();
    readonly Dictionary<(int, int), int> _rep = new();
    /// <summary>PC parties get their own faction ids (above the module's), allocated on demand.</summary>
    int _nextPartyFaction = 100000;

    public const int PlayerFaction = 0, Hostile = 1, Commoner = 2, Merchant = 3, Defender = 4;

    /// <summary>True when the module has no repute.fac and runs on the original's fallback factions.</summary>
    public bool Fallback { get; private set; }

    /// <summary>The faction ids of STANDARD_FACTION_HOSTILE, _COMMONER, _MERCHANT and _DEFENDER.</summary>
    int[] _standard = { Hostile, Commoner, Merchant, Defender };

    public void Load(ResMan rm)
    {
        var fac = rm.ReadGff("repute", ResType.Fac);
        if (fac == null)
        {
            // A module without repute.fac (measured on nwserver, parity suite faction_nofac): every NPC faction is a
            // friend (100) of every other, and the factions are still told apart. Creatures keep their FactionID,
            // but the standard factions sit at other ids than in a toolset repute.fac: FactionID 2 is the faction of
            // STANDARD_FACTION_MERCHANT, 3 of DEFENDER, 4 of COMMONER; 1, 0 and ids past 4 are HOSTILE.
            // Toward players (nwmain, visual scene rulesfollowpc) Hostile is an enemy (0) and the others are friends
            // (100), both ways.
            Fallback = true;
            string[] names = { "PC", "Hostile", "Merchant", "Defender", "Commoner" };
            for (int i = 0; i < names.Length; i++) Factions.Add(new Faction(i, names[i], -1, true));
            _standard = new[] { Hostile, 4, 2, 3 };
            for (int of = Hostile; of <= 4; of++)
                for (int toward = PlayerFaction; toward <= 4; toward++)
                {
                    if (of == toward) continue;
                    SetFactionReputation(of, toward, toward == PlayerFaction && of == Hostile ? 0 : 100);
                }
            return;
        }
        var list = fac.GetList("FactionList");
        for (int i = 0; i < list.Count; i++)
            Factions.Add(new Faction(i, list[i].GetString("FactionName"), unchecked((int)(uint)list[i].GetInt("FactionParentID")), list[i].GetInt("FactionGlobal") != 0));
        foreach (var r in fac.GetList("RepList"))
            _rep[((int)r.GetInt("FactionID1"), (int)r.GetInt("FactionID2"))] = (int)r.GetInt("FactionRep");
    }

    /// <summary>
    /// The toolset's standard repute.fac (for tests and tools that build worlds without a module): PC, Hostile,
    /// Commoner, Merchant, Defender at ids 0-4; Hostile hates everyone, the other three like each other and are
    /// neutral to players.
    /// </summary>
    public void LoadStandard()
    {
        Factions.Clear();
        _rep.Clear();
        Fallback = false;
        _standard = new[] { Hostile, Commoner, Merchant, Defender };
        string[] names = { "PC", "Hostile", "Commoner", "Merchant", "Defender" };
        for (int i = 0; i < names.Length; i++) Factions.Add(new Faction(i, names[i], -1, true));
        for (int of = Hostile; of <= Defender; of++)
            for (int toward = PlayerFaction; toward <= Defender; toward++)
            {
                if (of == toward) continue;
                SetFactionReputation(of, toward, of == Hostile || toward == Hostile ? 0 : toward == PlayerFaction ? 50 : 100);
            }
    }

    public int NewPartyFaction() => _nextPartyFaction++;

    /// <summary>STANDARD_FACTION_* (0 hostile .. 3 defender) to the module's faction id, or -1.</summary>
    public int StandardFaction(int standard) => standard is >= 0 and <= 3 ? _standard[standard] : -1;

    /// <summary>
    /// The faction an NPC's stored FactionID puts it in: as stored, except that without a repute.fac FactionID 0
    /// and ids past the list are the hostile faction (faction_nofac).
    /// </summary>
    public int NpcFaction(int id) => Fallback && (id <= PlayerFaction || id >= Factions.Count) && !IsPartyFaction(id) ? Hostile : id;

    /// <summary>How faction <paramref name="of"/> feels about faction <paramref name="toward"/>.</summary>
    public int FactionReputation(int of, int toward)
    {
        bool same = of == toward;
        if (same && of >= FirstPartyFaction) return 100;
        if (of >= 100000) of = PlayerFaction;
        if (toward >= 100000) toward = PlayerFaction;
        if (of == toward && !same) return 50;
        return _rep.TryGetValue((toward, of), out var r) ? r : same ? 100 : 50;
    }

    public void SetFactionReputation(int of, int toward, int value) => _rep[(toward, of)] = Math.Clamp(value, 0, 100);

    /// <summary>How <paramref name="source"/> feels about <paramref name="target"/>, including personal adjustments.</summary>
    public int Reputation(Creature source, Creature target) => ReputationOf(source, target);

    /// <summary>Sets how <paramref name="source"/> feels about <paramref name="target"/> with an indefinite personal adjustment.</summary>
    public void SetPersonal(Creature source, Creature target, int value) => SetTemporary(source, target, Math.Clamp(value, 0, 100), false, 0);
    public void ClearPersonal(Creature source, Creature target) => ClearPersonal(source.Id, target.Id);

    public bool IsEnemy(Creature source, Creature target) => Reputation(source, target) <= 10;
    public bool IsFriend(Creature source, Creature target) => Reputation(source, target) >= 90;
    public bool IsNeutral(Creature source, Creature target) { int r = Reputation(source, target); return r > 10 && r < 90; }

    public int FindByName(string name) => Factions.FindIndex(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
