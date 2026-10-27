using System.Numerics;
using System.Text.Json.Nodes;
using OpenWinter.Aurora.Formats;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAddMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAppearanceMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateDeleteMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateUpdateMessages;
using AnimTypeData = OpenWinter.Net.Nwn.Messages.GameObjUpdateAnimationMessages.AnimTypeData;
using VisualEffectUpdate = OpenWinter.Net.Nwn.Messages.GameObjUpdateSharedMessages.VisualEffectUpdate;
using VisualTransformData = OpenWinter.Net.Nwn.Messages.NetTypesMessages.VisualTransformData;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// How the world looks and sounds, as OpenWinter's own server presents it: creature animations become PlayAnimation
/// (script animations, emotes, conjuring and casting; the spell's conjure and cast effects are NwnAbilities'), spell
/// projectiles "spell-projectile", knockdowns a condition flag; visual effects at places and on objects "vfx", beams "beam", areas of
/// effect "aoe-created" and "aoe-moved"; visual transforms and highlights "visual-transform" and "hilite-color";
/// PlaySound, voice chat, sound objects, music and ambience "play-sound", "voice-chat", "sound-object" and
/// "area-audio"; script shader uniforms "shader-uniform". The client's emotes go back as the animation input nwmain sends.
/// </summary>
public sealed class NwnPresentation(INwnBridgeData data, NwnWorldMirror world, Action<byte[]> emit)
{
    const uint InvalidObject = GameObjUpdateMessages.InvalidObjectId;
    const uint NoSpell = uint.MaxValue;
    /// <summary>The plain idle: asking for it ends a looping animation, as OpenWinter's server does.</summary>
    const string Idle = "pause1";
    /// <summary>A looping animation's length: until the server's next animation replaces it.</summary>
    const float UntilReplaced = 1e6f;
    /// <summary>A knockdown's animation code: the creature lies until another code arrives.</summary>
    const ushort KnockedDown = 5;

    sealed class Creature
    {
        public ushort Anim = NwnAnimations.Ready;
        /// <summary>The animation shown loops (or is a cast) and has to be ended when the code changes.</summary>
        public bool Holding;
        /// <summary>The latest animation of a creature not shown yet, played once it is.</summary>
        public (AnimTypeData Anim, float Speed)? Pending;
        public int SoundSet = -1;
        public byte Gender;
    }

    /// <summary>What the bridge has shown of one object: highlight, transform channels and lasting effects.</summary>
    sealed class Shown
    {
        public int? Hilite;
        public readonly Dictionary<(int Scope, int Transform), (float To, int Lerp, float Duration)> Transforms = [];
        /// <summary>Lasting effects by row and creator, the ids given to each application (latest last).</summary>
        public readonly Dictionary<(ushort Row, uint Creator), Stack<long>> Effects = [];
    }

    readonly Dictionary<uint, Creature> _creatures = [];
    readonly HashSet<uint> _pending = [];
    readonly Dictionary<uint, Shown> _shown = [];
    readonly Dictionary<uint, (int Row, uint Host, Vector3? At)> _aoes = [];
    readonly HashSet<uint> _sounds = [];
    readonly Dictionary<string, SoundSet?> _soundSets = [];
    long _nextEffect;

    // -- client to server ---------------------------------------------------

    /// <summary>
    /// An emote from the radial or quickbar (OpenWinter's emote key): the animation input nwmain sends, with the
    /// looping time the emote plays for; null for a key that isn't an emote.
    /// </summary>
    public NwnMessage? Emote(string key)
    {
        if (OpenWinter.Content.Emotes.Find(key) is not { } emote || NwnAnimations.FromScript(emote.Animation) is not { } code) return null;
        return new InputMessages.InputPlayAnimation
        {
            Animation = code, Target = InvalidObject, Position = (0, 0, 0), Duration = emote.Seconds,
        };
    }

    // -- server to client ---------------------------------------------------

