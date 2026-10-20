using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Net;
using Xunit;
using Xunit.Abstractions;

namespace OpenWinter.Tests;

public class EffectsTests
{
    readonly ITestOutputHelper _out;
    public EffectsTests(ITestOutputHelper output) { _out = output; }

    sealed class Recorder : IWorldOutput
    {
        public readonly List<(string Kind, uint Subject, string Json)> Notes = new();
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) { }
        public void FloatingText(Creature over, string text, bool toFaction) { }
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null) =>
            Notes.Add((kind, subject?.Id ?? GameObject.InvalidId, System.Text.Json.JsonSerializer.Serialize(data)));
        public int Count(string kind) => Notes.Count(n => n.Kind == kind);
    }

    static string ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "scripts"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests", "scripts");
    }

    sealed record Setup(World World, Area Area, Recorder Output, List<string> Log)
    {
        public LocalVariables M => World.Module.Locals;
        public EffectConstants K => EffectConstants.Of(World);
        public int Run(string script, GameObject self) => World.Scripts!.Run(script, self.Id);
        public void Step(float seconds, float dt = 0.25f)
        {
            for (float t = 0; t < seconds - 1e-4f; t += dt) World.Step(dt);
        }
        public Creature Spawn(string tag, Vector3 pos, int hp = 20)
        {
            var c = new Creature { Tag = tag, MaxHitPoints = hp, CurrentHitPoints = hp };
            World.AddToArea(c, Area, pos, 0);
            return c;
        }
    }

    static Setup Make()
    {
        var rm = new ResMan(TestData.Paths);
        rm.Add(new DirectorySource(ScriptsDir()));
        rm.AddBaseGame();
        var world = new World(rm);
        world.EnableScripts();
        world.Register(world.Module);
        world.Module.Tag = "test_module";
        var log = new List<string>();
        world.Scripts!.Log = log.Add;
        var rec = new Recorder();
        world.Output = rec;
        var area = new Area { Tag = "fx_area", ResRef = "fx_area" };
        world.Register(area);
        world.Areas.Add(area);
        return new Setup(world, area, rec, log);
    }

    void Dump(Setup s)
    {
        foreach (var l in s.Log) _out.WriteLine(l);
        Assert.DoesNotContain(s.Log, l => l.Contains("not implemented") && (l.Contains("Effect") || l.Contains("ItemProperty")));
    }

    [DataFact]
    public void VisualPayloadPreservesCreatorForInstantAndReplayedEffects()
    {
        var s = Make();
        var caster = s.Spawn("caster", new Vector3(5, 5, 0));
        var target = s.Spawn("target", new Vector3(15, 5, 0));
        var effect = new Effect
        {
            Type = s.K.VisualEffect, Creator = caster.Id,
            Ints = { [0] = 181 }, Floats = { [0] = 1 }
        };
        var system = s.World.System<EffectSystem>();
        system.Apply(effect, s.K.Instant, target, 0);
        var note = Assert.Single(s.Output.Notes, n => n.Kind == "vfx");
        Assert.Equal(target.Id, note.Subject);
        using var payload = System.Text.Json.JsonDocument.Parse(note.Json);
        Assert.Equal(caster.Id, payload.RootElement.GetProperty("creator").GetUInt32());
        effect.Ints[0] = 147; // Only duration rows are retained and replayed by native.
        system.Apply(effect, s.K.Permanent, target, 0);
        var replay = Assert.Single(system.LastingVisuals(target));
        var data = System.Text.Json.JsonSerializer.SerializeToElement(replay.Data);
        Assert.Equal(caster.Id, data.GetProperty("creator").GetUInt32());
        s.Output.Notes.Clear();
        system.ApplyAtLocation(effect, s.K.Instant, new Location(s.Area.Id, new Vector3(12, 8, 0), 0), 0);
        using var location = System.Text.Json.JsonDocument.Parse(Assert.Single(s.Output.Notes, n => n.Kind == "vfx").Json);
        Assert.Equal(caster.Id, location.RootElement.GetProperty("creator").GetUInt32());
    }

    [DataFact]
    public void AppliedAreaEffectSnapshotsItsSpellContextWithoutChangingConstructorValue()
    {
        var s = Make();
        var caster = s.Spawn("caster", new Vector3(10, 10, 0));
        caster.LastSpell = new SpellCastInfo { SpellId = 6, CasterLevel = 20, MetaMagic = 2, SaveDC = 16 };
        var effect = new Effect { Type = 20, Creator = caster.Id, SpellId = 6, CasterLevel = 20 };
        effect.Ints[0] = 37;
        s.World.System<EffectSystem>().Apply(effect, s.K.Temporary, caster, 30);
        var applied = Assert.Single(caster.Effects, e => e.Type == 20);
        Assert.Equal(new[] { 37, 1, 0, 2, 16, 65535, 0, 0 }, applied.Ints);
        Assert.Equal(new[] { 37, 0, 0, 0, 0, 0, 0, 0 }, effect.Ints);
        caster.LastSpell = new SpellCastInfo { SpellId = 79, CasterLevel = 20, SaveDC = 21 };
        Assert.Equal(16, applied.Ints[4]);
        var scriptEffect = effect.Copy();
        scriptEffect.Creator = s.World.Module.Id;
        var other = s.Spawn("other", new Vector3(20, 10, 0));
        s.World.System<EffectSystem>().Apply(scriptEffect, s.K.Temporary, other, 30);
        Assert.Equal(new[] { 37, 0, 0, 0, 0, 0, 0, 0 }, Assert.Single(other.Effects).Ints);
    }

    [DataFact]
    public void OneShotVisualRowsDoNotRetainOrRemoveLinkedBuffs()
    {
        var s = Make();
        var k = s.K;
        var fx = s.World.System<EffectSystem>();
        foreach (int row in new[] { 18, 181, 233 })
            foreach (int durationType in new[] { k.Temporary, k.Permanent })
            {
                var target = s.Spawn($"vfx_{row}_{durationType}", new Vector3(5, 5, 0));
                var visual = new Effect { Type = k.VisualEffect, Ints = { [0] = row }, SubType = k.Magical };
                var buff = new Effect
                {
                    Type = k["EFFECT_TYPE_ABILITY_INCREASE"],
                    Ints = { [0] = 0, [1] = 3 }, SubType = k.Magical
                };
                var link = new Effect { Type = buff.Type };
                link.Linked.Add(visual);
                link.Linked.Add(buff);
                fx.Apply(link, durationType, target, .5f);
                var retained = Assert.Single(target.Effects);
                Assert.Equal(buff.Type, retained.Type);
                var message = Assert.Single(s.Output.Notes, n => n.Kind == "vfx" && n.Subject == target.Id);
                using var payload = System.Text.Json.JsonDocument.Parse(message.Json);
                Assert.Equal(row, payload.RootElement.GetProperty("vfx").GetInt32());
                Assert.Equal(0, payload.RootElement.GetProperty("duration").GetSingle());
                Assert.Equal(0, payload.RootElement.GetProperty("id").GetInt64());
                fx.Remove(target, retained);
                Assert.Empty(target.Effects);
                Assert.Single(s.Output.Notes, n => n.Kind == "effect-removed" && n.Subject == target.Id);
            }
        foreach (int row in new[] { 147, 73, 127 })
        {
            var target = s.Spawn($"lasting_{row}", new Vector3(5, 5, 0));
            fx.Apply(new Effect { Type = k.VisualEffect, Ints = { [0] = row } }, k.Temporary, target, .5f);
            Assert.Single(target.Effects);
            s.Step(.75f);
            Assert.Empty(target.Effects);
        }
    }

    [DataFact]
    public void ConstructsLinksIteratesAndRemoves()
    {
        var s = Make();
        var caster = s.Spawn("fx_caster", new Vector3(10, 10, 0));
        var target = s.Spawn("fx_target", new Vector3(12, 10, 0));
        s.Run("effects_apply", caster);
        Dump(s);
        var m = s.M.Ints;

        Assert.Equal(36, m["haste_type"]);
        Assert.Equal(0, m["heal_hidden"]);
        Assert.Equal(96, m["heal_all"]);
        Assert.Equal(0, m["heal_invalid"]);
        Assert.Equal(8, m["default_subtype"]);
        Assert.Equal(-1, m["default_spellid"]);
        // The original's layout: AC type, amount, versus race (RACIAL_TYPE_INVALID when none).
        Assert.Equal(4, m["ac_int0"]);
        Assert.Equal(3, m["ac_int1"]);
        Assert.Equal(28, m["ac_int2"]);
        Assert.Equal(2.5f, s.M.Floats["vfx_scale"]);
        Assert.Equal("", s.M.Strings["orig_tag"]);
        Assert.Equal("copy", s.M.Strings["copy_tag"]);

        // Visual + AC (linked) + haste.
        Assert.Equal(3, m["count"]);
        Assert.Equal(2, m["super"]);
        Assert.Equal(1, m["same_link"]);
        Assert.Equal(10, m["remaining"]);
        Assert.Equal(10, m["link_duration"]);
        Assert.Equal(1, m["link_durtype"]);
        Assert.Equal(1, m["creator_self"]);
        // Removing the AC effect took the linked visual with it.
        Assert.Equal(1, m["after_remove"]);
        Assert.Equal(1, m["has_spell"]);
        Assert.Equal(0, m["has_other_spell"]);
        // Paralysis immunity rejects the paralysis and its linked visual; IgnoreEffectImmunity gets through.
        Assert.Equal(0, m["immune_added"]);
        Assert.Equal(1, m["ignore_added"]);

        Assert.True(s.Output.Count("effect-applied") >= 5);
        Assert.True(s.Output.Count("effect-removed") >= 2);
        Assert.Contains(s.Output.Notes, n => n.Kind == "vfx" && n.Subject == target.Id);
    }

    [DataFact]
    public void TemporaryEffectsExpireWithTheirLink()
    {
        var s = Make();
        var target = s.Spawn("fx_target", new Vector3(5, 5, 0));
        var k = s.K;
        var link = new Effect { Type = k["EFFECT_TYPE_HASTE"], SubType = k.Magical };
        link.Linked.Add(new Effect { Type = k["EFFECT_TYPE_HASTE"], SubType = k.Magical });
        link.Linked.Add(new Effect { Type = k.VisualEffect, SubType = k.Magical });
        var fx = s.World.System<EffectSystem>();
        fx.Apply(link, k.Temporary, target, 2f);
        fx.Apply(new Effect { Type = k["EFFECT_TYPE_SLOW"], SubType = k.Magical }, k.Permanent, target, 0);
        Assert.Equal(3, target.Effects.Count);
        Assert.Single(target.Effects.Select(e => e.LinkId).Where(l => l == target.Effects[0].LinkId).Skip(1));

        s.Step(1.5f);
        Assert.Equal(3, target.Effects.Count);
        Assert.True(EffectQueries.Has(target, k["EFFECT_TYPE_HASTE"]));
        s.Step(0.75f);
        Assert.Single(target.Effects);
        Assert.False(EffectQueries.Has(target, k["EFFECT_TYPE_HASTE"]));
        Assert.Equal(2, s.Output.Count("effect-removed"));
    }

    [DataFact]
    public void CorpseBeamsKeepTheirLifetimeUnlessLinkedGameplayEffectIsRemoved()
    {
        var s = Make();
        var k = s.K;
        var fx = s.World.System<EffectSystem>();
        var source = s.Spawn("beam_source", new Vector3(5, 5, 0));
        var target = s.Spawn("beam_target", new Vector3(7, 5, 0));
        Effect Beam() => new()
        {
            Type = k.Beam, SubType = k.Magical,
            Objects = { [0] = source.Id }, Ints = { [0] = 73 }
        };
        fx.Apply(Beam(), k.Temporary, target, 1);
        fx.Apply(Beam(), k.Permanent, target, 0);
        var linked = new Effect { Type = k.Beam };
        linked.Linked.Add(Beam());
        linked.Linked.Add(new Effect
        {
            Type = k["EFFECT_TYPE_ABILITY_INCREASE"],
            SubType = k.Magical, Ints = { [0] = 0, [1] = 3 }
        });
        fx.Apply(linked, k.Permanent, target, 0);
        Assert.Equal(4, target.Effects.Count);
        HitPoints.Kill(s.World, target, source.Id);
        Assert.True(target.IsDead);
        Assert.Equal(2, target.Effects.Count);
        Assert.All(target.Effects, e => Assert.Equal(k.Beam, e.Type));
        s.Step(1.25f);
        var permanent = Assert.Single(target.Effects);
        Assert.Equal(k.Permanent, permanent.DurationType);
        s.World.Destroy(source);
        Assert.Same(permanent, Assert.Single(target.Effects));
        fx.Remove(target, permanent);
        Assert.Empty(target.Effects);
    }

    [DataFact]
    public void QueriesHonourVersusFilters()
    {
        var s = Make();
        var k = s.K;
        var target = s.Spawn("fx_target", new Vector3(5, 5, 0));
        var evil = s.Spawn("fx_evil", new Vector3(6, 5, 0));
        evil.GoodEvil = 0;
        var good = s.Spawn("fx_good", new Vector3(7, 5, 0));
        good.GoodEvil = 100;
        var fx = s.World.System<EffectSystem>();
        int acInc = k["EFFECT_TYPE_AC_INCREASE"];
        fx.Apply(new Effect { Type = acInc, Ints = { [0] = 2 } }, k.Permanent, target, 0);
        fx.Apply(new Effect { Type = acInc, Ints = { [0] = 3 }, VersusGoodEvil = 3 }, k.Permanent, target, 0);
        Assert.Equal(2, EffectQueries.Sum(target, acInc));
        Assert.Equal(5, EffectQueries.Sum(target, acInc, versus: evil));
        Assert.Equal(2, EffectQueries.Sum(target, acInc, versus: good));
        Assert.Equal(3, EffectQueries.Max(target, acInc, versus: evil));
    }

    [DataFact]
    public void HealClampsDamageKillsAndResurrects()
    {
        var s = Make();
        var caster = s.Spawn("fx_caster", new Vector3(10, 10, 0));
        var target = s.Spawn("fx_target", new Vector3(12, 10, 0));
        target.CurrentHitPoints = 10;
        target.Scripts["ScriptDeath"] = "effects_ondeath";
        uint killer = 0;
        void OnDied(GameObject victim, uint by) { if (victim == target) killer = by; }
        HitPoints.Died += OnDied;
        try
        {
            void Stage(int n) { s.M.Ints["stage"] = n; s.Run("effects_hp", caster); }
            Stage(1);
            Assert.Equal(20, target.CurrentHitPoints);
            Stage(2);
            Assert.Equal(15, target.CurrentHitPoints);
            Stage(3); // 5 temporary hit points soak 5 of 8
            Assert.Equal(12, target.CurrentHitPoints);
            Assert.False(EffectQueries.Has(target, s.K.TemporaryHitpoints));
            Stage(4); // cold resistance 5 against 7
            Assert.Equal(10, target.CurrentHitPoints);
            Assert.False(target.IsDead);
            Stage(5);
            Assert.True(target.IsDead);
            Assert.True(target.CurrentHitPoints <= 0);
            Assert.Equal(caster.Id, killer);
            Assert.Empty(target.Effects); // death strips (non-unyielding) effects
            s.World.Step(0.1f);
            Assert.Equal("fx_target", s.M.Strings["died"]);
            Stage(1); // the dead can't be healed
            Assert.True(target.CurrentHitPoints <= 0);
            target.IsRaiseable = false;
            int corpseHp = target.CurrentHitPoints;
            Stage(6);
            Assert.True(target.IsDead);
            Assert.Equal(corpseHp, target.CurrentHitPoints);
            target.IsRaiseable = true;
            Stage(6);
            Assert.False(target.IsDead);
            Assert.Equal(1, target.CurrentHitPoints);
            Dump(s);
        }
        finally { HitPoints.Died -= OnDied; }
    }

    [DataFact]
    public void PlayersDyeThenDie()
    {
        var s = Make();
        var pc = s.Spawn("fx_pc", new Vector3(10, 10, 0));
        pc.Player = new PlayerSession { Connection = LoopbackConnection.CreatePair().Server, Creature = pc };
        s.World.Module.Scripts["Mod_OnPlrDying"] = "effects_pcdying";
        s.World.Module.Scripts["Mod_OnPlrDeath"] = "effects_pcdeath";

        HitPoints.Damage(s.World, pc, GameObject.InvalidId, 25, s.K.DamageMagical);
        s.World.Step(0.1f);
        Assert.Equal(-5, pc.CurrentHitPoints);
        Assert.False(pc.IsDead);
        Assert.Equal("fx_pc", s.M.Strings["pc_dying"]);
        Assert.False(s.M.Strings.ContainsKey("pc_died"));

        HitPoints.Damage(s.World, pc, GameObject.InvalidId, 10, s.K.DamageMagical);
        s.World.Step(0.1f);
        Assert.True(pc.IsDead);
        Assert.Equal("fx_pc", s.M.Strings["pc_died"]);
        Dump(s);
    }

    [DataFact]
    public void ItemPropertiesAddRemoveIterateAndExpire()
    {
        var s = Make();
        var caster = s.Spawn("fx_caster", new Vector3(10, 10, 0));
        var item = new Item { Tag = "fx_item" };
        s.World.Register(item);
        s.Run("effects_ip", caster);
        Dump(s);
        var m = s.M.Ints;
        Assert.Equal(16, m["fire_type"]);
        Assert.Equal(10, m["fire_sub"]);
        Assert.Equal(4, m["fire_table"]);
        Assert.Equal(7, m["fire_value"]);
        Assert.Equal(-1, m["fire_param1"]); // script-built properties have no Param1 (as on the original server)
        Assert.Equal(24, m["race_sub"]);
        Assert.Equal(-1, m["race_param1"]);
        Assert.Equal(10, m["race_param1value"]);
        Assert.Equal(2, m["race_value"]);
        Assert.Equal(-1, m["light_param1"]);
        Assert.Equal(0, m["light_param1value"]);
        Assert.Equal(43, m["custom_type"]);
        Assert.Equal(3, m["ip_count"]); // the instant one was refused
        Assert.Equal(1, m["has_haste"]);
        Assert.Equal(0, m["has_keen"]);
        Assert.Equal(1, m["haste_durtype"]);
        Assert.Equal(5, m["haste_remaining"]);
        Assert.Equal(2, m["after_remove"]);
        Assert.Equal(1, m["after_copy_remove"]);

        Assert.Single(item.Properties);
        s.Step(5.5f);
        Assert.Empty(item.Properties);
    }

    [DataFact]
    public void AreaOfEffectTracksEnterExitHeartbeatAndExpiry()
    {
        var s = Make();
        var caster = s.Spawn("fx_caster", new Vector3(10, 10, 0));
        var target = s.Spawn("fx_target", new Vector3(12, 10, 0));
        s.Spawn("fx_far", new Vector3(30, 30, 0));
        s.Run("effects_aoe", caster);
        Dump(s);
        var aoe = Assert.Single(s.Area.Objects.OfType<AreaOfEffect>());
        Assert.Equal(caster.Id, aoe.Creator);
        Assert.Equal(5f, aoe.Radius);
        Assert.Equal(2, s.M.Ints["aoe_in"]);
        Assert.Contains(s.Output.Notes, n => n.Kind == "aoe-created");
        Assert.Contains(s.Output.Notes, n => n.Kind == "vfx" && n.Subject == caster.Id);
        // Effects made in the AoE's scripts belong to its creator.
        var dec = Assert.Single(EffectQueries.Active(target, s.K["EFFECT_TYPE_AC_DECREASE"]));
        Assert.Equal(caster.Id, dec.Creator);

        target.Position = new Vector3(25, 10, 0);
        s.World.Step(0.25f);
        Assert.Equal(1, s.M.Ints["aoe_out"]);
        Assert.Equal("fx_target", s.M.Strings["aoe_out_last"]);

        s.Step(6.5f);
        Assert.True(s.M.Ints["aoe_hb"] >= 1);
        s.Step(6f);
        Assert.Empty(s.Area.Objects.OfType<AreaOfEffect>());
        Assert.Equal(2, s.M.Ints["aoe_out"]);
        Assert.Equal("fx_caster", s.M.Strings["aoe_out_last"]);
        Assert.Single(s.Output.Notes, n => n.Kind == "aoe-removed");
    }

    [DataFact]
    public void DestroyObjectEndsPersistentAreaVisualExactlyOnce()
    {
        var s = Make();
        var caster = s.Spawn("fx_caster", new Vector3(10, 10, 0));
        s.Run("effects_aoe", caster);
        var aoe = Assert.Single(s.Area.Objects.OfType<AreaOfEffect>());
        s.World.Destroy(aoe);
        s.World.Destroy(aoe);
        s.Step(14f);
        Assert.Empty(s.World.System<EffectSystem>().AreasOfEffect);
        Assert.Empty(s.Area.Objects.OfType<AreaOfEffect>());
        Assert.Equal(1, s.M.Ints["aoe_out"]);
        var removed = Assert.Single(s.Output.Notes, n => n.Kind == "aoe-removed");
        Assert.Equal(s.Area.Id, removed.Subject);
        using var payload = System.Text.Json.JsonDocument.Parse(removed.Json);
        Assert.Equal(aoe.Id, payload.RootElement.GetProperty("id").GetUInt32());
        Dump(s);
    }

    [DataFact]
    public void RunScriptAndRegenerateTick()
    {
        var s = Make();
        var caster = s.Spawn("fx_caster", new Vector3(10, 10, 0));
        caster.CurrentHitPoints = 5;
        s.Run("effects_rs", caster);
        Dump(s);
        Assert.Equal("payload", s.M.Strings["rs_data"]);
        Assert.Equal(1, s.M.Ints["rs_calls"]); // applied
        s.Step(5f);
        // Intervals at 1, 2 and 3 seconds, then removed at 3.5.
        Assert.Equal(5, s.M.Ints["rs_calls"]);
        Assert.Equal(15, caster.CurrentHitPoints);
    }

    [DataFact]
    public void PortraitIconsDeduplicateAndRemainUntilLastEffectExpires()
    {
        var s = Make();
        var pc = s.Spawn("icon_target", new Vector3(5, 5, 0));
        var fx = s.World.System<EffectSystem>();
        var effect = new Effect { Type = s.K.Icon, Ints = { [0] = 25 } };
        fx.Apply(effect, s.K.Temporary, pc, 1f);
        fx.Apply(effect, s.K.Temporary, pc, 3f);
        Assert.Equal(new[] { 25 }, fx.PortraitIcons(pc));
        s.Step(1.5f);
        Assert.Equal(new[] { 25 }, fx.PortraitIcons(pc));
        s.Step(2f);
        Assert.Empty(fx.PortraitIcons(pc));
    }

    [DataFact]
    public void SummonEffectsCancelArrivalAndKeepReplacementIdentity()
    {
        var s = Make();
        var master = s.Spawn("summoner", new Vector3(10, 10, 0));
        var fx = s.World.System<EffectSystem>();
        Effect Summon(float delay) => new()
        {
            Type = s.K.SummonCreature, SubType = s.K.Magical, Creator = master.Id,
            ScriptId = fx.NextId(), Strings = { [0] = "nw_s_badgerdire" },
            Floats = { [0] = delay }, Ints = { [0] = -1, [2] = -1 },
        };
        fx.ApplyAtLocation(Summon(2), s.K.Temporary, master.Location, 5);
        var cancelled = Assert.Single(master.Effects);
        fx.RemoveOnly(master, cancelled);
        s.Step(2.5f);
        Assert.DoesNotContain(s.Area.Objects.OfType<Creature>(), c => c.Master == master.Id);

        fx.ApplyAtLocation(Summon(2), s.K.Temporary, master.Location, 5);
        var first = Assert.Single(master.Effects);
        s.Step(2.5f);
        var old = Assert.IsType<Creature>(s.World.Get(first.SummonedObject));
        fx.ApplyAtLocation(Summon(0), s.K.Temporary, master.Location, 10);
        s.Step(.25f);
        Assert.True(old.IsDestroyed);
        var second = Assert.Single(master.Effects, e => e != first);
        var replacement = Assert.IsType<Creature>(s.World.Get(second.SummonedObject));
        s.Step(3f);
        Assert.DoesNotContain(first, master.Effects);
        Assert.False(replacement.IsDestroyed);
        fx.RemoveOnly(master, second);
        Assert.True(replacement.IsDestroyed);
        fx.RemoveOnly(master, second); // removing it twice is harmless
        Assert.Empty(master.Effects);
    }

    [DataFact]
    public void SummonExpiryFollowsApplicationAndSurvivesMasterDestruction()
    {
        var s = Make();
        var master = s.Spawn("summoner", new Vector3(10, 10, 0));
        var fx = s.World.System<EffectSystem>();
        var e = new Effect
        {
            Type = s.K.SummonCreature, Creator = master.Id, ScriptId = fx.NextId(),
            Strings = { [0] = "nw_s_badgerdire" }, Floats = { [0] = 2 },
            Ints = { [0] = -1, [2] = -1 },
        };
        fx.ApplyAtLocation(e, s.K.Temporary, master.Location, 5);
        var stored = Assert.Single(master.Effects);
        s.Step(2.5f);
        var summon = Assert.IsType<Creature>(s.World.Get(stored.SummonedObject));
        s.World.Destroy(master);
        s.Step(1);
        Assert.False(summon.IsDestroyed);
        s.Step(2);
        Assert.True(summon.IsDestroyed);
    }

    [DataFact]
    public void NonCreatureSummonOwnersNeverCreateOrphans()
    {
        var s = Make();
        var fx = s.World.System<EffectSystem>();
        foreach (int durationType in new[] { s.K.Instant, s.K.Temporary, s.K.Permanent })
        {
            var target = new Placeable();
            s.World.AddToArea(target, s.Area, new Vector3(10, 10, 0), 0);
            var e = new Effect
            {
                Type = s.K.SummonCreature, Creator = s.World.Module.Id, ScriptId = fx.NextId(),
                Strings = { [0] = "nw_s_badgerdire" }, Ints = { [0] = -1, [2] = -1 },
            };
            fx.ApplyAtLocation(e, durationType, target.Location, 1);
            fx.Apply(e, durationType, target, 1);
            s.Step(.25f);
            Assert.Empty(s.Area.Objects.OfType<Creature>());
            Assert.Equal(durationType == s.K.Instant ? 0 : 1, target.Effects.Count);
            s.Step(2);
            Assert.Equal(durationType == s.K.Permanent ? 1 : 0, target.Effects.Count);
        }
    }

    [DataFact]
    public void FamiliarDeathScriptCanWoundMasterBeforeCorpseDetaches()
    {
        var s = Make();
        foreach (int hp in new[] { 1, 2 })
        {
            var master = s.Spawn("familiar_master", new Vector3(10, 10, 0));
            master.Classes.Add(new CreatureClass { Class = NwRules.ClassWizard, Level = 20 });
            master.FamiliarType = 0;
            var familiar = CreatureRules.SummonAssociate(s.World, master, NwRules.AssociateFamiliar)!;
            Assert.NotNull(familiar);
            master.CurrentHitPoints = hp;
            HitPoints.Kill(s.World, familiar, GameObject.InvalidId);
            s.Step(.25f);
            Assert.False(master.IsDead);
            Assert.Equal(1, master.CurrentHitPoints);
            Assert.Equal(GameObject.InvalidId, familiar.Master);
            Assert.Equal(0, familiar.AssociateType);
        }
    }

    [DataFact]
    public void ExistingCreatureAdoptionRequiresBlankResrefAndDoesNotMoveOrDelay()
    {
        var s = Make();
        var master = s.Spawn("summoner", new Vector3(10, 10, 0));
        var existing = s.Spawn("existing", new Vector3(20, 20, 0));
        var start = existing.Position;
        var fx = s.World.System<EffectSystem>();
        Effect MakeSummon(string resref) => new()
        {
            Type = s.K.SummonCreature, Creator = master.Id, ScriptId = fx.NextId(),
            Strings = { [0] = resref }, Objects = { [0] = existing.Id },
            Floats = { [0] = 2 }, Ints = { [0] = -1, [2] = -1 },
        };
        fx.ApplyAtLocation(MakeSummon("nw_s_badgerdire"), s.K.Temporary, master.Location, 10);
        s.Step(2.5f);
        var named = Assert.Single(master.Effects);
        Assert.NotEqual(existing.Id, named.SummonedObject);
        Assert.Equal(GameObject.InvalidId, existing.Master);
        Assert.Equal(start, existing.Position);
        fx.RemoveOnly(master, named);
        Assert.False(existing.IsDestroyed);

        fx.ApplyAtLocation(MakeSummon(""), s.K.Temporary, master.Location, 5);
        var adoption = Assert.Single(master.Effects);
        Assert.Equal(existing.Id, adoption.SummonedObject); // immediate despite the delay argument
        Assert.Equal(master.Id, existing.Master);
        Assert.Equal(start, existing.Position);
        fx.ApplyAtLocation(MakeSummon(""), s.K.Temporary, master.Location, 20);
        Assert.Same(adoption, Assert.Single(master.Effects)); // already-owned target is rejected
        s.Step(5.5f);
        Assert.True(existing.IsDestroyed);
        Assert.Empty(master.Effects);
    }

    [DataFact]
    public void SummonedCorpseDetachesAfterDeathWithoutRemovingMastersEffect()
    {
        var s = Make();
        var master = s.Spawn("summon_master", new Vector3(10, 10, 0));
        var fx = s.World.System<EffectSystem>();
        fx.ApplyAtLocation(new Effect
        {
            Type = s.K.SummonCreature, Creator = master.Id, ScriptId = fx.NextId(),
            Strings = { [0] = "nw_s_badgerdire" }, Ints = { [0] = -1, [2] = -1 },
        }, s.K.Temporary, master.Location, 10);
        s.Step(.25f);
        var effect = Assert.Single(master.Effects);
        var summon = Assert.IsType<Creature>(s.World.Get(effect.SummonedObject));
        HitPoints.Kill(s.World, summon, GameObject.InvalidId);
        Assert.Equal(master.Id, summon.Master); // Still available to the queued death script.
        s.Step(.25f);
        Assert.Equal(GameObject.InvalidId, summon.Master);
        Assert.Equal(0, summon.AssociateType);
        Assert.DoesNotContain(summon.Id, master.Society.Associates);
        Assert.Same(effect, Assert.Single(master.Effects));
        s.Step(11);
        Assert.Empty(master.Effects);
        Assert.Null(s.World.Get(summon.Id));
    }

    [DataFact]
    public void CurseImmunityIsCapturedWhenEachAbilityPenaltyLands()
    {
        var s = Make();
        var fx = s.World.System<EffectSystem>();
        Creature Target(string tag)
        {
            var c = s.Spawn(tag, new Vector3(10, 10, 0), 100);
            Array.Fill(c.Abilities, 14);
            c.Classes.Add(new CreatureClass { Class = NwRules.ClassFighter, Level = 10 });
            CreatureRules.EnsureInit(s.World, c);
            return c;
        }
        int[] Scores(Creature c) => Enumerable.Range(0, 6).Select(a => CreatureRules.AbilityScore(s.World, c, a)).ToArray();
        Effect Curse() => new() { Type = NwRules.EffectCurse, Ints = { [0] = 2, [1] = 2, [2] = 2, [3] = 2, [4] = 2, [5] = 2 } };
        Effect Immunity() => new() { Type = NwRules.EffectImmunity, Ints = { [0] = NwRules.ImmunityAbilityDecrease } };

        var immune = Target("immune_first");
        var baseline = Scores(immune);
        int hp = CreatureRules.MaxHitPoints(s.World, immune);
        fx.Apply(Immunity(), s.K.Temporary, immune, 1);
        fx.Apply(Curse(), s.K.Permanent, immune, 0);
        s.Step(1.5f);
        var retained = Assert.Single(immune.Effects);
        Assert.Equal(NwRules.EffectCurse, retained.Type);
        Assert.All(retained.Ints.Take(6), x => Assert.Equal(2, x));
        Assert.Equal(baseline, Scores(immune)); // immunity expired; suppressed penalties do not return
        Assert.Equal(hp, CreatureRules.MaxHitPoints(s.World, immune));

        var reapplied = Target("reapplied");
        fx.Apply(retained, s.K.Permanent, reapplied, 0);
        s.Step(.25f);
        Assert.Equal(baseline.Select(x => x - 2), Scores(reapplied)); // suppression is re-evaluated for a new application

        var linked = Target("curse_before_immunity");
        var link = Curse();
        link.Linked.Add(Curse());
        link.Linked.Add(Immunity());
        fx.Apply(link, s.K.Permanent, linked, 0);
        s.Step(.25f);
        Assert.Equal(baseline.Select((x, a) => a == NwRules.AbilityCon ? x : x - 2), Scores(linked));
        Assert.Equal(hp, CreatureRules.MaxHitPoints(s.World, linked)); // delayed Constitution penalty meets the new immunity
    }


    [DataFact]
    public void FeatImmunityBlocksSleepAndFearIncludingLinkedVisuals()
    {
        var s = Make();
        var fx = s.World.System<EffectSystem>();
        foreach (var (feat, immunity, type) in new[]
        {
            (NwRules.FeatImmunityToSleep, NwRules.ImmunitySleep, NwRules.EffectSleep),
            (NwRules.FeatAuraOfCourage, NwRules.ImmunityFear, NwRules.EffectFrightened),
        })
        {
            var target = s.Spawn("immune", new Vector3(10, 10, 0));
            fx.Apply(new Effect { Type = s.K["EFFECT_TYPE_BONUS_FEAT"], Ints = { [0] = feat } }, s.K.Permanent, target, 0);
            Assert.True(CreatureRules.IsImmune(s.World, target, immunity));
            var link = new Effect { Type = type };
            link.Linked.Add(new Effect { Type = type });
            link.Linked.Add(new Effect { Type = s.K.VisualEffect, Ints = { [0] = s.K["VFX_DUR_GLOW_RED"] } });
            fx.Apply(link, s.K.Temporary, target, 10);
            Assert.Single(target.Effects); // only the immunity feat; no harmful effect or orphan visual
            fx.Apply(link.With(e => e.IgnoreImmunity = true), s.K.Temporary, target, 10);
            Assert.Contains(target.Effects, e => e.Type == type);
            Assert.Contains(target.Effects, e => e.Type == s.K.VisualEffect);
        }
    }

}
