using System.Globalization;
using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Effect: effects as the original's internal records (parity suite nwnx_effect). An unpacked effect carries
/// the original's own effect number, its subtype ORed with the duration type, the calendar time it expires, eight
/// integers in the original's layout, four floats, six strings, four objects and two vectors (the same fields the
/// original's saved effect lists hold; <see cref="NativeEffects"/>). A link is its own record (number 40) whose
/// left and right are the two effects linked. The original also lists, beside each effect that shows a portrait
/// icon, an icon record (number 67) with the same id, and every creature carries an innate record of its racial
/// type (number 68); the "true" effect lists include those.
/// </summary>
static class EffectPlugin
{
    const string P = "Effect";
    const int NativeLink = 40, NativeIcon = 67, NativeRace = 68;
    /// <summary>Stands for an internal record a script holds (GetAppliedEffect): no scripts' type, but not invalid.</summary>
    const int InternalType = -NativeRace;

    sealed class Unpacked
    {
        public string Id = "";
        public int Type, SubType, Day, Time, SpellId = -1, CasterLevel = -1, Expose = 1, ShowIcon = 1, NumIntegers = 8;
        public float Duration;
        public uint Creator = GameObject.InvalidId;
        public Effect? Left, Right;
        public bool LeftValid, RightValid;
        public int[] Ints = new int[8];
        public float[] Floats = new float[4];
        public string[] Strings = ["", "", "", "", "", ""];
        public uint[] Objects = [GameObject.InvalidId, GameObject.InvalidId, GameObject.InvalidId, GameObject.InvalidId];
        public Vector3[] Vectors = new Vector3[2];
        public string Tag = "", ItemProp = "0";
    }

    /// <summary>
    /// One entry of an object's full effect list: an effect, the icon record beside it, a creature's race, or one of
    /// the two records the original adds to an ultravision effect (<see cref="Vision"/> 1: its visual, 2: the other).
    /// </summary>
    readonly record struct TrueEntry(Effect Effect, int Icon, int Race = -1, int Vision = 0);

    const int UltravisionVisual = 244, NativeVisual = 30, NativeVision = 69;

    static EffectSystem Effects(World w) => w.System<EffectSystem>();

    // ---------------------------------------------------------------- unpacking

    /// <summary>
    /// An effect's id as the original reports it: every part of a link applied together shares one, and an effect
    /// gets its id when it is made, not when it is applied.
    /// </summary>
    static long IdOf(World w, Effect e)
    {
        if (e.UniqueId == 0) return e.ScriptId;
        if (e.LinkId.Length == 0 || w.Get(e.Target) is not { } holder) return e.UniqueId;
        long id = e.UniqueId;
        foreach (var x in holder.Effects) if (x.LinkId == e.LinkId && x.UniqueId < id) id = x.UniqueId;
        return id;
    }

