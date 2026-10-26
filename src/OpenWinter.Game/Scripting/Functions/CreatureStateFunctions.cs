using System.Numerics;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>Creature flags and modes, associates (familiars, companions), and area/tile lighting.</summary>
static class CreatureStateFunctions
{
    static Creature? Cr(Call c, int i) => c.Obj<Creature>(i);

    // ---------------------------------------------------------------- AI level, combat, resting

    [NwFunc("GetAILevel")]
    static object? AILevel(Call c)
    {
        // A placeable reads AI_LEVEL_VERY_LOW, in an area without players (nwserver ai_level_auto) and with one (nwmain
        // playerai).
        if (Cr(c, 0) is not { } cr) return c.Obj(0) is { Area: not null } ? 0 : -1;
        // AI_LEVEL_VERY_HIGH set by script reads back as AI_LEVEL_HIGH (nwserver ai_events).
        if (cr.AILevel >= 0) return Math.Min(cr.AILevel, 3);
        // The game picks (World.EffectiveAILevel): a player's creature VERY_HIGH, her associates HIGH, normal in combat,
        // low near players, else very low.
        return c.World.EffectiveAILevel(cr);
    }

    /// <summary>
    /// SetAILevel locks the level; AI_LEVEL_DEFAULT (-1) unlocks it. Values above AI_LEVEL_HIGH are clamped to HIGH and
    /// values below -1 to AI_LEVEL_VERY_LOW, which they then lock (nwserver ai_level_auto: 9 read HIGH; -5 read VERY_LOW
    /// and stayed there in a fight).
    /// </summary>
    [NwFunc("SetAILevel")]
    static object? SetAILevel(Call c)
    {
        if (Cr(c, 0) is { IsPC: false } cr)
        {
            cr.AILevel = c.I(1) < -1 ? 0 : Math.Min(c.I(1), 3);
            c.World.NoteAILevel(cr, c.World.EffectiveAILevel(cr));
        }
        return null;
    }

    [NwFunc("GetIsInCombat")] static object? InCombat(Call c) => Cr(c, 0)?.InCombat == true ? 1 : 0;
    [NwFunc("GetIsResting")] static object? Resting(Call c) => Cr(c, 0)?.IsResting == true ? 1 : 0;

    // ---------------------------------------------------------------- action modes

    const int ModeDetect = 0, ModeStealth = 1, ModeDefensiveCast = 10;

    [NwFunc("GetActionMode")]
    static object? GetMode(Call c)
    {
        if (Cr(c, 0) is not { } cr) return 0;
        CreatureRules.EnsureInit(c.World, cr);
        return c.I(1) switch
        {
            ModeStealth => cr.StealthMode ? 1 : 0,
            ModeDetect => Skills.SkillSystem.ActiveDetect(c.World, cr) ? 1 : 0,
            int m => cr.ActionModes.Contains(m) ? 1 : 0,
        };
    }

    [NwFunc("SetActionMode")]
    static object? SetMode(Call c)
    {
        if (Cr(c, 0) is not { } cr) return null;
        CreatureRules.EnsureInit(c.World, cr);
        int mode = c.I(1);
        bool on = c.I(2) != 0;
        if (mode < 0) return null;
        // Stealth (refused while attacking) and detect mode belong to the skills package.
        if (mode is ModeStealth or ModeDetect) { c.World.System<Skills.SkillSystem>().SetActionMode(cr, mode, on); return null; }
        // Flurry of Blows doesn't take on a creature without monk levels, even with the feat (nwserver, combat_modes).
        if (on && mode == Combat.CombatK.ModeFlurry && CreatureRules.ClassLevel(cr, NwRules.ClassMonk) == 0) return null;
        if (mode == ModeDefensiveCast && !c.World.System<Combat.CombatSystem>().SetDefensiveCasting(cr, on)) return null;
        Requests.AbilityRequests.ApplyCombatMode(c.World, cr, mode, on);
        return null;
    }

    [NwFunc("GetStealthMode")] static object? StealthMode(Call c) => Cr(c, 0)?.StealthMode == true ? 1 : 0;
    [NwFunc("GetDetectMode")] static object? DetectMode(Call c) => Cr(c, 0) is { } cr ? Skills.SkillSystem.DetectModeValue(c.World, cr) : 0;

    [NwFunc("GetDefensiveCastingMode")]
    static object? DefensiveCasting(Call c)
    {
        if (Cr(c, 0) is not { } cr) return 0;
        CreatureRules.EnsureInit(c.World, cr);
        return cr.ActionModes.Contains(ModeDefensiveCast) ? 1 : 0;
    }

    // ---------------------------------------------------------------- death and corpse flags

    [NwFunc("SetIsDestroyable")]
    static object? SetDestroyable(Call c)
    {
        // int bDestroyable, int bRaiseable, int bSelectableWhenDead, object oObject
        if ((c.World.Get(c.O(3)) ?? c.Self) is Creature cr)
        {
            CreatureRules.EnsureInit(c.World, cr);
            cr.IsDestroyable = c.I(0) != 0;
            cr.IsRaiseable = c.I(1) != 0;
            cr.SelectableWhenDead = c.I(2) != 0;
            c.World.MarkChanged(cr);
        }
        return null;
    }

