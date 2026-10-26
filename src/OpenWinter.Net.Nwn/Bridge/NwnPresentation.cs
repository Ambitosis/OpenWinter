using System.Numerics;
using System.Text.Json.Nodes;
using OpenWinter.Aurora.Formats;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAddMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAppearanceMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateUpdateMessages;
using VisualEffectUpdate = OpenWinter.Net.Nwn.Messages.GameObjUpdateSharedMessages.VisualEffectUpdate;
using VisualTransformData = OpenWinter.Net.Nwn.Messages.NetTypesMessages.VisualTransformData;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// How the world looks and sounds, as OpenWinter's own server presents it: creature animations become PlayAnimation
/// (script animations, emotes, conjuring and casting with the "spell-conjure", "spell-cast" and "spell-projectile"
/// notifies), knockdowns a condition flag; visual effects at places and on objects "vfx", beams "beam", areas of
/// effect "aoe-created"; visual transforms and highlights "visual-transform" and "hilite-color"; PlaySound, voice
/// chat, sound objects, music and ambience "play-sound", "voice-chat", "sound-object" and "area-audio". The client's
/// emotes go back as the animation input nwmain sends.
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
        public ushort? Pending;
        public int SoundSet = -1;
        public byte Gender;
    }

    readonly Dictionary<uint, Creature> _creatures = [];
    readonly Dictionary<uint, int> _hilites = [];
    readonly Dictionary<(uint, int, int), (float To, int Lerp, float Duration)> _transforms = [];
    readonly Dictionary<uint, (int Row, uint Host)> _aoes = [];
    readonly HashSet<uint> _sounds = [];
    readonly Dictionary<string, SoundSet?> _soundSets = [];

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
                var vfx = new JsonObject { ["vfx"] = (int)v.AnimId, ["miss"] = false, ["scale"] = Scale(v.Vt), ["pos"] = Vec(v.Position), ["facing"] = 0f, ["duration"] = 0f, ["id"] = 0L };
                Notify("vfx", world.AreaId, vfx);
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
        }
        return false;
    }

    /// <summary>A new area: what was known of the last one is dropped, and the area's sound objects are placed.</summary>
    public void Area(AreaMessages.AreaClientArea area)
    {
        _creatures.Keys.Where(id => id != world.Controlled).ToList().ForEach(id => _creatures.Remove(id));
        _hilites.Clear();
        _transforms.Clear();
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
            if (entry is GameObjUpdateObjListUpdateCreature { AnimTypeData: { } knock } k && world.KindOf(k.ObjectId) != null)
            {
                bool down = knock.AnimType == KnockedDown;
                if (down || (_creatures.TryGetValue(k.ObjectId, out var was) && was.Anim == KnockedDown))
                    world.SetFlags(k.ObjectId, down ? ObjectFlags.KnockedDown : 0, ObjectFlags.KnockedDown);
            }
        mirror(list);
        foreach (var entry in list.Entries)
        {
            switch (entry)
            {
                case GameObjUpdateObjListAddCreature add:
                {
                    var c = Of(add.ObjectId);
                    c.Anim = NwnAnimations.Ready;
                    c.Holding = false;
                    // A creature that arrives sitting, kneeling or lying plays that once it is shown.
                    if (add.Anim is not (NwnAnimations.Pause or NwnAnimations.Ready)) c.Pending = add.Anim;
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
                    _aoes[aoe.ObjectId] = (aoe.AreaEffectId, aoe.TargetOid);
                    break;
                case ObjListUpdateAOE { Position: { } at } aoe when _aoes.TryGetValue(aoe.ObjectId, out var shape):
                    AreaOfEffect(aoe.ObjectId, shape.Row, shape.Host, at, aoe.Orientation);
                    break;
                case GameObjUpdateObjListUpdateGeneric g:
                    if (g.Effects is { } placed) Effects(g.ObjectId, placed, creature: false);
                    if (g.VisualTransform is { } gvt) Transform(g.ObjectId, gvt);
                    if (g.HiliteR is { } gr) Hilite(g.ObjectId, gr, g.HiliteG ?? -1, g.HiliteB ?? -1);
                    break;
                case GameObjUpdateDeleteMessages.ObjListDeleteAOE d:
                    if (_aoes.Remove(d.ObjectId)) Notify("aoe-removed", world.AreaId, new JsonObject { ["id"] = d.ObjectId });
                    break;
                case GameObjUpdateDeleteMessages.ObjListDeleteCreature d:
                    _creatures.Remove(d.ObjectId);
                    break;
            }
        }
        // Animations of creatures the mirror has shown by now.
        foreach (var (id, c) in _creatures)
            if (c.Pending is { } pending && world.TryPose(id, out _, out _, out _, out _))
            {
                c.Pending = null;
                Animate(id, new GameObjUpdateAnimationMessages.AnimTypeData { AnimType = pending }, 1);
            }
    }

    // -- animations ---------------------------------------------------------

    void Animate(uint id, GameObjUpdateAnimationMessages.AnimTypeData anim, float speed)
    {
        var c = Of(id);
        ushort code = anim.AnimType;
        bool held = c.Holding;
        c.Anim = code;
        c.Holding = false;
        switch (code)
        {
            case NwnAnimations.Pause or NwnAnimations.Ready:
                // The script's time is up: back to standing.
                if (held) Play(id, Idle, speed, 0, false);
                return;
            case KnockedDown:
                // A condition flag, set before the mirror described the list.
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
        // Walking, attacks, hit reactions and death are the mirror's and combat's; anything else unknown stays still.
        if (NwnAnimations.For(code) is not { } clip) return;
        // A gesture plays over the body's animation: a looping one it replaces has to end first.
        if (held && clip.Gesture) Play(id, Idle, speed, 0, false);
        c.Holding = clip.Looping;
        Play(id, clip.Name, speed, clip.Looping ? UntilReplaced : 0, clip.Gesture);
    }

    void Play(uint id, string clip, float speed, float duration, bool gesture) =>
        emit(new PlayAnimationMessage { ObjectId = id, Animation = clip, Speed = speed > 0 ? speed : 1, Duration = duration, Gesture = gesture }.Write());

    /// <summary>Conjuring: the conjure animation, and for a spell its head, hand and ground visuals, sound and chant.</summary>
    void Conjure(uint id, Creature c, ushort code, GameObjUpdateAnimationMessages.CastAnimData? cast, float speed)
    {
        c.Holding = true;
        if (cast is not { SpellId: not NoSpell } s)
        {
            Play(id, NwnAnimations.ConjureClip("", code), speed, UntilReplaced, false);
            return;
        }
        int spell = (int)s.SpellId;
        float seconds = Int(Spell(spell, "ConjTime")) / 1000f;
        Play(id, NwnAnimations.ConjureClip(Spell(spell, "ConjAnim"), code), speed, seconds > 0 ? seconds : UntilReplaced, false);
        world.TryPose(id, out var at, out _, out _, out _);
        Notify("spell-conjure", id, new JsonObject
        {
            ["spell"] = spell, ["presentation"] = 0, ["target"] = InvalidObject, ["pos"] = Vec(at), ["duration"] = seconds,
            ["conjureLifetime"] = null, ["animation"] = Spell(spell, "ConjAnim"), ["headVfx"] = Spell(spell, "ConjHeadVisual"),
            ["handVfx"] = Spell(spell, "ConjHandVisual"), ["groundVfx"] = Spell(spell, "ConjGrndVisual"),
            ["sound"] = Spell(spell, "ConjSoundVFX"),
            ["voice"] = s.Silent ? "" : Spell(spell, c.Gender == 1 ? "ConjSoundFemale" : "ConjSoundMale"),
            ["metaMagic"] = 0, ["fake"] = false,
        });
    }

    /// <summary>A spell's release: the cast animation and the spell's cast visuals and sound, towards its target.</summary>
    void Cast(uint id, Creature c, ushort code, GameObjUpdateAnimationMessages.AnimTypeData anim, float speed)
    {
        c.Holding = true;
        int spell = anim.Spell is { SpellId: not NoSpell } s ? (int)s.SpellId : -1;
        string style = spell >= 0 ? Spell(spell, "CastAnim") : "";
        Play(id, NwnAnimations.CastClip(style, code), speed, UntilReplaced, false);
        if (spell < 0) return;
        uint target = anim.CastTarget?.TargetOid ?? InvalidObject;
        Vector3 aim = anim.CastTarget?.TargetPosition is { } p ? V(p)
            : world.TryPose(target, out var on, out _, out _, out _) ? on
            : world.TryPose(id, out var self, out _, out _, out _) ? self : default;
        Notify("spell-cast", id, new JsonObject
        {
            ["spell"] = spell, ["presentation"] = 0, ["target"] = target, ["pos"] = Vec(aim),
            ["duration"] = Int(Spell(spell, "CastTime")) / 1000f, ["recovery"] = 0f, ["animation"] = style, ["attackAnimation"] = "",
            ["headVfx"] = Spell(spell, "CastHeadVisual"), ["handVfx"] = Spell(spell, "CastHandVisual"),
            ["groundVfx"] = Spell(spell, "CastGrndVisual"), ["sound"] = Spell(spell, "CastSound"),
            ["item"] = anim.Spell!.CastItemId, ["fake"] = false,
        });
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
            ["travel"] = p.DeltaTime > 1 ? p.DeltaTime / 1000f : FlightTime(Vector3.Distance(from, to)),
        });
    }

    /// <summary>A projectile's flight when the server leaves it to the client: distance over 3 ln(d) + 2 metres a second.</summary>
    static float FlightTime(float distance) => distance <= 1 ? 0.001f : distance / (3 * MathF.Log(distance) + 2);

    // -- visual effects -----------------------------------------------------

    /// <summary>The effect id the bridge gives a lasting visual effect on an object (its object and visualeffects.2da row).</summary>
    public static long EffectId(uint target, ushort effect) => (1L << 40) | ((long)target << 16) | effect;

    bool IsBeam(int row) => data.TwoDA("visualeffects", row, "Type_FD").Equals("B", StringComparison.OrdinalIgnoreCase);

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
                json["pos"] = Vec(V(v.TargetLocation));
                Notify("vfx", world.AreaId, json);
            }
            else Notify("vfx", v.TargetId, json);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Lasting visual effects coming ('A') and going ('D'): beams on any object, the rest on placeables and doors
    /// (a creature's are combat's).
    /// </summary>
    void Effects(uint id, List<VisualEffectUpdate> effects, bool creature)
    {
        foreach (var e in effects)
        {
            bool beam = IsBeam(e.EffectId);
            if (creature && !beam) continue;
            long effect = EffectId(id, e.EffectId);
            if (e.ChangeType == 'D')
            {
                Notify("effect-removed", id, new JsonObject { ["id"] = effect, ["type"] = 0, ["linkId"] = "", ["icon"] = 0 });
                continue;
            }
            if (e.ChangeType != 'A') continue;
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

    /// <summary>An area of effect: vfx_persistent.2da's shape and size, where the server placed it.</summary>
    void AreaOfEffect(uint id, int row, uint host, (float, float, float) at, OrientationUpdate? facing)
    {
        string Cell(string column) => data.TwoDA("vfx_persistent", row, column) is var v && v != "****" ? v : "";
        float F(string column) => float.TryParse(Cell(column), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0;
        // A bearing counts from +Y (as the mirror reads facings); the client's facing from +X.
        float degrees = facing is { Full: true } o ? MathF.Atan2(o.Oy ?? 0, o.Ox ?? 1) * 180f / MathF.PI
            : facing?.Bearing is { } bearing ? (bearing + 90f) % 360f : 0;
        Notify("aoe-created", world.AreaId, new JsonObject
        {
            ["id"] = id, ["aoe"] = row, ["shape"] = Cell("SHAPE").ToUpperInvariant() == "R" ? "R" : "C", ["radius"] = F("RADIUS"),
            ["width"] = F("WIDTH"), ["length"] = F("LENGTH"), ["pos"] = Vec(V(at)), ["facing"] = degrees,
            ["attachedTo"] = host == InvalidObject ? InvalidObject : host,
        });
    }

    // -- transforms and highlights ------------------------------------------

    /// <summary>
    /// SetObjectVisualTransform: each channel (scale, rotation, translation, animation speed) that changed, as
    /// "visual-transform"; a scope back to identity as "visual-transform-clear".
    /// </summary>
    void Transform(uint id, VisualTransformData vt)
    {
        foreach (var u in vt.TotalUpdates)
        {
            if (u.Identity)
            {
                bool had = _transforms.Keys.Any(k => k.Item1 == id && k.Item2 == u.Scope);
                foreach (var k in _transforms.Keys.Where(k => k.Item1 == id && k.Item2 == u.Scope).ToList()) _transforms.Remove(k);
                if (had) Notify("visual-transform-clear", id, new JsonObject { ["scope"] = u.Scope });
                continue;
            }
            Channel(id, u.Scope, 10, u.Scale?.X, 1);
            Channel(id, u.Scope, 21, u.Rotate?.X, 0);
            Channel(id, u.Scope, 22, u.Rotate?.Y, 0);
            Channel(id, u.Scope, 23, u.Rotate?.Z, 0);
            Channel(id, u.Scope, 31, u.Translate?.X, 0);
            Channel(id, u.Scope, 32, u.Translate?.Y, 0);
            Channel(id, u.Scope, 33, u.Translate?.Z, 0);
            Channel(id, u.Scope, 40, u.AnimationSpeed, 1);
        }
    }

    void Channel(uint id, int scope, int transform, NetTypesMessages.LerpFloat? f, float identity)
    {
        if (f == null) return;
        var key = (id, scope, transform);
        var now = (f.ValueTo, (int)f.LerpType, f.Duration);
        if (_transforms.TryGetValue(key, out var known) ? known == now : f.ValueTo == identity && f.LerpType == NetTypesMessages.LerpType.None) return;
        _transforms[key] = now;
        Notify("visual-transform", id, new JsonObject
        {
            ["transform"] = transform, ["scope"] = scope, ["from"] = f.LerpType == NetTypesMessages.LerpType.None ? f.ValueTo : f.ValueFrom,
            ["value"] = f.ValueTo, ["lerp"] = (int)f.LerpType, ["duration"] = f.Duration, ["pauseWithGame"] = f.TimerType == NetTypesMessages.LerpTimerType.WorldTime,
            ["behavior"] = (f.Flags & NetTypesMessages.LerpFlags.Bounce) != 0 ? 1 : 0, ["repeats"] = f.RepeatsRemaining,
            ["elapsed"] = f.Progress * f.Duration,
        });
    }

    /// <summary>SetObjectHiliteColor: a colour (0xRRGGBB) for the object's highlight, or -1 for its own.</summary>
    void Hilite(uint id, float r, float g, float b)
    {
        int color = r < 0 ? -1 : (Byte(r) << 16) | (Byte(g) << 8) | Byte(b);
        if (_hilites.TryGetValue(id, out var known) ? known == color : color == -1) return;
        _hilites[id] = color;
        Notify("hilite-color", id, new JsonObject { ["color"] = color });
    }

    static int Byte(float channel) => Math.Clamp((int)MathF.Round(channel * 255), 0, 255);

    // -- sounds -------------------------------------------------------------

    void PlaySound(uint source, string resref)
    {
        if (resref.Length == 0) return;
        world.TryPose(source, out var at, out _, out _, out _);
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
        catch (InvalidDataException) { }
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
                ["rangeX"] = rangeX, ["rangeY"] = rangeY, ["elevation"] = 0f, ["interval"] = (int)interval,
                ["intervalVariation"] = (int)intervalVariation, ["pitchVariation"] = pitchVariation, ["volume"] = (int)volume,
                ["volumeVariation"] = (int)volumeVariation, ["minDistance"] = minDistance, ["maxDistance"] = maxDistance,
                ["times"] = (int)times, ["hours"] = hours,
            }.ToJsonString(),
        }.Write());
    }

    /// <summary>One area music or ambience setting changed (the client keeps the rest).</summary>
    void Audio(string key, JsonNode value) => Notify("area-audio", world.AreaId, new JsonObject { [key] = value });

    // -- helpers ------------------------------------------------------------

    Creature Of(uint id)
    {
        if (!_creatures.TryGetValue(id, out var c)) _creatures[id] = c = new Creature();
        return c;
    }

    string Spell(int spell, string column) => data.TwoDA("spells", spell, column) is var v && v != "****" ? v : "";

    static int Int(string s) => int.TryParse(s, out var i) ? i : 0;

    static float Scale(VisualTransformData? vt) => vt?.TotalUpdates.FirstOrDefault(u => u.Scope == 0 && !u.Identity)?.Scale?.X.ValueTo ?? 1f;

    static Vector3 V((float, float, float) v) => new(v.Item1, v.Item2, v.Item3);

    static JsonArray Vec(Vector3 v) => new(v.X, v.Y, v.Z);

    static JsonArray Vec((float, float, float) v) => new(v.Item1, v.Item2, v.Item3);

    void Notify(string kind, uint subject, JsonObject json) =>
        emit(new NotifyMessage { Kind = kind, Subject = subject, Json = json.ToJsonString() }.Write());
}