    /// <summary>Visual effects, projectiles, sounds, voices, sound objects and the area's music; false for anything else.</summary>
    public bool ToClient(NwnMessage message)
    {
        switch (message)
        {
            case AreaMessages.AreaVisualEffect v:
                Notify("vfx", world.AreaId, new JsonObject
                {
                    ["vfx"] = (int)v.AnimId, ["miss"] = false, ["scale"] = Scale(v.Vt), ["pos"] = Vec(v.Position), ["facing"] = 0f,
                    ["duration"] = 0f, ["id"] = 0L,
                });
                return true;
            case GameObjUpdateMessages.GameObjUpdateVisEffect v:
                return VisualEffect(v);
            case SafeProjectileMessages.SafeProjectileSpawn p:
                Projectile(p);
                return true;
            case ChatMessages.ChatPlaySound s:
                PlaySound(s.SpeakerId, s.Sound);
                return true;
            case QuickChatMessages.QuickChat q:
                Voice(q.Speaker, q.SoundSetSoundId, caption: true);
                return true;
            case VoiceChatMessages.VoiceChatPlay v:
                Voice(v.ObjectId, v.VoicesetId, caption: false);
                return true;
            case SoundMessages.SoundObjectCreate c:
                SpawnSound(c.SoundObject, c.Sound.Position, c.Sound.Active, c.Sound.Positional, c.Sound.Looping, c.Sound.Continuous,
                    c.Sound.Random, c.Sound.RandomPosition, c.Sound.RandomXRange, c.Sound.RandomYRange, c.Sound.Interval,
                    c.Sound.IntervalVariance, c.Sound.PitchVariation, c.Sound.Volume, c.Sound.VolumeVariation, c.Sound.MinDistance,
                    c.Sound.MaxDistance, c.Sound.TimeOfDay, c.Sound.Hours, c.Sound.Sounds);
                return true;
            case SoundMessages.SoundObjectDestroy d:
                if (_sounds.Remove(d.SoundObject)) emit(new DespawnMessage { ObjectId = d.SoundObject }.Write());
                return true;
            case SoundMessages.SoundObjectPlay p:
                Notify("sound-object", p.SoundObject, new JsonObject { ["action"] = "play" });
                return true;
            case SoundMessages.SoundObjectStop s:
                Notify("sound-object", s.SoundObject, new JsonObject { ["action"] = "stop" });
                return true;
            case SoundMessages.SoundObjectChangeVolume v:
                Notify("sound-object", v.SoundObject, new JsonObject { ["action"] = "volume", ["volume"] = v.Volume });
                return true;
            case SoundMessages.SoundObjectChangePosition p:
                Notify("sound-object", p.SoundObject, new JsonObject { ["action"] = "position", ["position"] = Vec(p.Position) });
                return true;
            case AmbientMessages.AmbientMusicChange m:
                Audio(m.Day ? "musicDay" : "musicNight", m.Track);
                return true;
            case AmbientMessages.AmbientMusicSetDelay m:
                Audio("musicDelay", m.Delay);
                return true;
            case AmbientMessages.AmbientMusicPlay m:
                Audio("musicPlaying", m.Play);
                return true;
            case AmbientMessages.AmbientBattleMusicChange m:
                Audio("musicBattle", m.Track);
                return true;
            case AmbientMessages.AmbientSoundChange m:
                Audio(m.Day ? "ambientDay" : "ambientNight", m.Track);
                return true;
            case AmbientMessages.AmbientSoundVolume m:
                Audio(m.Day ? "ambientDayVolume" : "ambientNightVolume", m.Volume);
                return true;
            case AmbientMessages.AmbientSoundPlay m:
                Audio("ambientPlaying", m.Play);
                return true;
            // SetShaderUniform*: the player's script uniform banks, as OpenWinter's server sends them.
            case SetShaderUniformMessages.SetShaderUniformFloat u:
                ShaderUniform(u.Index, "float", [u.Value]);
                return true;
            case SetShaderUniformMessages.SetShaderUniformInt u:
                ShaderUniform(u.Index, "int", [u.Value], u.Value);
                return true;
            case SetShaderUniformMessages.SetShaderUniformVec u:
                ShaderUniform(u.Index, "vec4", [u.X, u.Y, u.Z, u.W]);
                return true;
        }
        return false;
    }

    /// <summary>A new area: what was known of the last one is dropped, and the area's sound objects are placed.</summary>
    public void Area(AreaMessages.AreaClientArea area)
    {
        foreach (var id in _creatures.Keys.Where(id => id != world.Controlled).ToList()) _creatures.Remove(id);
        _pending.Clear();
        _shown.Clear();
        _aoes.Clear();
        _sounds.Clear();
        foreach (var s in area.Sounds)
            SpawnSound(s.ObjectId, s.Position, s.IsActive, s.Positional, s.Looping, s.Continuous, s.Random, s.RandomPosition,
                s.RandomXRange, s.RandomYRange, s.Interval, s.IntervalVariance, s.PitchVariation, s.Volume, s.VolumeVariation,
                s.MinDistance, s.MaxDistance, s.TimeOfDay, s.Hours, s.SoundList);
    }