    static Unpacked Unpack(World w, Effect e)
    {
        var u = new Unpacked();
        if (e.Type == 0 && !e.IsLinked) return u;
        if (e.Type == InternalType)
        {
            if (e.Ints[7] != NativeVision) return UnpackRace(e.Creator, e.Ints[0]);
            var v = UnpackIcon(w, e, 0);
            v.Type = NativeVision;
            v.Ints[0] = e.Ints[0];
            v.ShowIcon = 0;
            return v;
        }
        var k = EffectConstants.Of(w);
        if (e.IsLinked)
        {
            u.Id = IdOf(w, e).ToString(CultureInfo.InvariantCulture);
            u.Type = NativeLink;
            u.SubType = (e.SubType & ~7) | (e.DurationType & 7);
            u.Duration = e.Duration;
            u.Creator = e.Creator;
            u.SpellId = e.SpellId;
            u.CasterLevel = e.CasterLevel > 0 ? e.CasterLevel : -1;
            u.Tag = e.Tag;
            // The original links two effects at a time: the first argument of EffectLinkEffects on the left.
            var leaves = e.Leaves;
            u.Left = leaves[0].Copy(false);
            u.Right = leaves.Count == 2 ? leaves[1].Copy(false) : Link(w, e, leaves.Skip(1));
            u.LeftValid = u.RightValid = true;
            return u;
        }
        var s = NativeEffects.Write(w, e);
        u.Id = IdOf(w, e).ToString(CultureInfo.InvariantCulture);
        u.Type = (int)s.GetInt("Type");
        u.SubType = (int)s.GetInt("SubType");
        u.Duration = e.Duration;
        u.Day = (int)s.GetInt("ExpireDay");
        u.Time = (int)s.GetInt("ExpireTime");
        u.Creator = (uint)s.GetInt("CreatorId", GameObject.InvalidId);
        u.SpellId = unchecked((int)(uint)s.GetInt("SpellId", 0xFFFFFFFF));
        u.CasterLevel = unchecked((int)(uint)s.GetInt("CasterLevel", 0xFFFFFFFF));
        u.Expose = (int)s.GetInt("IsExposed");
        u.ShowIcon = (int)s.GetInt("IsIconShown");
        u.Tag = s.GetString("CustomTag");
        u.NumIntegers = e.Type == k.Damage ? 39 : 8;
        var ints = s.GetList("IntList");
        for (int i = 0; i < 8 && i < ints.Count; i++) u.Ints[i] = (int)ints[i].GetInt("Value");
        // An interval effect only has a next tick once applied.
        if (e.Target == GameObject.InvalidId && (e.Type == k.RunScript || e.Type == k.Regenerate)) u.Ints[2] = u.Ints[3] = 0;
        // Feat and racial modifiers are innate records, exposed but without an icon.
        if (e.Innate)
        {
            u.SubType = (u.SubType & ~7) | 4;
            u.Expose = 1;
            u.ShowIcon = 0;
        }
        // A lasting effect applied as instant is in force but out of the scripts' list: an exposed instant record.
        else if (e.Hidden && e.Target != GameObject.InvalidId)
        {
            u.SubType &= ~7;
            u.Expose = u.ShowIcon = 1;
        }
        var floats = s.GetList("FloatList");
        for (int i = 0; i < 4 && i < floats.Count; i++) u.Floats[i] = floats[i].GetFloat("Value");
        var strings = s.GetList("StringList");
        for (int i = 0; i < 6 && i < strings.Count; i++) u.Strings[i] = strings[i].GetString("Value");
        var objects = s.GetList("ObjectList");
        for (int i = 0; i < 4 && i < objects.Count; i++) u.Objects[i] = (uint)objects[i].GetInt("Value", GameObject.InvalidId);
        var vectors = s.GetList("VectorList");
        for (int i = 0; i < 2 && i < vectors.Count; i++)
            u.Vectors[i] = new Vector3(vectors[i].GetFloat("ValueX"), vectors[i].GetFloat("ValueY"), vectors[i].GetFloat("ValueZ"));
        return u;
    }

    /// <summary>The icon record beside an effect: its number, subtype, timing, creator and id, the icon in the first integer.</summary>
    static Unpacked UnpackIcon(World w, Effect e, int icon)
    {
        var u = Unpack(w, e.Copy(false));
        u.Type = NativeIcon;
        u.Tag = "";
        u.Expose = 0;
        u.ShowIcon = 1;
        u.NumIntegers = 8;
        u.Ints = new int[8];
        u.Ints[0] = icon;
        u.Floats = new float[4];
        u.Strings = ["", "", "", "", "", ""];
        u.Objects = [GameObject.InvalidId, GameObject.InvalidId, GameObject.InvalidId, GameObject.InvalidId];
        u.Vectors = new Vector3[2];
        return u;
    }

    /// <summary>A creature's racial record: innate, made by the creature itself, the race in the first integer.</summary>
    static Unpacked UnpackRace(uint creature, int race)
    {
        var u = new Unpacked { Type = NativeRace, SubType = 4, Creator = creature, CasterLevel = 0 };
        u.Ints[0] = race;
        return u;
    }

