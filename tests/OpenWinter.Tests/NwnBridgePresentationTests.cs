using System.IO.Compression;
using System.Text.Json;
using OpenWinter.Net;
using OpenWinter.Net.Nwn.Bridge;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Session;
using OpenWinter.Net.Nwn.Wire;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Animations, visual effects, projectiles and sounds through the NWN bridge, from frames recorded on nwserver running
/// the bridgefx scene (parity/visual/scenes/bridgefx): bandits A (0xC), B (0xF), C (0x12) and D (0x15), a chest (0x1B)
/// and a sound object (3). Each frame is named by its time in the recording.
/// </summary>
public class NwnBridgePresentationTests
{
    const uint A = 0xC, B = 0xF, C = 0x12, D = 0x15, Chest = 0x1B;

    sealed class Data : INwnBridgeData
    {
        public int TwoDARowCount(string table) => 1000;
        public string TwoDA(string table, int row, string column) => (table, row, column) switch
        {
            ("baseitems", 16, "ModelType") => "3",
            ("baseitems", _, "ModelType") => "2",
            ("classes", _, _) => "****",
            ("visualeffects", 73, "Type_FD") => "B",
            ("visualeffects", 225 or 411, "Type_FD") => "D",
            ("visualeffects", _, "Type_FD") => "F",
            ("vfx_persistent", 0, "SHAPE") => "C",
            ("vfx_persistent", 0, "RADIUS") => "5",
            ("spells", 58, _) => column switch
            {
                "ConjTime" => "1500",
                "ConjAnim" => "hand",
                "ConjHandVisual" => "vco_mehanfire03",
                "ConjSoundVFX" => "sco_mehanfire03",
                "ConjSoundMale" => "vs_chant_evoc_lm",
                "ConjSoundFemale" => "vs_chant_evoc_lf",
                "CastAnim" => "area",
                "CastTime" => "1000",
                "Proj" => "1",
                "ProjModel" => "vpr_fireball",
                "ProjType" => "homing",
                "ProjSpwnPoint" => "hand",
                "ProjSound" => "spr_fireball",
                "ProjOrientation" => "path",
                "HasProjectile" => "1",
                _ => "****",
            },
            _ => "",
        };
        public string Text(uint strRef, byte gender) => $"#{strRef}";
        public void UseContent(IReadOnlyList<string> haks, string customTlk) { }
    }

