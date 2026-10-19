using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Client.Gameplay;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.WorldObjects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Music, ambient sound and sound objects as measured on the original (docs/AUDIO.md): the client's music and
/// ambience directors, the sound object rules, and the server's battle music for each player.
/// </summary>
public class AudioTests
{
    static void Run(MusicDirector d, double seconds, double step = 0.05)
    {
        for (double t = 0; t < seconds - 1e-9; t += step) d.Update(step, false);
    }

    static MusicDirector Entered()
    {
        var d = new MusicDirector();
        d.Enter("day", "night", "battle", "stinger", 3.0, false);
        Assert.True(d.TakeRestart());
        Assert.Equal("day", d.Track);
        return d;
    }

    [Fact]
    public void BackgroundStartsOnEntryAndRepeatsAfterTheDelay()
    {
        var d = Entered();
        d.Update(0.05, true);
        Assert.Equal("", d.Track);
        Run(d, 2.9);
        Assert.Equal("", d.Track);
        Run(d, 0.2);
        Assert.Equal("day", d.Track);
        Assert.True(d.TakeRestart());
    }

    [Fact]
    public void ChangingTheTrackFadesTheOldOneThenStartsTheNewFromItsBeginning()
    {
        var d = Entered();
        d.SetTracks("forest", "night", "battle", "stinger");
        d.Update(0.05, false);
        Assert.Equal("day", d.Track);
        Run(d, 0.4);
        Assert.InRange(d.Gain, 0.3f, 0.7f);
        Run(d, 0.5);
        Assert.Equal("forest", d.Track);
        Assert.True(d.TakeRestart());
        Assert.Equal(1f, d.Gain);
    }

    [Fact]
    public void BattleMusicFadesTheBackgroundAndEndsWithItsStingerAndTheDelay()
    {
        var d = Entered();
        d.SetBattle(true);
        Run(d, 0.9);
        Assert.Equal("battle", d.Track);
        Assert.True(d.BattlePlaying);
        d.TakeRestart();
        Run(d, 5);
        d.SetBattle(false);
        Assert.Equal("stinger", d.TakeStinger());
        Assert.Equal("", d.Track);
        // MusicDelay (3 s) plus the fade time, then the background from its start.
        Run(d, 3.7);
        Assert.Equal("", d.Track);
        Run(d, 0.3);
        Assert.Equal("day", d.Track);
        Assert.True(d.TakeRestart());
    }

    [Fact]
    public void StoppingFadesAndPlayingStartsAtOnce()
    {
        var d = Entered();
        d.SetBackground(false);
        Run(d, 1.0);
        Assert.Equal("", d.Track);
        d.SetBackground(true);
        d.Update(0.05, false);
        Assert.Equal("day", d.Track);
        Assert.True(d.TakeRestart());
    }

    [Fact]
    public void NightfallFadesTheDayTrackOverTenSeconds()
    {
        var d = Entered();
        d.SetNight(true);
        Run(d, 5);
        Assert.Equal("day", d.Track);
        Assert.InRange(d.Gain, 0.4f, 0.6f);
        Run(d, 5.2);
        Assert.Equal("night", d.Track);
    }

    [Fact]
    public void AmbientFadesForSixSecondsBeforeAnythingElseStarts()
    {
        var a = new AmbientDirector();
        a.Enter("amb1", 127);
        Assert.True(a.TakeRestart());
        Assert.Equal(1f, a.Gain, 3);
        a.Set("amb2", 100, true);
        for (int i = 0; i < 60; i++) a.Update(0.05);
        Assert.Equal("amb1", a.Playing);
        Assert.InRange(a.Gain, 0.35f, 0.45f); // half way through the fade, at 100/127
        for (int i = 0; i < 62; i++) a.Update(0.05);
        Assert.Equal("amb2", a.Playing);
        Assert.True(a.TakeRestart());
        Assert.Equal(100 / 127f, a.Gain, 3);
        a.Set("amb2", 100, false);
        for (int i = 0; i < 122; i++) a.Update(0.05);
        Assert.Equal("", a.Playing);
    }

