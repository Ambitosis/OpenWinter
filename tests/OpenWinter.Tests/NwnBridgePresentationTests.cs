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
        // The release ends the held conjure, then casts.
        var casting = bridge.Feed(127715);
        var released = casting.Where(r => r.Type == MessageType.PlayAnimation).Select(PlayAnimationMessage.Read).Where(a => a.ObjectId == C).Select(a => a.Animation);
        Assert.Equal(["pause1", "castarea"], released);

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
        Assert.NotEqual(0, id);
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

    static GameObjUpdateMessages.GameObjUpdateObjList List(params object[] entries) => new() { Entries = [.. entries] };

    static GameObjUpdateUpdateMessages.GameObjUpdateObjListUpdateCreature Anim(uint id, ushort code) =>
        new() { ObjectId = id, AnimSpeed = 1, AnimTypeData = new GameObjUpdateAnimationMessages.AnimTypeData { AnimType = code } };

    static List<MessageReader> Send(Bridge bridge, params object[] entries)
    {
        int before = bridge.Output.Count;
        bridge.Presentation.ObjectList(List(entries), bridge.World.ObjectList);
        return bridge.Output.Skip(before).Select(b => new MessageReader(b)).ToList();
    }

    [Fact]
    public void AHeldLoopEndsWhateverComesNext()
    {
        var bridge = new Bridge();
        Assert.True(Animation(Send(bridge, Anim(A, 40))).Gesture);
        // Walking (a code the mirror plays) ends the forceful talk first; an unknown code does too.
        Assert.Equal("pause1", Animation(Send(bridge, Anim(A, 2))).Animation);
        Send(bridge, Anim(A, 33));
        Assert.Equal("pause1", Animation(Send(bridge, Anim(A, 250))).Animation);
        // Nothing held: an unknown code and standing play nothing.
        Assert.DoesNotContain(Send(bridge, Anim(A, 251), Anim(A, 0)), r => r.Type == MessageType.PlayAnimation);
        // The same looping code again doesn't restart it.
        Send(bridge, Anim(A, 33));
        Assert.DoesNotContain(Send(bridge, Anim(A, 33)), r => r.Type == MessageType.PlayAnimation);
    }

    [Fact]
    public void ALivingCreaturePlaysAScriptsDeadPose()
    {
        var bridge = new Bridge();
        var pose = Animation(Send(bridge, Anim(A, NwnAnimations.DeadFront)));
        Assert.Equal(("deadfnt", true), (pose.Animation, pose.Duration > 1000));
        Assert.Equal("pause1", Animation(Send(bridge, Anim(A, 0))).Animation);
        // One that is dead (combat's flag) lies as combat shows it.
        bridge.World.SetFlags(B, ObjectFlags.Dead, ObjectFlags.Dead);
        Assert.DoesNotContain(Send(bridge, Anim(B, NwnAnimations.DeadBack)), r => r.Type == MessageType.PlayAnimation);
    }

    static GameObjUpdateSharedMessages.VisualEffectUpdate Effect(char change, ushort row, uint? creator) =>
        new() { ChangeType = (byte)change, EffectId = row, CreatorId = creator, BodyNode = creator == null ? null : (byte)0 };

    static List<long> Ids(List<MessageReader> output) =>
        output.Where(r => r.Type == MessageType.Notify).Select(NotifyMessage.Read)
            .Select(n => JsonDocument.Parse(n.Json).RootElement.GetProperty("id").GetInt64()).ToList();

    [Fact]
    public void EachBeamOrEffectApplicationIsItsOwn()
    {
        var bridge = new Bridge();
        GameObjUpdateUpdateMessages.GameObjUpdateObjListUpdateCreature On(params GameObjUpdateSharedMessages.VisualEffectUpdate[] e) =>
            new() { ObjectId = D, Effects = [.. e] };
        var ids = Ids(Send(bridge, On(Effect('A', 73, C), Effect('A', 73, A))));
        Assert.Equal(2, ids.Distinct().Count());
        // The beam from C ends: its own id, not the one from A.
        Assert.Equal(ids[0], Notice(Send(bridge, On(Effect('D', 73, C))), "effect-removed").Json.GetProperty("id").GetInt64());
        // A removal nothing matches sends nothing.
        Assert.DoesNotContain(Send(bridge, On(Effect('D', 73, B))), r => r.Type == MessageType.Notify);
        // The same lasting effect twice on a placeable: two ids, removed latest first.
        var placed = new GameObjUpdateUpdateMessages.ObjListUpdatePlaceable { ObjectId = Chest, Flags = 8, Effects = [Effect('A', 411, null), Effect('A', 411, null)] };
        var both = Ids(Send(bridge, placed).Where(r => r.Type == MessageType.Notify).ToList());
        Assert.Equal(2, both.Distinct().Count());
        var removed = new GameObjUpdateUpdateMessages.ObjListUpdatePlaceable { ObjectId = Chest, Flags = 8, Effects = [Effect('D', 411, null)] };
        Assert.Equal(both[1], Notice(Send(bridge, removed), "effect-removed").Json.GetProperty("id").GetInt64());
    }

    [Fact]
    public void AMovingAreaOfEffectIsCreatedOnceThenMoved()
    {
        var bridge = new Bridge();
        Send(bridge, new GameObjUpdateAddMessages.GameObjUpdateObjListAddAOE { ObjectId = 40, AreaEffectId = 0, TargetOid = A },
            new GameObjUpdateUpdateMessages.ObjListUpdateAOE { ObjectId = 40, Flags = 1, Position = (18, 17, 0) });
        var moved = Send(bridge, new GameObjUpdateUpdateMessages.ObjListUpdateAOE { ObjectId = 40, Flags = 1, Position = (19, 17, 0) })
            .Where(r => r.Type == MessageType.Notify).Select(NotifyMessage.Read).ToList();
        var move = Assert.Single(moved);
        Assert.Equal("aoe-moved", move.Kind);
        Assert.Equal("[19,17,0]", JsonDocument.Parse(move.Json).RootElement.GetProperty("pos").GetRawText());
    }

    [Fact]
    public void ServerFloatsThatArentNumbersDontStopTheList()
    {
        var bridge = new Bridge();
        var transform = new NetTypesMessages.VisualTransformData
        {
            TotalUpdates = [new NetTypesMessages.VisualTransformData.Update
            {
                Scale = new NetTypesMessages.LerpVector { X = new NetTypesMessages.LerpFloat { ValueTo = float.NaN } },
                Rotate = new NetTypesMessages.LerpVector
                {
                    Z = new NetTypesMessages.LerpFloat { ValueTo = 30, ValueFrom = float.PositiveInfinity, LerpType = NetTypesMessages.LerpType.Linear, Duration = float.NaN },
                },
            }],
        };
        var output = Send(bridge,
            new GameObjUpdateUpdateMessages.GameObjUpdateObjListUpdateCreature { ObjectId = D, VisualTransform = transform, HiliteR = float.NaN },
            new GameObjUpdateAddMessages.GameObjUpdateObjListAddAOE { ObjectId = 41, AreaEffectId = 0, TargetOid = GameObjUpdateMessages.InvalidObjectId },
            new GameObjUpdateUpdateMessages.ObjListUpdateAOE { ObjectId = 41, Flags = 1, Position = (float.NaN, 1, 0) });
        var rotate = output.Where(r => r.Type == MessageType.Notify).Select(NotifyMessage.Read).Where(n => n.Kind == "visual-transform")
            .Select(n => JsonDocument.Parse(n.Json).RootElement).Single(j => j.GetProperty("transform").GetInt32() == 23);
        Assert.Equal((23, 30f, 0f), (rotate.GetProperty("transform").GetInt32(), rotate.GetProperty("value").GetSingle(), rotate.GetProperty("duration").GetSingle()));
        int before = bridge.Output.Count;
        Assert.True(bridge.Presentation.ToClient(new AreaMessages.AreaVisualEffect { AnimId = 22, Position = (float.NaN, float.NegativeInfinity, 0) }));
        var place = Notice(bridge.Output.Skip(before).Select(b => new MessageReader(b)).ToList(), "vfx").Json;
        Assert.Equal("[0,0,0]", place.GetProperty("pos").GetRawText());
    }

    [Fact]
    public void ADeletedObjectGetsItsHighlightAgainWhenItReturns()
    {
        var bridge = new Bridge();
        var lit = new GameObjUpdateUpdateMessages.GameObjUpdateObjListUpdateCreature { ObjectId = D, HiliteR = 1, HiliteG = 0, HiliteB = 0 };
        Notice(Send(bridge, lit), "hilite-color");
        Send(bridge, new GameObjUpdateDeleteMessages.ObjListDeleteCreature { ObjectId = D });
        Notice(Send(bridge, lit), "hilite-color");
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