    /// <summary>
    /// The object list: knockdowns are given to the world mirror before it describes the list (<paramref name="mirror"/>),
    /// so they go out with it; animations, effects, transforms and highlights follow.
    /// </summary>
    public void ObjectList(GameObjUpdateMessages.GameObjUpdateObjList list, Action<GameObjUpdateMessages.GameObjUpdateObjList> mirror)
    {
        foreach (var entry in list.Entries)
        {
            (uint id, ushort code) = entry switch
            {
                GameObjUpdateObjListAddCreature add => (add.ObjectId, add.Anim),
                GameObjUpdateObjListUpdateCreature { AnimTypeData: { } anim } u => (u.ObjectId, anim.AnimType),
                _ => (0u, (ushort)0),
            };
            if (id == 0) continue;
            bool down = code == KnockedDown;
            if (down || (_creatures.TryGetValue(id, out var was) && was.Anim == KnockedDown))
                world.SetFlags(id, down ? ObjectFlags.KnockedDown : 0, ObjectFlags.KnockedDown);
        }
        mirror(list);
        foreach (var entry in list.Entries)
        {
            switch (entry)
            {
                case GameObjUpdateObjListAddCreature add:
                {
                    var c = Of(add.ObjectId);
                    (c.Anim, c.Holding, c.Pending) = (NwnAnimations.Ready, false, null);
                    // A creature that arrives sitting, kneeling or lying plays that once it is shown.
                    if (add.Anim is not (NwnAnimations.Pause or NwnAnimations.Ready))
                        Animate(add.ObjectId, new AnimTypeData { AnimType = add.Anim }, 1);
                    break;
                }
                case GameObjUpdateObjListAppearanceCreature a:
                {
                    var c = Of(a.ObjectId);
                    if ((a.Flags & UpdateCreatureAppearanceFlags.Gender) != 0) c.Gender = a.Gender;
                    if ((a.Flags & UpdateCreatureAppearanceFlags.Soundset) != 0 && a.SoundsetInfo is { } sounds) c.SoundSet = sounds.Soundset;
                    break;
                }
                case GameObjUpdateObjListUpdateCreature u:
                    if (u.AnimTypeData is { } anim) Animate(u.ObjectId, anim, u.AnimSpeed ?? 1);
                    if (u.Effects is { } effects) Effects(u.ObjectId, effects, creature: true);
                    if (u.VisualTransform is { } vt) Transform(u.ObjectId, vt);
                    if (u.HiliteR is { } r) Hilite(u.ObjectId, r, u.HiliteG ?? -1, u.HiliteB ?? -1);
                    break;
                case GameObjUpdateObjListAddAOE aoe:
                    _aoes[aoe.ObjectId] = (aoe.AreaEffectId, aoe.TargetOid, null);
                    break;
                case ObjListUpdateAOE { Position: { } at } aoe when _aoes.ContainsKey(aoe.ObjectId):
                    AreaOfEffect(aoe.ObjectId, V(at), aoe.Orientation);
                    break;
                case GameObjUpdateObjListUpdateGeneric g:
                    if (g.Effects is { } placed) Effects(g.ObjectId, placed, creature: false);
                    if (g.VisualTransform is { } gvt) Transform(g.ObjectId, gvt);
                    if (g.HiliteR is { } gr) Hilite(g.ObjectId, gr, g.HiliteG ?? -1, g.HiliteB ?? -1);
                    break;
                case ObjListDeleteAOE d:
                    if (_aoes.Remove(d.ObjectId)) Notify("aoe-removed", world.AreaId, new JsonObject { ["id"] = d.ObjectId });
                    Forget(d.ObjectId);
                    break;
                case ObjListDeleteGeneric or ObjListDeleteCreature or ObjListDeleteDoor or ObjListDeletePlaceable or ObjListDeleteItem
                    or ObjListDeleteTrigger:
                    Forget(DeletedId(entry));
                    break;
            }
        }
        // Animations of creatures the mirror has shown by now.
        foreach (var id in _pending.ToList())
        {
            if (!world.TryPose(id, out _, out _, out _, out _)) continue;
            _pending.Remove(id);
            if (_creatures.TryGetValue(id, out var c) && c.Pending is var (anim, speed))
            {
                c.Pending = null;
                Animate(id, anim, speed);
            }
        }
    }

