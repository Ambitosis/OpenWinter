using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Skills;

/// <summary>
/// Hiders revealed through their stealth (NWNX_Reveal): to one observer until the hider next enters stealth out of
/// that observer's perception, or to its party while the flag is set. A reveal is a detection method: seen, or only
/// heard. It applies to player observers; NPC observers were measured unaffected on nwserver (parity suite
/// nwnx_reveal: RevealTo and SetRevealToParty left stealthy hiders unperceived by NPCs 3 m away). The player-observer
/// behaviour (what is perceived, when a reveal ends, who counts as party) follows the NWNX header and is unmeasured.
/// </summary>
public sealed partial class SkillSystem
{
    public const int RevealHeard = 0, RevealSeen = 1;

    readonly Dictionary<(uint Hider, uint Observer), int> _revealedTo = new();
    readonly Dictionary<uint, int> _revealedToParty = new();

    public void RevealTo(Creature hider, Creature observer, int method) => _revealedTo[(hider.Id, observer.Id)] = method;

    public void SetRevealToParty(Creature hider, bool reveal, int method)
    {
        if (reveal) _revealedToParty[hider.Id] = method;
        else _revealedToParty.Remove(hider.Id);
    }

    /// <summary>The detection method <paramref name="hider"/> is revealed to <paramref name="observer"/> by, or null.</summary>
    public int? RevealMethod(Creature observer, Creature hider)
    {
        if (!observer.IsPC || _revealedTo.Count == 0 && _revealedToParty.Count == 0) return null;
        if (_revealedTo.TryGetValue((hider.Id, observer.Id), out int m)) return m;
        if (_revealedToParty.TryGetValue(hider.Id, out int p) && SameParty(observer, hider)) return p;
        return null;
    }

    public bool RevealedSeen(Creature observer, Creature hider) => RevealMethod(observer, hider) == RevealSeen;
    public bool RevealedHeard(Creature observer, Creature hider) => RevealMethod(observer, hider) == RevealHeard;

    bool SameParty(Creature observer, Creature hider)
    {
        if (PlayerOf(observer) == null || PlayerOf(hider) == null) return false;
        int? of = _world.Factions.FactionOf(observer), hf = _world.Factions.FactionOf(hider);
        return of != null && of == hf;
    }

    /// <summary>Entering stealth ends the reveals to observers that don't perceive the hider at that moment.</summary>
    void EndRevealsOutOfSight(Creature hider)
    {
        if (_revealedTo.Count == 0) return;
        foreach (var key in _revealedTo.Keys.Where(k => k.Hider == hider.Id).ToList())
        {
            var observer = _world.Get<Creature>(key.Observer);
            if (observer == null || observer.Area != hider.Area
                || !observer.Society.Seen.Contains(hider.Id) && !observer.Society.Heard.Contains(hider.Id))
                _revealedTo.Remove(key);
        }
    }

    /// <summary>Forgets a destroyed creature's reveals.</summary>
    void ForgetReveals(uint id)
    {
        _revealedToParty.Remove(id);
        if (_revealedTo.Count == 0) return;
        foreach (var key in _revealedTo.Keys.Where(k => k.Hider == id || k.Observer == id).ToList()) _revealedTo.Remove(key);
    }
}