    [NwFunc("GetIsDestroyable")] static object? Destroyable(Call c) => Cr(c, 0)?.IsDestroyable == true ? 1 : 0;
    [NwFunc("GetIsRaiseable")] static object? Raiseable(Call c) => Cr(c, 0)?.IsRaiseable == true ? 1 : 0;

    [NwFunc("GetIsSelectableWhenDead")]
    static object? SelectableWhenDead(Call c)
    {
        if (Cr(c, 0) is not { } cr) return 0;
        CreatureRules.EnsureInit(c.World, cr);
        return cr.SelectableWhenDead ? 1 : 0;
    }

    [NwFunc("SetLootable")]
    static object? SetLootable(Call c)
    {
        if (Cr(c, 0) is { IsPC: false, IsDead: false } cr) cr.Lootable = c.I(1) != 0;
        return null;
    }

    [NwFunc("GetLootable")] static object? Lootable(Call c) => Cr(c, 0)?.Lootable == true ? 1 : 0;

    [NwFunc("SetImmortal")]
    static object? SetImmortal(Call c)
    {
        if (Cr(c, 0) is { } cr) cr.IsImmortal = c.I(1) != 0;
        return null;
    }

    [NwFunc("GetImmortal")] static object? Immortal(Call c) => Cr(c, 0)?.IsImmortal == true ? 1 : 0;

    [NwFunc("GetBodyBag")]
    static object? BodyBag(Call c) => c.Obj(0) switch
    {
        Creature cr => cr.BodyBag,
        Placeable p => (int)p.Template.GetInt("BodyBag"),
        _ => -1,
    };

    [NwFunc("SetBodyBag")]
    static object? SetBodyBag(Call c)
    {
        switch (c.Obj(0))
        {
            case Creature cr: cr.BodyBag = c.I(1); break;
            case Placeable p: p.Template.SetByte("BodyBag", c.I(1)); break;
        }
        return null;
    }

    [NwFunc("GetIsCreatureDisarmable")]
    static object? Disarmable(Call c) =>
        Cr(c, 0) is { Disarmable: true } cr && cr.Equipment.TryGetValue(EquipSlot.RightHand, out var weapon) && weapon.Droppable && !weapon.Plot ? 1 : 0;

    [NwFunc("GetCreatureStartingPackage")]
    static object? StartingPackage(Call c)
    {
        if (Cr(c, 0) is not { } cr) return 255;
        CreatureRules.EnsureInit(c.World, cr);
        return cr.StartingPackage;
    }

    // ---------------------------------------------------------------- familiars and animal companions

    // The choice a creature has stored, feat or not (nwserver nwnx_creature_misc: a bandit without the feats reads 0
    // and "", and what a script set); 255 internally means none was ever chosen.
    [NwFunc("GetFamiliarCreatureType")]
    static object? FamiliarType(Call c) => Cr(c, 0) is { } cr ? Chosen(c, cr, NwRules.AssociateFamiliar) : 255;

    [NwFunc("GetAnimalCompanionCreatureType")]
    static object? CompanionType(Call c) => Cr(c, 0) is { } cr ? Chosen(c, cr, NwRules.AssociateCompanion) : 255;

    static int Chosen(Call c, Creature cr, int associate)
    {
        CreatureRules.EnsureInit(c.World, cr);
        int now = associate == NwRules.AssociateFamiliar ? cr.FamiliarType : cr.CompanionType;
        if (now != 255) return now;
        return CreatureRules.HasAssociateAbility(c.World, cr, associate) ? 255 : 0;
    }

    [NwFunc("GetFamiliarName")]
    static object? FamiliarName(Call c)
    {
        if (Cr(c, 0) is not { } cr) return "";
        CreatureRules.EnsureInit(c.World, cr);
        return cr.FamiliarName;
    }

    [NwFunc("GetAnimalCompanionName")]
    static object? CompanionName(Call c)
    {
        if (Cr(c, 0) is not { } cr) return "";
        CreatureRules.EnsureInit(c.World, cr);
        return cr.CompanionName;
    }

    [NwFunc("SummonAnimalCompanion")]
    static object? SummonCompanion(Call c)
    {
        if ((c.World.Get(c.O(0)) ?? c.Self) is Creature master) CreatureRules.SummonAssociate(c.World, master, NwRules.AssociateCompanion);
        return null;
    }

    [NwFunc("SummonFamiliar")]
    static object? SummonFamiliar(Call c)
    {
        if ((c.World.Get(c.O(0)) ?? c.Self) is Creature master) CreatureRules.SummonAssociate(c.World, master, NwRules.AssociateFamiliar);
        return null;
    }

    [NwFunc("UnpossessFamiliar")]
    static object? Unpossess(Call c)
    {
        if (c.Obj<Creature>(0) is { } creature) c.World.System<Players.FamiliarPossession>().Unpossess(creature);
        return null;
    }


    // ---------------------------------------------------------------- area and tile lighting