    static uint DeletedId(object entry) => entry switch
    {
        ObjListDeleteGeneric e => e.ObjectId,
        ObjListDeleteCreature e => e.ObjectId,
        ObjListDeleteDoor e => e.ObjectId,
        ObjListDeletePlaceable e => e.ObjectId,
        ObjListDeleteItem e => e.ObjectId,
        ObjListDeleteTrigger e => e.ObjectId,
        _ => 0,
    };

    /// <summary>A deleted object: everything shown of it starts again if it comes back.</summary>
    void Forget(uint id)
    {
        if (id != world.Controlled) _creatures.Remove(id);
        _pending.Remove(id);
        _shown.Remove(id);
    }

    // -- animations ---------------------------------------------------------

    void Animate(uint id, AnimTypeData anim, float speed)
    {
        var c = Of(id);
        if (!world.TryPose(id, out _, out _, out _, out _))
        {
            // Not shown yet: the latest animation plays once it is.
            c.Pending = (anim, speed);
            c.Anim = anim.AnimType;
            _pending.Add(id);
            return;
        }
        ushort code = anim.AnimType;
        if (c.Holding && code == c.Anim && !IsSpell(code)) return;
        bool held = c.Holding;
        c.Anim = code;
        c.Holding = false;
        // A looping animation (a gesture included) lasts until replaced: whatever comes next ends it first.
        if (held) Play(id, Idle, speed, 0, false);
        switch (code)
        {
            case NwnAnimations.Pause or NwnAnimations.Ready or KnockedDown:
                // Standing, or lying knocked down (a condition flag, set before the mirror described the list).
                return;
            case NwnAnimations.DeadFront or NwnAnimations.DeadBack:
                // The dead lie as combat shows them; a living creature plays the script's dead pose.
                if ((world.FlagsOf(id) & (ObjectFlags.Dead | ObjectFlags.Dying)) != 0) return;
                c.Holding = true;
                Play(id, code == NwnAnimations.DeadFront ? "deadfnt" : "deadbck", speed, UntilReplaced, false);
                return;
            case NwnAnimations.Conjure1 or NwnAnimations.Conjure2:
                Conjure(id, c, code, anim.Spell, speed);
                return;
        }
        if (NwnAnimations.Casts.Contains(code))
        {
            Cast(id, c, code, anim, speed);
            return;
        }
        // Walking, attacks and hit reactions are the mirror's and combat's; anything else unknown stays still.
        if (NwnAnimations.For(code) is not { } clip) return;
        c.Holding = clip.Looping;
        Play(id, clip.Name, speed, clip.Looping ? UntilReplaced : 0, clip.Gesture);
    }

    static bool IsSpell(ushort code) => code is NwnAnimations.Conjure1 or NwnAnimations.Conjure2 || NwnAnimations.Casts.Contains(code);

    void Play(uint id, string clip, float speed, float duration, bool gesture) =>
        emit(new PlayAnimationMessage { ObjectId = id, Animation = clip, Speed = speed > 0 && float.IsFinite(speed) ? speed : 1, Duration = duration, Gesture = gesture }.Write());

    /// <summary>Conjuring: the conjure animation for the spell (spells.2da ConjAnim), for its conjure time.</summary>
    void Conjure(uint id, Creature c, ushort code, GameObjUpdateAnimationMessages.CastAnimData? cast, float speed)
    {
        c.Holding = true;
        int spell = cast is { SpellId: not NoSpell } s ? (int)s.SpellId : -1;
        float seconds = spell >= 0 ? Int(Spell(spell, "ConjTime")) / 1000f : 0;
        Play(id, NwnAnimations.ConjureClip(spell >= 0 ? Spell(spell, "ConjAnim") : "", code), speed, seconds > 0 ? seconds : UntilReplaced, false);
    }

    /// <summary>A spell's release: the cast animation for the spell (spells.2da CastAnim) until the server's next code.</summary>
    void Cast(uint id, Creature c, ushort code, AnimTypeData anim, float speed)
    {
        c.Holding = true;
        string style = anim.Spell is { SpellId: not NoSpell } s ? Spell((int)s.SpellId, "CastAnim") : "";
        Play(id, NwnAnimations.CastClip(style, code), speed, UntilReplaced, false);
    }