    static void PushUnpacked(NwnxCall n, Unpacked u, bool links)
    {
        n.Push(u.Id);
        n.Push(u.Type);
        n.Push(u.SubType);
        n.Push(u.Duration);
        n.Push(u.Day);
        n.Push(u.Time);
        n.PushObject(u.Creator);
        n.Push(u.SpellId);
        n.Push(u.Expose);
        n.Push(u.ShowIcon);
        n.Push(u.CasterLevel);
        if (links)
        {
            n.Push(new NwnxValue(NwnxType.Effect, u.Left ?? new Effect()));
            n.Push(u.LeftValid);
            n.Push(new NwnxValue(NwnxType.Effect, u.Right ?? new Effect()));
            n.Push(u.RightValid);
        }
        n.Push(u.NumIntegers);
        foreach (int i in u.Ints) n.Push(i);
        foreach (float f in u.Floats) n.Push(f);
        foreach (var s in u.Strings) n.Push(s);
        foreach (uint o in u.Objects) n.PushObject(o);
        foreach (var v in u.Vectors) n.Push(new NwnxValue(NwnxType.Vector, v));
        n.Push(u.Tag);
        n.Push(u.ItemProp);
    }

    /// <summary>Pops what __NWNX_Effect_ResolvePack pushed (without the type and links when replacing).</summary>
    static Unpacked PopUnpacked(NwnxCall n, bool full)
    {
        var u = new Unpacked();
        u.ItemProp = n.PopString();
        u.Tag = n.PopString();
        for (int i = 1; i >= 0; i--) u.Vectors[i] = n.PopVector();
        for (int i = 3; i >= 0; i--) u.Objects[i] = n.PopObjectId();
        for (int i = 5; i >= 0; i--) u.Strings[i] = n.PopString();
        for (int i = 3; i >= 0; i--) u.Floats[i] = n.PopFloat();
        for (int i = 7; i >= 0; i--) u.Ints[i] = n.PopInt();
        u.NumIntegers = n.PopInt();
        if (full)
        {
            u.RightValid = n.PopInt() != 0;
            u.Right = n.PopEffect();
            u.LeftValid = n.PopInt() != 0;
            u.Left = n.PopEffect();
        }
        u.CasterLevel = n.PopInt();
        u.ShowIcon = n.PopInt();
        u.Expose = n.PopInt();
        u.SpellId = n.PopInt();
        u.Creator = n.PopObjectId();
        u.Time = n.PopInt();
        u.Day = n.PopInt();
        u.Duration = n.PopFloat();
        u.SubType = n.PopInt();
        if (full) u.Type = n.PopInt();
        return u;
    }

    // ---------------------------------------------------------------- packing

    /// <summary>The effect an unpacked record describes (without links), or null for numbers not known here.</summary>
    static Effect? Single(World w, Unpacked u, int native, out float remaining)
    {
        var s = new GffStruct(2)
            .Set("Id", GffType.DWord64, ulong.TryParse(u.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong id) ? id : 0UL)
            .SetWord("Type", native)
            .SetWord("SubType", u.SubType)
            .SetFloat("Duration", u.Duration)
            .SetDWord("ExpireDay", unchecked((uint)u.Day))
            .SetDWord("ExpireTime", unchecked((uint)u.Time))
            .SetDWord("CreatorId", u.Creator)
            .SetDWord("SpellId", unchecked((uint)u.SpellId))
            .SetDWord("CasterLevel", unchecked((uint)u.CasterLevel))
            .SetString("CustomTag", u.Tag);
        s.SetList("IntList", u.Ints.Select(v => new GffStruct(3).SetInt("Value", v)).ToList());
        s.SetList("FloatList", u.Floats.Select(v => new GffStruct(4).SetFloat("Value", v)).ToList());
        s.SetList("StringList", u.Strings.Select(v => new GffStruct(5).SetString("Value", v)).ToList());
        s.SetList("ObjectList", u.Objects.Select(v => new GffStruct(6).SetDWord("Value", v)).ToList());
        s.SetList("VectorList", u.Vectors.Select(v => new GffStruct(7).SetFloat("ValueX", v.X).SetFloat("ValueY", v.Y).SetFloat("ValueZ", v.Z)).ToList());
        var e = NativeEffects.Read(w, s, out remaining);
        if (e == null) return null;
        e.Target = GameObject.InvalidId;
        return e;
    }