    static Area? AreaOf(Call c, int i) => (c.World.Get(c.O(i)) ?? c.Self) switch
    {
        Area a => a,
        { Area: { } a } => a,
        _ => null,
    };

    /// <summary>0xRRGGBB (scripts) to and from 0xBBGGRR (area files).</summary>
    static uint SwapRedBlue(uint c) => ((c & 0xFF) << 16) | (c & 0xFF00) | ((c >> 16) & 0xFF);

    static readonly string[] AreaLightFields = { "MoonAmbientColor", "MoonDiffuseColor", "SunAmbientColor", "SunDiffuseColor" };

    [NwFunc("GetFogColor")]
    static object? FogColor(Call c)
    {
        if (AreaOf(c, 1) is not { } a) return 0;
        return c.I(0) switch
        {
            1 => (int)SwapRedBlue((uint)a.Are.GetInt("SunFogColor")),
            2 => (int)SwapRedBlue((uint)a.Are.GetInt("MoonFogColor")),
            _ => 0,
        };
    }

    [NwFunc("SetFogColor")]
    static object? SetFogColor(Call c)
    {
        // int nFogType, int nFogColor, object oArea, float fFadeTime
        if (AreaOf(c, 2) is not { } a) return null;
        uint color = unchecked((uint)c.I(1)) & 0xFFFFFF;
        // Scripts use 0xRRGGBB; the area keeps 0xBBGGRR (what clients get on entering it).
        if (c.I(0) is 0 or 1) a.Are.SetDWord("SunFogColor", SwapRedBlue(color));
        if (c.I(0) is 0 or 2) a.Are.SetDWord("MoonFogColor", SwapRedBlue(color));
        c.World.Output?.Notify("fog-color", a, new { fogType = c.I(0), color, fadeTime = c.F(3) });
        return null;
    }

    [NwFunc("GetAreaLightColor")]
    static object? AreaLight(Call c)
    {
        int type = c.I(0);
        if (AreaOf(c, 1) is not { } a || type < 0 || type > 3) return 0;
        return (int)SwapRedBlue((uint)a.Are.GetInt(AreaLightFields[type]));
    }

    [NwFunc("SetAreaLightColor")]
    static object? SetAreaLight(Call c)
    {
        // int nColorType, int nColor, object oArea, float fFadeTime
        int type = c.I(0);
        if (AreaOf(c, 2) is not { } a || type < 0 || type > 3) return null;
        uint color = unchecked((uint)c.I(1)) & 0xFFFFFF;
        a.Are.SetDWord(AreaLightFields[type], SwapRedBlue(color));
        c.World.Output?.Notify("area-light-color", a, new { colorType = type, color, fadeTime = c.F(3) });
        return null;
    }

    /// <summary>The tile a location's vector names (tile grid x, y).</summary>
    static (Area Area, int Index, int X, int Y)? Tile(Call c, int i)
    {
        var loc = c.L(i);
        if (c.World.Get(loc.Area) is not Area a) return null;
        int x = (int)loc.Position.X, y = (int)loc.Position.Y;
        if (x < 0 || y < 0 || x >= a.Width || y >= a.Height) return null;
        int index = y * a.Width + x;
        return index < a.Tiles.Length ? (a, index, x, y) : null;
    }

    static void NotifyTile(Call c, Area a, int index, int x, int y)
    {
        var t = a.Tiles[index];
        c.World.Output?.Notify("tile-light", a, new { x, y, mainLight1 = (int)t.MainLight1, mainLight2 = (int)t.MainLight2, sourceLight1 = (int)t.SourceLight1, sourceLight2 = (int)t.SourceLight2 });
    }

    [NwFunc("SetTileMainLightColor")]
    static object? SetTileMain(Call c)
    {
        if (Tile(c, 0) is not { } t) return null;
        t.Area.Tiles[t.Index].MainLight1 = (byte)Math.Clamp(c.I(1), 0, 255);
        t.Area.Tiles[t.Index].MainLight2 = (byte)Math.Clamp(c.I(2), 0, 255);
        NotifyTile(c, t.Area, t.Index, t.X, t.Y);
        return null;
    }

    [NwFunc("SetTileSourceLightColor")]
    static object? SetTileSource(Call c)
    {
        if (Tile(c, 0) is not { } t) return null;
        t.Area.Tiles[t.Index].SourceLight1 = (byte)Math.Clamp(c.I(1), 0, 255);
        t.Area.Tiles[t.Index].SourceLight2 = (byte)Math.Clamp(c.I(2), 0, 255);
        NotifyTile(c, t.Area, t.Index, t.X, t.Y);
        return null;
    }

    // The original server keeps no tile lighting: these always report -1 there (the client applies Set*LightColor).
    [NwFunc("GetTileMainLight1Color")] static object? TileMain1(Call c) => -1;
    [NwFunc("GetTileMainLight2Color")] static object? TileMain2(Call c) => -1;
    [NwFunc("GetTileSourceLight1Color")] static object? TileSource1(Call c) => -1;
    [NwFunc("GetTileSourceLight2Color")] static object? TileSource2(Call c) => -1;
}