    /// <summary>A spell's projectile, sent as it flies: the spells.2da model, path and sound, with its flight time.</summary>
    void Projectile(SafeProjectileMessages.SafeProjectileSpawn p)
    {
        (int spell, int path) = p.Projectile.Value switch
        {
            SafeProjectileMessages.ProjectileSpell s => ((int)s.SpellId, 0),
            SafeProjectileMessages.ProjectileSpellCustomPath s => ((int)s.SpellId, (int)s.ProjectilePath),
            _ => (-1, 0),
        };
        // Arrows and thrown weapons are drawn with the attack that shoots them.
        if (spell < 0 || Int(Spell(spell, "Proj")) == 0 || Spell(spell, "ProjModel").Length == 0) return;
        var from = V(p.OriginLocation);
        var to = V(p.TargetLocation);
        string kind = path switch
        {
            1 => "homing",
            2 => "ballistic",
            3 => "highballistic",
            4 => "accelerating",
            _ => Spell(spell, "ProjType") is { Length: > 0 } t ? t.ToLowerInvariant() : "default",
        };
        Notify("spell-projectile", p.Originator, new JsonObject
        {
            ["spell"] = spell, ["presentation"] = 0, ["target"] = p.Target, ["from"] = Vec(from), ["to"] = Vec(to),
            ["model"] = Spell(spell, "ProjModel"), ["path"] = kind, ["showModel"] = Int(Spell(spell, "HasProjectile")) != 0,
            ["spawnPoint"] = Spell(spell, "ProjSpwnPoint"), ["sound"] = Spell(spell, "ProjSound"),
            ["orientation"] = Spell(spell, "ProjOrientation"), ["delay"] = 0f,
            ["travel"] = p.DeltaTime > 1 ? p.DeltaTime / 1000f : FlightTime(Vector3.Distance(from, to), kind),
        });
    }

    /// <summary>
    /// A spell projectile's flight when the server leaves it to the client (OpenWinter's own timing, measured by the
    /// projectile_paths suites): distance over 3 ln(d) + 2 metres a second, doubled for homing, x1.5 accelerating,
    /// x0.8 bounce; high ballistic and linked a flat 2 s, spiral 2.5 s more.
    /// </summary>
    static float FlightTime(float distance, string path)
    {
        const float Immediate = 0.001f;
        if (path == "highballistic" || path == "linked") return 2f;
        if (!float.IsFinite(distance) || distance <= 0) return Immediate;
        float speed = (3f * MathF.Log(distance) + 2f) * path switch { "homing" => 2f, "accelerating" => 1.5f, "bounce" => 0.8f, _ => 1f };
        if (!(speed > 0)) return Immediate;
        float ms = MathF.Floor(distance / speed * 1000f);
        if (!float.IsFinite(ms)) return Immediate;
        return ms / 1000f + (path == "spiral" ? 2.5f : 0);
    }

    // -- visual effects -----------------------------------------------------