    /// <summary>The recorded frames, by their time in the recording.</summary>
    static readonly Lazy<List<(long Ms, byte[] Frame)>> Recording = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "nwn-bridge-presentation.jsonl.gz");
        using var reader = new StreamReader(new GZipStream(File.OpenRead(path), CompressionMode.Decompress));
        var frames = new List<(long, byte[])>();
        while (reader.ReadLine() is { } line)
        {
            var row = JsonDocument.Parse(line).RootElement;
            frames.Add((row.GetProperty("ms").GetInt64(), Convert.FromHexString(row.GetProperty("hex").GetString()!)));
        }
        return frames;
    });

    /// <summary>The area, its creatures and placeables, as the bridge's connection dispatches them; then each later frame on demand.</summary>
    sealed class Bridge
    {
        readonly Data _data = new();
        readonly NwnSessionContext _context;
        public readonly NwnWorldMirror World;
        public readonly NwnPresentation Presentation;
        public readonly List<byte[]> Output = [];

        public Bridge()
        {
            _context = new NwnSessionContext(_data.TwoDARowCount, _data.TwoDA);
            World = new NwnWorldMirror(_data, Output.Add);
            Presentation = new NwnPresentation(_data, World, Output.Add);
            foreach (var ms in new long[] { 2249, 2638, 2840, 3028, 3231, 3617 }) Feed(ms);
            Output.Clear();
        }

        /// <summary>Gives the bridge every frame recorded at <paramref name="ms"/>; returns what it sent the client.</summary>
        public List<MessageReader> Feed(long ms)
        {
            int before = Output.Count;
            foreach (var (_, frame) in Recording.Value.Where(f => f.Ms == ms))
            {
                var m = NwnMessageRegistry.Create(frame[1], frame[2], NwnDirection.ServerToClient)!;
                m.Read(new NwnWsReader(frame.AsMemory(3), _context));
                switch (m)
                {
                    case AreaMessages.AreaClientArea area:
                        World.Area(area);
                        Presentation.Area(area);
                        break;
                    case GameObjUpdateMessages.GameObjUpdateObjList list:
                        Presentation.ObjectList(list, World.ObjectList);
                        break;
                    default:
                        Assert.True(Presentation.ToClient(m), $"{m.GetType().Name} at {ms} not taken");
                        break;
                }
            }
            return Output.Skip(before).Select(b => new MessageReader(b)).ToList();
        }
    }

    static PlayAnimationMessage Animation(List<MessageReader> output, uint id = A) =>
        Assert.Single(output.Where(r => r.Type == MessageType.PlayAnimation).Select(PlayAnimationMessage.Read), a => a.ObjectId == id);

    static (string Kind, uint Subject, JsonElement Json) Notice(List<MessageReader> output, string kind)
    {
        var n = output.Where(r => r.Type == MessageType.Notify).Select(NotifyMessage.Read).Single(n => n.Kind == kind);
        return (n.Kind, n.Subject, JsonDocument.Parse(n.Json).RootElement);
    }

    [Fact]
    public void ScriptAnimationsLoopUntilTheServerStandsTheCreatureUp()
    {
        var bridge = new Bridge();
        // ANIMATION_LOOPING_PAUSE2, sent as code 52.
        var pause2 = Animation(bridge.Feed(10697));
        Assert.Equal((A, "pause2", false), (pause2.ObjectId, pause2.Animation, pause2.Gesture));
        Assert.True(pause2.Duration > 1000);
        // Its time up, the server sends the plain pause: back to idle.
        var idle = Animation(bridge.Feed(12652));
        Assert.Equal(("pause1", 0f), (idle.Animation, idle.Duration));
        // ANIMATION_LOOPING_TALK_NORMAL (38) plays on the upper body.
        var talk = Animation(bridge.Feed(28028));
        Assert.Equal(("tlknorm", true), (talk.Animation, talk.Gesture));
        Assert.Equal("pause1", Animation(bridge.Feed(30013)).Animation);
    }

    [Fact]
    public void AKnockdownLaysTheCreatureDownUntilItStandsAgain()
    {
        var bridge = new Bridge();
        var down = bridge.Feed(8334).Where(r => r.Type == MessageType.Update).Select(UpdateMessage.Read).Single(u => u.ObjectId == B);
        Assert.True(down.Flags.HasFlag(ObjectFlags.KnockedDown));
        var up = bridge.Feed(12264).Where(r => r.Type == MessageType.Update).Select(UpdateMessage.Read).Single(u => u.ObjectId == B);
        Assert.False(up.Flags.HasFlag(ObjectFlags.KnockedDown));
    }

    [Fact]
    public void APlaceableAnimationIsItsState()
    {
        var bridge = new Bridge();
        var open = bridge.Feed(10293).Where(r => r.Type == MessageType.Update).Select(UpdateMessage.Read).Single(u => u.ObjectId == Chest);
        Assert.True(open.Flags.HasFlag(ObjectFlags.Open));
    }

    [Fact]
    public void ASpellConjuresCastsAndFlies()
    {
        var bridge = new Bridge();
        var conjuring = bridge.Feed(126147);
        var conjure = Animation(conjuring, C);
        Assert.Equal((C, "conjure1", 1.5f), (conjure.ObjectId, conjure.Animation, conjure.Duration));
        var (_, caster, c) = Notice(conjuring, "spell-conjure");
        Assert.Equal(C, caster);
        Assert.Equal((58, "vco_mehanfire03", "vs_chant_evoc_lm"), (c.GetProperty("spell").GetInt32(), c.GetProperty("handVfx").GetString(), c.GetProperty("voice").GetString()));

        var casting = bridge.Feed(127715);
        Assert.Equal("castarea", Animation(casting, C).Animation);
        var (_, _, cast) = Notice(casting, "spell-cast");
        Assert.Equal("[20,5,0]", cast.GetProperty("pos").GetRawText());

        var (_, from, flight) = Notice(bridge.Feed(128070), "spell-projectile");
        Assert.Equal(C, from);
        Assert.Equal(("vpr_fireball", "homing", 0.435f), (flight.GetProperty("model").GetString(), flight.GetProperty("path").GetString(), flight.GetProperty("travel").GetSingle()));

        var (_, area, burst) = Notice(bridge.Feed(128488), "vfx");
        Assert.Equal(1u, area);
        Assert.Equal((22, "[20,5,0]"), (burst.GetProperty("vfx").GetInt32(), burst.GetProperty("pos").GetRawText()));
        // The release over, the caster stands.
        Assert.Equal("pause1", Animation(bridge.Feed(129096), C).Animation);
    }

    [Fact]
    public void BeamsComeAndGo()
    {
        var bridge = new Bridge();
        var (_, target, beam) = Notice(bridge.Feed(80804), "beam");
        Assert.Equal(D, target);
        Assert.Equal((73, C), (beam.GetProperty("vfx").GetInt32(), beam.GetProperty("from").GetUInt32()));
        long id = beam.GetProperty("id").GetInt64();
        Assert.Equal(NwnPresentation.EffectId(D, 73), id);
        Assert.Equal(id, Notice(bridge.Feed(83777), "effect-removed").Json.GetProperty("id").GetInt64());
        // An instant beam back the other way.
        var (_, back, instant) = Notice(bridge.Feed(85735), "beam");
        Assert.Equal((C, D, 0L), (back, instant.GetProperty("from").GetUInt32(), instant.GetProperty("id").GetInt64()));
    }

    [Fact]
    public void AnAreaOfEffectAppearsAndGoes()
    {
        var bridge = new Bridge();
        var (_, _, aoe) = Notice(bridge.Feed(88771), "aoe-created");
        Assert.Equal((32u, 0, "C", 5f, "[20,6,0]"),
            (aoe.GetProperty("id").GetUInt32(), aoe.GetProperty("aoe").GetInt32(), aoe.GetProperty("shape").GetString(), aoe.GetProperty("radius").GetSingle(), aoe.GetProperty("pos").GetRawText()));
        Assert.Equal(32u, Notice(bridge.Feed(96682), "aoe-removed").Json.GetProperty("id").GetUInt32());
    }

    [Fact]
    public void TransformsAndHighlights()
    {
        var bridge = new Bridge();
        var (_, scaled, vt) = Notice(bridge.Feed(98640), "visual-transform");
        Assert.Equal((D, 10, 1.6f), (scaled, vt.GetProperty("transform").GetInt32(), vt.GetProperty("value").GetSingle()));
        var (_, lit, hilite) = Notice(bridge.Feed(107357), "hilite-color");
        Assert.Equal((D, 0xFF0000), (lit, hilite.GetProperty("color").GetInt32()));
    }

    [Fact]
    public void SoundsObjectsAndMusic()
    {
        var bridge = new Bridge();
        var (_, played, sound) = Notice(bridge.Feed(169081), "play-sound");
        Assert.Equal((A, "as_cv_bell1"), (played, sound.GetProperty("resref").GetString()));
        var (_, soundObject, play) = Notice(bridge.Feed(173019), "sound-object");
        Assert.Equal((3u, "play"), (soundObject, play.GetProperty("action").GetString()));
        Assert.Equal(10, Notice(bridge.Feed(177945), "area-audio").Json.GetProperty("musicDay").GetInt32());
    }

    [Fact]
    public void TheAreasSoundObjectsAreSpawned()
    {
        var data = new Data();
        var output = new List<byte[]>();
        var world = new NwnWorldMirror(data, output.Add);
        var presentation = new NwnPresentation(data, world, output.Add);
        var frame = Recording.Value.Single(f => f.Ms == 2249).Frame;
        var area = new AreaMessages.AreaClientArea();
        area.Read(new NwnWsReader(frame.AsMemory(3), new NwnSessionContext(data.TwoDARowCount, data.TwoDA)));
        presentation.Area(area);
        var spawn = SpawnMessage.Read(new MessageReader(Assert.Single(output)));
        Assert.Equal((3u, ObjectKind.Sound), (spawn.ObjectId, spawn.Kind));
        var extra = JsonDocument.Parse(spawn.Extra).RootElement;
        Assert.Equal(("as_cv_bell2", true, 50f), (extra.GetProperty("sounds")[0].GetString(), extra.GetProperty("positional").GetBoolean(), extra.GetProperty("maxDistance").GetSingle()));
    }

    [Fact]
    public void AnEmoteIsTheAnimationInput()
    {
        var bridge = new Bridge();
        // Worship loops for seven seconds; the server echoed it as code 33 (bridgefx emote run).
        var emote = Assert.IsType<InputMessages.InputPlayAnimation>(bridge.Presentation.Emote("worship"));
        Assert.Equal((33, 7f, GameObjUpdateMessages.InvalidObjectId), ((int)emote.Animation, emote.Duration, emote.Target));
        Assert.Null(bridge.Presentation.Emote("no-such-emote"));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 52)]
    [InlineData(8, 38)]
    [InlineData(16, 6)]
    [InlineData(17, 8)]
    [InlineData(23, 101)]
    [InlineData(32, 110)]
    [InlineData(43, 121)]
    [InlineData(79, 157)]
    [InlineData(105, 35)]
    [InlineData(116, 23)]
    public void ScriptAnimationsHaveTheCodesRecorded(int animation, int code) =>
        Assert.Equal((ushort)code, NwnAnimations.FromScript(animation));
}