    static Effect Pack(World w, Unpacked u)
    {
        var sys = Effects(w);
        Effect? self = u.Type == NativeLink ? null : Single(w, u, u.Type, out _);
        var parts = new List<Effect>();
        if (u.LeftValid && u.Left is { } left && (left.IsValid || left.IsLinked)) parts.Add(left);
        if (self != null) parts.Add(self);
        if (u.RightValid && u.Right is { } right && (right.IsValid || right.IsLinked)) parts.Add(right);
        if (parts.Count == 0) return new Effect();
        Effect result;
        if (parts.Count == 1 && self != null) result = self;
        else
        {
            // A link: the record's own fields go to every linked effect, as linking does.
            var head = self ?? parts[^1];
            result = Link(w, head, parts.SelectMany(p => p.Leaves));
            if (u.Type == NativeLink)
            {
                var k = EffectConstants.Of(w);
                result = result.With(x =>
                {
                    x.SubType = u.SubType & ~7;
                    x.DurationType = u.SubType & 7;
                    x.Duration = u.Duration;
                    x.Creator = u.Creator;
                    x.SpellId = u.SpellId;
                    x.CasterLevel = u.CasterLevel is > 0 and < 0xFFFF ? u.CasterLevel : 0;
                    x.Tag = u.Tag;
                    if (x.DurationType != k.Temporary) x.Duration = 0;
                });
            }
        }
        result.ScriptId = sys.NextId();
        return result;
    }

    /// <summary>A link of <paramref name="leaves"/> with <paramref name="head"/>'s own fields (EffectLinkEffects).</summary>
    static Effect Link(World w, Effect head, IEnumerable<Effect> leaves)
    {
        var sys = Effects(w);
        var link = head.Copy(false);
        link.Linked.Clear();
        foreach (var leaf in leaves) link.Linked.Add(leaf.Copy(false));
        link.LinkId = sys.NewLinkId();
        foreach (var leaf in link.Linked) leaf.LinkId = link.LinkId;
        return link;
    }

    // ---------------------------------------------------------------- the full list

    /// <summary>Every effect on an object, hidden ones included, with the original's icon records, in its order.</summary>
    static List<TrueEntry> TrueEffects(World w, GameObject? o)
    {
        if (o == null) return [];
        var k = EffectConstants.Of(w);
        var sys = Effects(w);
        var list = new List<(TrueEntry Entry, int Native, long Order)>();
        int ultravision = k["EFFECT_TYPE_ULTRAVISION"];
        // Within a kind, records go in the order their effects were applied.
        foreach (var e in o.Effects)
        {
            list.Add((new TrueEntry(e, 0), NativeOf(k, e), e.UniqueId));
            if (!e.Hidden && e.Type != k.Icon && sys.IconFor(e) is > 0 and var icon) list.Add((new TrueEntry(e, icon), NativeIcon, e.UniqueId));
            if (e.Type == ultravision)
            {
                list.Add((new TrueEntry(e, 0, -1, 1), NativeVisual, e.UniqueId));
                list.Add((new TrueEntry(e, 0, -1, 2), NativeVision, e.UniqueId));
            }
        }
        if (o is Creature c) list.Add((new TrueEntry(new Effect(), 0, BirthRace(w, c)), NativeRace, long.MaxValue));
        return list.OrderBy(x => x.Native).ThenBy(x => x.Order).Select(x => x.Entry).ToList();
    }

    static int NativeOf(EffectConstants k, Effect e) => NativeEffects.NativeType(k, e).Native;

    /// <summary>The race the creature's racial record was made with: a later NWNX_Creature_SetRacialType leaves it (nwnx_race).</summary>
    static int BirthRace(World w, Creature c) =>
        w.Systems.OfType<NwnxRegistry>().FirstOrDefault() is { } r && r.HasState(c.Id) && r.ObjectState(c.Id).TryGetValue(NwnxRegistry.BirthRaceKey, out var v) && v.Value is int race
            ? race : c.Race;

    static Unpacked UnpackEntry(World w, GameObject o, TrueEntry t) =>
        t.Race >= 0 ? UnpackRace(o.Id, t.Race) : t.Vision > 0 ? UnpackVision(w, t.Effect, t.Vision)
        : t.Icon > 0 ? UnpackIcon(w, t.Effect, t.Icon) : Unpack(w, t.Effect);