    /// <summary>A beam (visualeffects.2da Type_FD "B"): drawn between two objects, sent as "beam".</summary>
    public static bool IsBeam(INwnBridgeData data, int row) => data.TwoDA("visualeffects", row, "Type_FD").Equals("B", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A visual effect the server plays once: a beam between two objects, or a projectile-style effect (Magic
    /// Missile's) flying from its source. Impacts at one object or place are combat's (fire-and-forget).
    /// </summary>
    bool VisualEffect(GameObjUpdateMessages.GameObjUpdateVisEffect v)
    {
        string type = data.TwoDA("visualeffects", v.VisualEffectId, "Type_FD").ToUpperInvariant();
        if (type == "B" && world.KindOf(v.TargetId) != null)
        {
            Notify("beam", v.TargetId, new JsonObject
            {
                ["vfx"] = (int)v.VisualEffectId, ["from"] = v.SourceId, ["bodyPart"] = (int)v.SourceNode, ["miss"] = false,
                ["scale"] = Scale(v.Vt), ["translate"] = Vec(Vector3.Zero), ["rotate"] = Vec(Vector3.Zero), ["duration"] = 0f, ["id"] = 0L,
            });
            return true;
        }
        if (type == "P")
        {
            var json = new JsonObject { ["vfx"] = (int)v.VisualEffectId, ["creator"] = v.SourceId, ["miss"] = false, ["scale"] = Scale(v.Vt), ["duration"] = 0f, ["id"] = 0L };
            if (world.KindOf(v.TargetId) is null)
            {
                json["pos"] = Vec(v.TargetLocation);
                Notify("vfx", world.AreaId, json);
            }
            else Notify("vfx", v.TargetId, json);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Lasting visual effects coming ('A') and going ('D'): beams on any object, the rest on placeables and doors
    /// (a creature's are combat's). Each application gets its own id; a removal ends the latest one of that effect
    /// from that creator.
    /// </summary>
    void Effects(uint id, List<VisualEffectUpdate> effects, bool creature)
    {
        foreach (var e in effects)
        {
            bool beam = IsBeam(data, e.EffectId);
            if (creature && !beam) continue;
            var key = (e.EffectId, e.CreatorId ?? InvalidObject);
            var shown = ShownOf(id);
            if (e.ChangeType == 'D')
            {
                if (!shown.Effects.TryGetValue(key, out var applied) || !applied.TryPop(out var ended)) continue;
                if (applied.Count == 0) shown.Effects.Remove(key);
                Notify("effect-removed", id, new JsonObject { ["id"] = ended, ["type"] = 0, ["linkId"] = "", ["icon"] = 0 });
                continue;
            }
            if (e.ChangeType != 'A') continue;
            long effect = (2L << 48) | ++_nextEffect;
            if (!shown.Effects.TryGetValue(key, out var stack)) shown.Effects[key] = stack = new();
            stack.Push(effect);
            if (beam)
                Notify("beam", id, new JsonObject
                {
                    ["vfx"] = (int)e.EffectId, ["from"] = e.CreatorId ?? InvalidObject, ["bodyPart"] = (int)(e.BodyNode ?? 0), ["miss"] = false,
                    ["scale"] = Scale(e.Vt), ["translate"] = Vec(Vector3.Zero), ["rotate"] = Vec(Vector3.Zero), ["duration"] = -1f, ["id"] = effect,
                });
            else
                Notify("vfx", id, new JsonObject
                {
                    ["vfx"] = (int)e.EffectId, ["miss"] = false, ["scale"] = Scale(e.Vt), ["translate"] = Vec(Vector3.Zero),
                    ["rotate"] = Vec(Vector3.Zero), ["duration"] = -1f, ["id"] = effect,
                });
        }
    }

    /// <summary>
    /// An area of effect: vfx_persistent.2da's shape and size where the server first places it; later positions (one
    /// that moves with its creature) move it.
    /// </summary>
    void AreaOfEffect(uint id, Vector3 at, OrientationUpdate? facing)
    {
        var (row, host, placed) = _aoes[id];
        if (!Finite(at)) return;
        _aoes[id] = (row, host, at);
        if (placed is { } before)
        {
            if (before != at) Notify("aoe-moved", world.AreaId, new JsonObject { ["id"] = id, ["pos"] = Vec(at) });
            return;
        }
        string Cell(string column) => data.TwoDA("vfx_persistent", row, column) is var v && v != "****" ? v : "";
        float F(string column) => float.TryParse(Cell(column), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) && float.IsFinite(f) ? f : 0;
        // A bearing counts from +Y (as the mirror reads facings); the client's facing from +X.
        float degrees = facing is { Full: true } o ? MathF.Atan2(o.Oy ?? 0, o.Ox ?? 1) * 180f / MathF.PI
            : facing?.Bearing is { } bearing ? (bearing + 90f) % 360f : 0;
        Notify("aoe-created", world.AreaId, new JsonObject
        {
            ["id"] = id, ["aoe"] = row, ["shape"] = Cell("SHAPE").ToUpperInvariant() == "R" ? "R" : "C", ["radius"] = F("RADIUS"),
            ["width"] = F("WIDTH"), ["length"] = F("LENGTH"), ["pos"] = Vec(at), ["facing"] = Finite(degrees),
            ["attachedTo"] = host,
        });
    }

    // -- transforms and highlights ------------------------------------------

    /// <summary>
    /// SetObjectVisualTransform: each channel (scale, rotation, translation, animation speed) that changed, as
    /// "visual-transform"; a scope back to identity as "visual-transform-clear".
    /// </summary>
    void Transform(uint id, VisualTransformData vt)
    {
        var shown = ShownOf(id);
        foreach (var u in vt.TotalUpdates)
        {
            if (u.Identity)
            {
                var cleared = shown.Transforms.Keys.Where(k => k.Scope == u.Scope).ToList();
                foreach (var k in cleared) shown.Transforms.Remove(k);
                if (cleared.Count > 0) Notify("visual-transform-clear", id, new JsonObject { ["scope"] = u.Scope });
                continue;
            }
            Channel(id, shown, u.Scope, 10, u.Scale?.X, 1);
            Channel(id, shown, u.Scope, 21, u.Rotate?.X, 0);
            Channel(id, shown, u.Scope, 22, u.Rotate?.Y, 0);
            Channel(id, shown, u.Scope, 23, u.Rotate?.Z, 0);
            Channel(id, shown, u.Scope, 31, u.Translate?.X, 0);
            Channel(id, shown, u.Scope, 32, u.Translate?.Y, 0);
            Channel(id, shown, u.Scope, 33, u.Translate?.Z, 0);
            Channel(id, shown, u.Scope, 40, u.AnimationSpeed, 1);
        }
    }

    void Channel(uint id, Shown shown, int scope, int transform, NetTypesMessages.LerpFloat? f, float identity)
    {
        if (f == null || !float.IsFinite(f.ValueTo)) return;
        var key = (scope, transform);
        float duration = Finite(f.Duration);
        var now = (f.ValueTo, (int)f.LerpType, duration);
        if (shown.Transforms.TryGetValue(key, out var known) ? known == now : f.ValueTo == identity && f.LerpType == NetTypesMessages.LerpType.None) return;
        shown.Transforms[key] = now;
        Notify("visual-transform", id, new JsonObject
        {
            ["transform"] = transform, ["scope"] = scope, ["from"] = f.LerpType == NetTypesMessages.LerpType.None ? f.ValueTo : Finite(f.ValueFrom, f.ValueTo),
            ["value"] = f.ValueTo, ["lerp"] = (int)f.LerpType, ["duration"] = duration, ["pauseWithGame"] = f.TimerType == NetTypesMessages.LerpTimerType.WorldTime,
            ["behavior"] = (f.Flags & NetTypesMessages.LerpFlags.Bounce) != 0 ? 1 : 0, ["repeats"] = f.RepeatsRemaining,
            ["elapsed"] = Finite(f.Progress * duration),
        });
    }

    /// <summary>SetObjectHiliteColor: a colour (0xRRGGBB) for the object's highlight, or -1 for its own.</summary>
    void Hilite(uint id, float r, float g, float b)
    {
        int color = !(r >= 0) ? -1 : (Byte(r) << 16) | (Byte(g) << 8) | Byte(b);
        var shown = ShownOf(id);
        if (shown.Hilite is { } known ? known == color : color == -1) return;
        shown.Hilite = color;
        Notify("hilite-color", id, new JsonObject { ["color"] = color });
    }

    static int Byte(float channel) => float.IsFinite(channel) ? Math.Clamp((int)MathF.Round(channel * 255), 0, 255) : 0;

    // -- sounds -------------------------------------------------------------

    /// <summary>PlaySound at its object; from one the client doesn't know, where the player stands.</summary>
    void PlaySound(uint source, string resref)
    {
        if (resref.Length == 0) return;
        if (!world.TryPose(source, out var at, out _, out _, out _)) world.TryPose(world.Controlled, out at, out _, out _, out _);
        Notify("play-sound", source, new JsonObject { ["resref"] = resref, ["position"] = Vec(at), ["strref"] = -1 });
    }

    /// <summary>A creature's voice-set line (soundset.2da, its .ssf): the sound, with its words in chat for a voice chat.</summary>
    void Voice(uint speaker, int slot, bool caption)
    {
        if (!_creatures.TryGetValue(speaker, out var c) || c.SoundSet < 0) return;
        var entry = SoundSetOf(c.SoundSet)?.Get(slot) ?? new SoundSet.Entry("", uint.MaxValue);
        if (entry.Sound.Length > 0)
            Notify("voice-chat", speaker, new JsonObject { ["id"] = slot, ["soundset"] = c.SoundSet, ["sound"] = entry.Sound });
        if (caption && entry.StringRef != uint.MaxValue && data.Text(entry.StringRef, 0) is { Length: > 0 } text)
            emit(new ChatMessage { Channel = ChatChannel.Talk, Speaker = speaker, SpeakerName = world.NameOf(speaker), Text = text }.Write());
    }

    SoundSet? SoundSetOf(int soundset)
    {
        string resref = data.TwoDA("soundset", soundset, "RESREF");
        if (_soundSets.TryGetValue(resref, out var known)) return known;
        SoundSet? set = null;
        try { set = data.Resource(resref, "ssf") is { } bytes ? SoundSet.Read(bytes) : null; }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or ArgumentException) { }
        return _soundSets[resref] = set;
    }

    /// <summary>A sound object, spawned as OpenWinter's server spawns one: its sounds and playback settings as JSON.</summary>
    void SpawnSound(uint id, (float, float, float) at, bool active, bool positional, bool looping, bool continuous, bool random,
        bool randomPosition, float rangeX, float rangeY, uint interval, uint intervalVariation, float pitchVariation, byte volume,
        byte volumeVariation, float minDistance, float maxDistance, byte times, uint hours, List<string> sounds)
    {
        _sounds.Add(id);
        emit(new SpawnMessage
        {
            ObjectId = id, Kind = ObjectKind.Sound, Position = V(at),
            Extra = new JsonObject
            {
                ["sounds"] = new JsonArray(sounds.Select(s => (JsonNode)s).ToArray()), ["active"] = active, ["continuous"] = continuous,
                ["looping"] = looping, ["positional"] = positional, ["random"] = random, ["randomPosition"] = randomPosition,
                ["rangeX"] = Finite(rangeX), ["rangeY"] = Finite(rangeY), ["elevation"] = 0f, ["interval"] = (int)interval,
                ["intervalVariation"] = (int)intervalVariation, ["pitchVariation"] = Finite(pitchVariation), ["volume"] = (int)volume,
                ["volumeVariation"] = (int)volumeVariation, ["minDistance"] = Finite(minDistance), ["maxDistance"] = Finite(maxDistance),
                ["times"] = (int)times, ["hours"] = hours,
            }.ToJsonString(),
        }.Write());
    }

    /// <summary>A script shader uniform for the controlled player (one bank, index 0-15; ints exact).</summary>
    void ShaderUniform(int index, string type, float[] value, int? exact = null)
    {
        var values = new JsonArray();
        foreach (var v in value) values.Add(v);
        var json = new JsonObject { ["shader"] = index, ["type"] = type, ["value"] = values };
        if (exact is { } i) json["int"] = i;
        Notify("shader-uniform", world.Controlled, json);
    }

    /// <summary>One area music or ambience setting changed (the client keeps the rest).</summary>
    void Audio(string key, JsonNode value) => Notify("area-audio", world.AreaId, new JsonObject { [key] = value });

    // -- helpers ------------------------------------------------------------

    Creature Of(uint id)
    {
        if (!_creatures.TryGetValue(id, out var c)) _creatures[id] = c = new Creature();
        return c;
    }

    Shown ShownOf(uint id)
    {
        if (!_shown.TryGetValue(id, out var s)) _shown[id] = s = new Shown();
        return s;
    }

    string Spell(int spell, string column) => data.TwoDA("spells", spell, column) is var v && v != "****" ? v : "";

    static int Int(string s) => int.TryParse(s, out var i) ? i : 0;

    /// <summary>A float the server sent, as one JSON can carry: anything not finite becomes <paramref name="fallback"/>.</summary>
    static float Finite(float v, float fallback = 0) => float.IsFinite(v) ? v : float.IsFinite(fallback) ? fallback : 0;

    static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    static float Scale(VisualTransformData? vt) =>
        Finite(vt?.TotalUpdates.FirstOrDefault(u => u.Scope == 0 && !u.Identity)?.Scale?.X.ValueTo ?? 1f, 1f);

    static Vector3 V((float, float, float) v) => new(Finite(v.Item1), Finite(v.Item2), Finite(v.Item3));

    static JsonArray Vec(Vector3 v) => new(Finite(v.X), Finite(v.Y), Finite(v.Z));

    static JsonArray Vec((float, float, float) v) => Vec(V(v));

    void Notify(string kind, uint subject, JsonObject json) =>
        emit(new NotifyMessage { Kind = kind, Subject = subject, Json = json.ToJsonString() }.Write());
}
