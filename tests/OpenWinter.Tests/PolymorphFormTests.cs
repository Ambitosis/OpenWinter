using System.Numerics;
using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Players;
using OpenWinter.Game.Progression;
using OpenWinter.Game.Requests;
using OpenWinter.Game.Rules;
using OpenWinter.Game.SaveGames;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Spells;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Polymorph beyond equipment (docs/POLYMORPH.md): the form's sound set, EffectDeath against the form's bonus hit
/// points (parity suite fx_poly_death), casting and levelling while polymorphed (fx_poly_abilities), the change-back
/// visual, the portrait icons and the chat line (visual scenes polymorphpc and polymorphpc2), and save games.
/// </summary>
public class PolymorphFormTests
{
    const int Drow = 59, Bear = 21, Chicken = 40, Rakshasa = 88, WaterElemental = 14;

    sealed class Output : IWorldOutput
    {
        public readonly List<(string Kind, uint Subject, JsonElement Data)> Notes = [];
        public readonly List<string> Messages = [];
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null) =>
            Notes.Add((kind, subject?.Id ?? 0, JsonSerializer.SerializeToElement(data)));
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) => Messages.Add(text);
        public void FloatingText(Creature over, string text, bool toFaction) { }
    }

    sealed record Setup(World World, Area Area, Output Output)
    {
        public EffectConstants K => EffectConstants.Of(World);
        public EffectSystem Fx => World.System<EffectSystem>();

        public Creature Spawn(bool player = false)
        {
            var c = new Creature { MaxHitPoints = 12, CurrentHitPoints = 12, AppearanceType = 6, Race = 6, SoundSet = 177 };
            if (player)
            {
                var (_, connection) = LoopbackConnection.CreatePair();
                c.Player = new PlayerSession { PlayerName = "poly", Connection = connection };
            }
            c.Abilities[NwRules.AbilityCon] = 12;
            c.Classes.Add(new CreatureClass { Class = NwRules.ClassFighter, Level = 1 });
            World.AddToArea(c, Area, new Vector3(5, 5, 0), 0);
            CreatureRules.EnsureInit(World, c);
            return c;
        }

        public Effect Polymorph(Creature c, int row, float duration = 30)
        {
            var e = new Effect { Type = K["EFFECT_TYPE_POLYMORPH"], SubType = K.Magical, ScriptId = Fx.NextId() };
            e.Ints[0] = row;
            Fx.Apply(e, K.Temporary, c, duration);
            return c.Effects.First(x => x.Type == e.Type && !x.Hidden);
        }

        public void Damage(Creature c, int amount) => HitPoints.Damage(World, c, GameObject.InvalidId, new Dictionary<int, int> { [8] = amount }, 0);

        public void Step(float seconds) { for (float t = 0; t < seconds; t += 0.05f) World.Step(0.05f); }
    }

    static Setup Make()
    {
        var rm = new ResMan(TestData.Paths);
        rm.AddBaseGame();
        var output = new Output();
        var world = new World(rm) { Output = output };
        world.Register(world.Module);
        world.EnableScripts();
        var area = new Area { Tag = "poly_form", ResRef = "poly_form", Width = 4, Height = 4 };
        world.Register(area);
        world.Areas.Add(area);
        return new Setup(world, area, output);
    }

    [DataFact]
    public void TheFormsSoundSetLastsAsLongAsTheForm()
    {
        var s = Make();
        var c = s.Spawn();
        s.Polymorph(c, Chicken);
        Assert.Equal(21, c.SoundSet);
        // A form without a sound set of its own (the bear) keeps the creature's, also when it replaces the chicken.
        s.Polymorph(c, Bear);
        Assert.Equal(177, c.SoundSet);
        s.Polymorph(c, Chicken);
        s.Fx.EndPolymorphNow(c);
        Assert.Equal(177, c.SoundSet);
    }

    [DataFact]
    public void EffectDeathSparesAFormWithTwelveBonusHitPointsLeft()
    {
        var s = Make();
        // The drow form's 30 bonus hit points: it survives at 1 hit point, changed back.
        var lucky = s.Spawn();
        s.Polymorph(lucky, Drow);
        HitPoints.Kill(s.World, lucky, GameObject.InvalidId);
        Assert.False(lucky.IsDead);
        Assert.Null(lucky.PolymorphForm);
        Assert.Equal(1, lucky.CurrentHitPoints);
        Assert.Equal(6, lucky.AppearanceType);

        // 18 damage leaves 12 bonus hit points: still spared. 19 leaves 11: it dies.
        var twelve = s.Spawn();
        s.Polymorph(twelve, Drow);
        s.Damage(twelve, 18);
        HitPoints.Kill(s.World, twelve, GameObject.InvalidId);
        Assert.False(twelve.IsDead);
        var eleven = s.Spawn();
        s.Polymorph(eleven, Drow);
        s.Damage(eleven, 19);
        HitPoints.Kill(s.World, eleven, GameObject.InvalidId);
        Assert.True(eleven.IsDead);

        // Temporary hit points from another effect don't count; the bear's 10 aren't enough.
        var bear = s.Spawn();
        s.Polymorph(bear, Bear);
        var temp = new Effect { Type = s.K.TemporaryHitpoints, SubType = s.K.Magical, ScriptId = s.Fx.NextId() };
        temp.Ints[0] = 20;
        s.Fx.Apply(temp, s.K.Temporary, bear, 30);
        HitPoints.Kill(s.World, bear, GameObject.InvalidId);
        Assert.True(bear.IsDead);
    }

    [DataFact]
    public void PolymorphedNpcsCastForFreeAndPlayersCannotCast()
    {
        var s = Make();
        foreach (bool player in new[] { false, true })
        {
            var caster = s.Spawn(player);
            caster.Classes.Clear();
            caster.Abilities[NwRules.AbilityInt] = 18;
            var wizard = new CreatureClass { Class = NwRules.ClassWizard, Level = 10 };
            caster.Classes.Add(wizard);
            wizard.Memorized[1].Add((102, 0, true));
            var plain = new CastSpellAction { Spell = 102, Class = NwRules.ClassWizard, MetaMagic = NwRules.MetaNone, Target = caster.Id };
            Assert.Equal(ActionStatus.InProgress, plain.Step(s.World, caster, .01f));
            plain.Cancel(s.World, caster);
            s.Polymorph(caster, Bear);
            // The unpolymorphed cast above prints its own "casting" line (docs/PARTY.md); only what follows counts.
            s.Output.Messages.Clear();
            var cast = new CastSpellAction { Spell = 102, Class = NwRules.ClassWizard, MetaMagic = NwRules.MetaNone, Target = caster.Id };
            var status = cast.Step(s.World, caster, .01f);
            if (player)
            {
                Assert.Equal(ActionStatus.Failed, status);
                Assert.DoesNotContain(s.Output.Messages, m => m.Contains("cast")); // silently
                continue;
            }
            Assert.Equal(ActionStatus.InProgress, status);
            var last = status;
            for (int i = 0; i < 200 && last == ActionStatus.InProgress; i++) { s.World.Step(.05f); last = cast.Step(s.World, caster, .05f); }
            Assert.Equal(ActionStatus.Done, last);
            Assert.True(wizard.Memorized[1][0].Ready); // the slot isn't spent
        }
    }

    [DataFact]
    public void APolymorphedPlayerCastsTheFormsAbilitiesFromTheRadial()
    {
        var s = Make();
        var pc = s.Spawn(player: true);
        s.Polymorph(pc, 68); // mind flayer: SPELL1 693 Mind Blast, SPELL2 741 Psionic Inertial Barrier
        Assert.Equal(new[] { 693, 741, -1 }, EffectSystem.FormAbilities(s.World, pc));
        var book = JsonSerializer.SerializeToElement(SpellbookRequests.Describe(s.World, pc));
        Assert.Equal(new[] { 693, 741, -1 }, book.GetProperty("formAbilities").EnumerateArray().Select(x => x.GetInt32()));
        Assert.Contains(book.GetProperty("info").EnumerateArray(), i => i.GetProperty("spell").GetInt32() == 741);
        var ability = new CastSpellAction { Spell = 741, Target = pc.Id, FormAbility = true, FromPlayer = true };
        Assert.Equal(ActionStatus.InProgress, ability.Step(s.World, pc, .01f));
        ability.Cancel(s.World, pc);
        // Not one of the form's, or not polymorphed: refused.
        Assert.Equal(ActionStatus.Failed, new CastSpellAction { Spell = 41, Target = pc.Id, FormAbility = true, FromPlayer = true }.Step(s.World, pc, .01f));
        s.Fx.EndPolymorphNow(pc);
        Assert.Equal(ActionStatus.Failed, new CastSpellAction { Spell = 741, Target = pc.Id, FormAbility = true, FromPlayer = true }.Step(s.World, pc, .01f));
    }

    [DataFact]
    public void PolymorphedPlayersCannotLevelUp()
    {
        var s = Make();
        var pc = s.Spawn(player: true);
        pc.Experience = 100000;
        s.Polymorph(pc, Bear);
        var errors = LevelUpRequests.LevelUp(s.World, pc, new LevelChoice { Class = NwRules.ClassFighter });
        Assert.Equal("polymorph", Assert.Single(errors).Code);
        Assert.Equal(1, pc.TotalLevel);
    }

    [DataFact]
    public void TheChangeBackVisualPlaysOnceWhenTheFormEnds()
    {
        var s = Make();
        var c = s.Spawn();
        var e = s.Polymorph(c, Bear);
        int Shown() => s.Output.Notes.Count(n => n.Kind == "vfx" && n.Subject == c.Id && n.Data.GetProperty("vfx").GetInt32() == EffectSystem.ChangeBackVfx);
        Assert.Equal(0, Shown());
        s.Fx.Remove(c, e);
        Assert.Equal(1, Shown());      // at once, while the model still has the form
        Assert.NotNull(c.PolymorphForm);
        Assert.DoesNotContain(e, c.Effects);
        Assert.Equal(10, CreatureRules.TemporaryHitPoints(c)); // the bear's bonus hit points stay until it changes back
        s.Step(1);
        Assert.Null(c.PolymorphForm);
        Assert.Equal(0, CreatureRules.TemporaryHitPoints(c));
        Assert.Equal(1, Shown());

        // Running out: shown as it expires, not again when the creature changes back.
        var d = s.Spawn();
        s.Polymorph(d, Bear, duration: 1);
        s.Step(3);
        Assert.Null(d.PolymorphForm);
        Assert.Equal(1, s.Output.Notes.Count(n => n.Kind == "vfx" && n.Subject == d.Id));
    }

    [DataFact]
    public void CancellingKeepsTheFormListedUntilThePlayerChangesBack()
    {
        var s = Make();
        var pc = s.Spawn(player: true);
        var e = s.Polymorph(pc, Rakshasa, duration: 120);
        s.Fx.CancelPolymorph(pc, e);
        Assert.Contains(e, pc.Effects);                      // still listed, second integer set
        Assert.Equal(1, e.Ints[5]);
        Assert.True(pc.PolymorphForm!.Ending);              // the radial no longer offers it
        Assert.Equal(1, s.Output.Notes.Count(n => n.Kind == "vfx" && n.Subject == pc.Id));
        s.Step(0.3f);
        Assert.Equal(293, pc.AppearanceType);
        Assert.True(pc.Effects.Any(x => x.Hidden && x.Type == s.K.TemporaryHitpoints));
        s.Step(0.5f);
        Assert.Null(pc.PolymorphForm);
        Assert.DoesNotContain(e, pc.Effects);
        Assert.Equal(6, pc.AppearanceType);
    }

    [DataFact]
    public void TheFormsBonusHitPointsShowTheirIconAndPlayersHearAboutTheirArmour()
    {
        var s = Make();
        var pc = s.Spawn(player: true);
        s.Polymorph(pc, Rakshasa);
        var t = s.World.Resources.TwoDA("effecticons");
        int Row(string label) => Enumerable.Range(0, t.RowCount).First(r => t.Get(r, "Label") == label);
        Assert.Equal(new[] { Row("POLYMORPH"), Row("TEMPORARY_HITPOINTS") }, s.Fx.PortraitIcons(pc));
        Assert.Contains(s.Output.Messages, m => m.Contains("Armor/Shield Applies: Skill Armor Check Penalty: 0 Arcane Spell Failure: 0%"));

        var water = s.Spawn(player: true);
        s.Polymorph(water, WaterElemental);
        Assert.Equal(new[] { Row("POLYMORPH") }, s.Fx.PortraitIcons(water));
    }

    [DataFact]
    public void TheSpellbookTellsTheClientItIsPolymorphed()
    {
        var s = Make();
        var pc = s.Spawn(player: true);
        bool Flag() => JsonSerializer.SerializeToElement(SpellbookRequests.Describe(s.World, pc)).GetProperty("polymorphed").GetBoolean();
        Assert.False(Flag());
        s.Polymorph(pc, Bear);
        Assert.True(Flag());
    }

    [DataFact]
    public void ACharacterFileHoldsTheOwnFormsHitPoints()
    {
        var s = Make();
        var pc = s.Spawn(player: true); // 12/12 with Constitution 12
        s.Polymorph(pc, Bear);           // Constitution 19: 15/15 and 10 bonus hit points
        Assert.Equal(15, pc.MaxHitPoints);
        var gff = ObjectSerializer.CreatureGff(s.World, pc, 0, asCharacter: true);
        Assert.Equal(12, gff.GetInt("MaxHitPoints"));
        Assert.Equal(12, gff.GetInt("PregameCurrent"));
        Assert.Equal(6, gff.GetInt("Appearance_Type"));
        Assert.Equal(15, pc.MaxHitPoints); // the live creature is untouched
    }

    [DataFact]
    public void ASaveGameKeepsTheFormAndItsPutAwayEquipment()
    {
        var s = Make();
        var c = s.Spawn();
        var sword = s.World.Items.FromBlueprint("nw_wswls001")!;
        s.World.Register(sword);
        c.Equipment[EquipSlot.RightHand] = sword;
        sword.Possessor = c.Id;
        s.Polymorph(c, Drow, duration: 60);
        var snapshot = WorldSnapshot.Capture(s.World, out var warnings);
        Assert.Empty(warnings);

        var fresh = Make();
        var restored = WorldSnapshot.Apply(fresh.World, snapshot);
        Assert.Empty(restored.Warnings);
        var back = fresh.World.Get<Creature>(c.Id)!;
        Assert.NotNull(back.PolymorphForm);
        Assert.Equal(216, back.AppearanceType);
        Assert.Equal("x2_wdrowls004", back.Equipment[EquipSlot.RightHand].ResRef, ignoreCase: true);
        Assert.Same(back.PolymorphForm!.Bonus, back.Effects.Single(x => x.Hidden && x.Type == fresh.K.TemporaryHitpoints));
        var stored = Assert.Single(back.PolymorphForm!.Stored);
        Assert.Equal(sword.Id, stored.Value.Id);

        // Ending it in the restored world gives the sword back.
        fresh.Fx.EndPolymorphNow(back);
        Assert.Equal(0, CreatureRules.TemporaryHitPoints(back));
        Assert.Equal(sword.Id, back.Equipment[EquipSlot.RightHand].Id);
        Assert.Equal(6, back.AppearanceType);
    }
}