    /// <summary>An ultravision effect's visual (244) or its other record (number 69, 3 in its first integer): not exposed.</summary>
    static Unpacked UnpackVision(World w, Effect e, int vision)
    {
        var u = UnpackIcon(w, e, 0);
        u.Type = vision == 1 ? NativeVisual : NativeVision;
        u.Ints[0] = vision == 1 ? UltravisionVisual : 3;
        if (vision == 1) u.Floats[0] = 1;
        u.ShowIcon = 0;
        return u;
    }

    static Effect EntryEffect(World w, GameObject o, TrueEntry t)
    {
        if (t.Race >= 0)
        {
            var race = new Effect { Type = InternalType, Creator = o.Id };
            race.Ints[0] = t.Race;
            return race;
        }
        if (t.Vision > 0)
        {
            var k0 = EffectConstants.Of(w);
            var record = t.Effect.Copy(false);
            record.Type = t.Vision == 1 ? k0.VisualEffect : InternalType;
            record.Ints = new int[8];
            record.Ints[0] = t.Vision == 1 ? UltravisionVisual : 3;
            record.Ints[7] = NativeVision;
            record.Tag = "";
            record.DurationType = k0.Instant;
            record.Hidden = true;
            return record;
        }
        if (t.Icon == 0) return t.Effect;
        var k = EffectConstants.Of(w);
        var icon = t.Effect.Copy(false);
        icon.Type = k.Icon;
        icon.Ints = new int[8];
        icon.Ints[0] = t.Icon;
        icon.Tag = "";
        // Not a scripts' effect: GetIsEffectValid is false for it.
        icon.DurationType = k.Instant;
        icon.Hidden = true;
        return icon;
    }

    // ---------------------------------------------------------------- functions

    [NwnxFunc(P, "UnpackEffect")]
    static void UnpackEffect(NwnxCall n) => PushUnpacked(n, Unpack(n.World, n.PopEffect() ?? new Effect()), links: true);

    [NwnxFunc(P, "PackEffect")]
    static void PackEffect(NwnxCall n) => n.Push(new NwnxValue(NwnxType.Effect, Pack(n.World, PopUnpacked(n, full: true))));

    [NwnxFunc(P, "GetTrueEffectCount")] static void TrueCount(NwnxCall n) => n.Push(TrueEffects(n.World, n.PopObject()).Count);
    [NwnxFunc(P, "GetAppliedEffectCount")] static void AppliedCount(NwnxCall n) => n.Push(TrueEffects(n.World, n.PopObject()).Count);

    [NwnxFunc(P, "GetTrueEffect")]
    static void TrueEffect(NwnxCall n)
    {
        var o = n.PopObject();
        var list = TrueEffects(n.World, o);
        int index = n.PopInt();
        var u = index >= 0 && index < list.Count ? UnpackEntry(n.World, o!, list[index]) : new Unpacked();
        PushUnpacked(n, u, links: false);
    }

    [NwnxFunc(P, "GetAppliedEffect")]
    static void AppliedEffect(NwnxCall n)
    {
        var o = n.PopObject();
        var list = TrueEffects(n.World, o);
        int index = n.PopInt();
        n.Push(new NwnxValue(NwnxType.Effect, index >= 0 && index < list.Count ? EntryEffect(n.World, o!, list[index]).Copy() : new Effect()));
    }

    /// <summary>Any effect with a type counts, instant ones and internal kinds included.</summary>
    [NwnxFunc(P, "GetIsEffectValid")]
    static void IsValid(NwnxCall n) => n.Push(n.PopEffect() is { } e && (e.Type != 0 || e.IsLinked));

    [NwnxFunc(P, "SetEffectCreator")]
    static void SetCreator(NwnxCall n)
    {
        var e = n.PopEffect() ?? new Effect();
        uint creator = n.PopObjectId();
        n.Push(new NwnxValue(NwnxType.Effect, e.With(x => x.Creator = creator)));
    }

