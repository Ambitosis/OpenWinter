using OpenWinter.Game.Objects;

namespace OpenWinter.Game;

/// <summary>Server-wide play settings (nwserver's options) that the rules consult.</summary>
public enum PvpSetting { None = 0, Party = 1, Full = 2 }

public sealed partial class World
{
    /// <summary>Player-versus-player: None (players can't attack players), Party (not their own party), Full.</summary>
    public PvpSetting Pvp = PvpSetting.Party;

    /// <summary>
    /// One Party Only (nwserver's server.one-party-only, on by default there): every player is put in the one party,
    /// and nobody may leave it or be removed (TLK 66735 / 66736). Off for single player and tests; the dedicated
    /// server's config mirrors nwserver's default. docs/PARTY.md.
    /// </summary>
    public bool OneParty;

    /// <summary>
    /// Player party control (nwserver's server.player-party-control, off by default): the setting SetCommandingPlayer's
    /// override depends on. OpenWinter has no party-control mode of its own yet; see <see cref="CommandingPlayer"/>.
    /// </summary>
    public bool PlayerPartyControl;

    /// <summary>
    /// The PvP setting in force in an area: its PlayerVsPlayer (0 none, 1 party, 2 full, 3 or absent the server's),
    /// never more than the server's. Measured from the original's area-entry line (mp_join, a party-PvP server): a
    /// no-PvP area said "No PVP", party, full and default areas all said "Party PVP".
    /// </summary>
    public PvpSetting AreaPvp(Areas.Area? area)
    {
        long value = area?.Are.GetInt("PlayerVsPlayer", 3) ?? 3;
        return value is >= 0 and <= 2 && (PvpSetting)value < Pvp ? (PvpSetting)value : Pvp;
    }

    /// <summary>
    /// A player-driven creature's hostile act the PvP setting forbids: tells the player TLK 66245 ("You cannot perform that
    /// action on a friendly target due to PvP settings"), as the original did at once for ActionAttack and Taunt on her
    /// own summon (associateradial chat.txt), and returns true. NPCs are refused silently.
    /// </summary>
    public bool RefusesPvp(Creature actor, GameObject target)
    {
        if (PvpAllows(actor, target)) return false;
        if ((actor.IsPC || actor.Player != null) && Society.AssociateService.PlayerMaster(this, actor) is { } player)
            Output?.SendMessage(player, Resources.TlkString(66245));
        return true;
    }

    /// <summary>Item level restrictions: players can't equip items worth more than their level allows (itemvalue.2da).</summary>
    public bool ItemLevelRestrictions;

    /// <summary>The player character behind a creature: itself, or the master of an associate (summons, henchmen...).</summary>
    public Creature? ControllingPlayer(Creature c)
    {
        for (int depth = 0; depth < 4; depth++)
        {
            if (c.IsPC) return c;
            if (Get<Creature>(c.Master) is not { } master) return null;
            c = master;
        }
        return null;
    }

    /// <summary>
    /// Whether the PvP setting lets <paramref name="attacker"/> fight <paramref name="target"/> (always true unless both sides
    /// belong to players). A player can't attack her own associates either unless PvP is Full: measured in the original's
    /// party-PvP area, her scripted ActionAttack and Taunt on her own summon were refused (associateradial). Her associates'
    /// own AI is left alone. Full PvP on one's own associate is not measured. The setting is the area's
    /// (<see cref="AreaPvp"/>): an area set to No PvP protects players there even on a Full PvP server (docs/PARTY.md).
    /// </summary>
    public bool PvpAllows(Creature attacker, GameObject target)
    {
        if (target is not Creature t || ControllingPlayer(attacker) is not { } a || ControllingPlayer(t) is not { } b) return true;
        // The area's PvP setting (never more than the server's) decides, as its area-entry line says (World.AreaPvp).
        var pvp = AreaPvp(t.Area ?? attacker.Area);
        if (a == b) return attacker == target || pvp == PvpSetting.Full || !(attacker.IsPC || attacker.Player != null);
        return pvp switch
        {
            PvpSetting.None => false,
            PvpSetting.Party => System<Society.PartyService>().Ensure(a) != System<Society.PartyService>().Ensure(b),
            _ => true,
        };
    }

    /// <summary>
    /// SetCommandingPlayer: the player who may command a creature. Without an override, the player whose associate it
    /// is (her own creature for a player). With an override and player party control on, the named player; an override
    /// naming anything else (the module) leaves nobody. With party control off the override changes nothing, as
    /// nwscript.nss says ("if player party control is enabled").
    /// </summary>
    public Creature? CommandingPlayer(Creature c)
    {
        if (PlayerPartyControl && c.CommandingPlayer != GameObject.InvalidId)
            return Get<Creature>(c.CommandingPlayer) is { IsPC: true, IsDestroyed: false } p ? p : null;
        return ControllingPlayer(c);
    }

    /// <summary>Item level restriction: the highest item value (gp) a character of this level may equip.</summary>
    public bool CanEquipByLevel(Creature c, Item item)
    {
        if (!ItemLevelRestrictions || !c.IsPC) return true;
        var table = Resources.TwoDA("itemvalue");
        int level = Math.Clamp(c.TotalLevel, 1, Math.Max(1, table.RowCount));
        int max = table.GetInt(level - 1, "MAXSINGLEITEMVALUE", int.MaxValue);
        return Inventory.Rules.GoldValue(item) <= max;
    }
}