    [Fact]
    public void SoundObjectRulesFollowTheMeasurements()
    {
        // Times: 0 the Hours bitmask (bit n = hour n), 1 day, 2 night, 3 always.
        Assert.True(SoundObjectRules.Audible(0, 1u << 21, true, 21));
        Assert.False(SoundObjectRules.Audible(0, 1u << 21, false, 13));
        Assert.True(SoundObjectRules.Audible(1, 0, false, 13));
        Assert.False(SoundObjectRules.Audible(1, 0, true, 21));
        Assert.True(SoundObjectRules.Audible(2, 0, true, 21));
        Assert.True(SoundObjectRules.Audible(3, 0, true, 3));
        // The gap after each sound: Interval plus or minus IntervalVrtn.
        Assert.Equal(0.2, SoundObjectRules.Gap(1.0, 0.8, 0), 6);
        Assert.Equal(1.8, SoundObjectRules.Gap(1.0, 0.8, 1), 6);
        // Volume variation: plus or minus, never above 127; volume is linear.
        Assert.Equal(127, SoundObjectRules.Volume(127, 40, 1));
        Assert.Equal(87, SoundObjectRules.Volume(127, 40, 0));
        Assert.Equal(0.5f, SoundObjectRules.Gain(64), 2);
        // Linear fade from the listener to MaxDistance.
        Assert.Equal(1f, SoundObjectRules.DistanceGain(0, 10));
        Assert.Equal(0.5f, SoundObjectRules.DistanceGain(5, 10));
        Assert.Equal(0f, SoundObjectRules.DistanceGain(10.5f, 10));
        Assert.Equal(new Vector3(1.9f, 0, 0), SoundObjectRules.Listener(Vector3.Zero, new Vector3(10, 0, 0)));
    }

    // ---------------------------------------------------------------- the server's battle music

    sealed class NullConnection : IConnection
    {
        public int Id => 0;
        public bool IsConnected => true;
        public void Send(byte[] message, bool reliable = true) { }
        public bool TryReceive(out byte[] message) { message = Array.Empty<byte>(); return false; }
        public void Disconnect() { }
    }

    sealed class Output : IWorldOutput
    {
        public readonly List<(uint Player, bool On)> Battle = new();
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) { }
        public void FloatingText(Creature over, string text, bool toFaction) { }
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null)
        {
            if (kind == "battle-music" && player != null)
                Battle.Add((player.Id, (bool)data.GetType().GetProperty("on")!.GetValue(data)!));
        }
    }

    sealed class Fx
    {
        public required World W;
        public required Area Area;
        public required Output Out;
        public required Creature Pc;
    }

    static Creature Fighter(World w, Area area, Vector3 pos, int faction)
    {
        var c = w.Creatures.FromTemplate(new GffStruct());
        c.Classes.Add(new CreatureClass { Class = 4, Level = 5 });
        c.Race = 6;
        c.MaxHitPoints = c.CurrentHitPoints = 200;
        c.Faction = faction;
        w.Register(c);
        CreatureRules.EnsureInit(w, c);
        c.Size = 3;
        w.AddToArea(c, area, pos, 0);
        return c;
    }

    static Fx Make()
    {
        var rm = new ResMan(TestData.Paths);
        rm.AddBaseGame();
        var w = new World(rm);
        var output = new Output();
        w.Output = output;
        w.Register(w.Module);
        var area = new Area { ResRef = "music_area", Tag = "music_area" };
        w.Register(area);
        w.Areas.Add(area);
        w.Factions.SetFactionReputation(FactionTable.Hostile, FactionTable.Defender, 0);
        w.Factions.SetFactionReputation(FactionTable.Defender, FactionTable.Hostile, 0);
        var pc = Fighter(w, area, new Vector3(10, 10, 0), FactionTable.Defender);
        pc.Player = new PlayerSession { Connection = new NullConnection(), PlayerName = "pc" };
        return new Fx { W = w, Area = area, Out = output, Pc = pc };
    }

    static void Step(World w, double seconds)
    {
        for (double t = 0; t < seconds; t += 0.1) w.Step(0.1f);
    }

    [DataFact]
    public void AHostileAttackingThePlayerStartsHerBattleMusicUntilAWhileAfterTheFight()
    {
        var f = Make();
        var foe = Fighter(f.W, f.Area, new Vector3(11, 10, 0), FactionTable.Hostile);
        Step(f.W, 0.5);
        Assert.Empty(f.Out.Battle); // a hostile in view is not enough
        f.W.System<CombatSystem>().Attack(foe, f.Pc);
        Step(f.W, 8);
        Assert.Equal([(f.Pc.Id, true)], f.Out.Battle);
        // The fight ends with the foe's death; battle music holds a while, then ends.
        foe.CurrentHitPoints = -20;
        foe.IsDead = true;
        Step(f.W, BattleMusicSystem.HoldSeconds - 1);
        Assert.Single(f.Out.Battle);
        Step(f.W, 2);
        Assert.Equal((f.Pc.Id, false), f.Out.Battle[^1]);
    }

    [DataFact]
    public void APlotPlayerAndThePlayersOwnAttacksGetNoBattleMusic()
    {
        var f = Make();
        var foe = Fighter(f.W, f.Area, new Vector3(11, 10, 0), FactionTable.Hostile);
        f.W.System<CombatSystem>().Attack(f.Pc, foe);
        Step(f.W, 8);
        Assert.Empty(f.Out.Battle);
        f.Pc.Plot = true;
        f.W.System<CombatSystem>().Attack(foe, f.Pc);
        Step(f.W, 8);
        Assert.Empty(f.Out.Battle);
    }
}