    /// <summary>Applies an effect as it is, with its own duration type and duration.</summary>
    [NwnxFunc(P, "Apply")]
    static void Apply(NwnxCall n)
    {
        var e = n.PopEffect();
        var target = n.PopObject();
        if (e == null || target == null) return;
        Effects(n.World).Apply(e, e.DurationType, target, e.Duration);
    }

    /// <summary>
    /// Gives the applied effects with <paramref name="eOld"/>'s id <paramref name="eNew"/>'s subtype, duration type,
    /// duration, tag and spell; returns how many records changed (icon records included). A duration type changed to
    /// instant takes the effect out of GetFirstEffect's list while it stays in force, as in the original.
    /// </summary>
    [NwnxFunc(P, "ReplaceEffect")]
    static void ReplaceEffect(NwnxCall n)
    {
        var o = n.PopObject();
        var old = n.PopEffect();
        var neu = n.PopEffect();
        if (o == null || old == null || neu == null || old.UniqueId == 0 || old.Type != neu.Type) { n.Push(0); return; }
        var sys = Effects(n.World);
        long id = IdOf(n.World, old);
        int count = 0;
        foreach (var e in o.Effects.Where(x => x.Type == neu.Type && IdOf(n.World, x) == id).ToList())
        {
            count += sys.IconFor(e) > 0 && !e.Hidden ? 2 : 1;
            Retime(n.World, e, neu.SubType, neu.DurationType, neu.Duration);
            e.Tag = neu.Tag;
            e.SpellId = neu.SpellId;
            e.CasterLevel = neu.CasterLevel;
        }
        if (count > 0) n.World.MarkChanged(o);
        n.Push(count);
    }

    static void Retime(World w, Effect e, int subType, int durationType, float duration)
    {
        var k = EffectConstants.Of(w);
        e.SubType = subType;
        if (e.DurationType != durationType)
        {
            e.DurationType = durationType;
            e.Hidden = durationType == k.Instant;
        }
        e.Duration = duration;
        e.ExpiresAt = durationType == k.Temporary ? e.AppliedAt + duration : 0;
    }

    /// <summary>Overwrites the fields of the nth record of the full list (not its type or id).</summary>
    [NwnxFunc(P, "ReplaceEffectByIndex")]
    static void ReplaceByIndex(NwnxCall n)
    {
        var o = n.PopObject();
        int index = n.PopInt();
        var u = PopUnpacked(n, full: false);
        var list = TrueEffects(n.World, o);
        if (o == null || index < 0 || index >= list.Count || list[index].Icon > 0 || list[index].Race >= 0) return;
        var e = list[index].Effect;
        var k = EffectConstants.Of(n.World);
        u.Id = IdOf(n.World, e).ToString(CultureInfo.InvariantCulture);
        if (Single(n.World, u, NativeOf(k, e), out float remaining) is not { } src) return;
        bool hidden = e.Hidden;
        Retime(n.World, e, src.SubType, src.DurationType, src.Duration);
        if (src.DurationType == k.Temporary) e.ExpiresAt = n.World.Time + remaining;
        e.Hidden = hidden || src.DurationType == k.Instant;
        e.Creator = src.Creator;
        e.SpellId = src.SpellId;
        e.CasterLevel = src.CasterLevel;
        e.Tag = src.Tag;
        e.Ints = src.Ints;
        e.Floats = src.Floats;
        e.Strings = src.Strings;
        e.Objects = src.Objects;
        e.Vectors = src.Vectors;
        e.VersusRace = src.VersusRace;
        e.VersusLawChaos = src.VersusLawChaos;
        e.VersusGoodEvil = src.VersusGoodEvil;
        n.World.MarkChanged(o);
    }

    [NwnxFunc(P, "RemoveEffectById")]
    static void RemoveById(NwnxCall n)
    {
        var o = n.PopObject();
        var text = n.PopString();
        if (o == null || !long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id) || id == 0) { n.Push(0); return; }
        var sys = Effects(n.World);
        var gone = o.Effects.Where(e => IdOf(n.World, e) == id).ToList();
        foreach (var e in gone) sys.RemoveOnly(o, e);
        n.Push(gone.Count > 0);
    }
}
