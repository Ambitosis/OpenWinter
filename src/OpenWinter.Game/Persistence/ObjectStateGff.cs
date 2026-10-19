using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.SaveGames;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Persistence;

/// <summary>
/// An object's state in its serialized form ("bSaveObjectState": local variables, effects, the action queue and
/// transition info), in the fields the original writes (parity suites persist_objects and persist_effects):
/// <list type="bullet">
/// <item><c>VarTable</c> on every object type;</item>
/// <item><c>EffectList</c> on creatures and items (<see cref="NativeEffects"/>); other objects have none;</item>
/// <item><c>ActionList</c> on every object type: <c>ActionId</c>, <c>GroupActionId</c>, <c>NumParams</c> and
/// <c>Paramaters</c> (sic), each a <c>Type</c> (1 int, 2 float, 3 object, 4 string, 5 script) and a <c>Value</c>;</item>
/// <item><c>Transition</c> on every object type: <c>TargetObjectId</c> (SetTransitionTarget) and <c>TargetTag</c>.</item>
/// </list>
/// Without the state, those fields are absent (items always carry them). OpenWinter's own actions keep their
/// exact state in an "OWAction" field; the original's are read for ActionWait and ActionMoveToPoint.
/// </summary>
public static class ObjectStateGff
{
    public const string OpenWinterActionField = "OWAction";

    /// <summary>The original's ActionId for OpenWinter's actions (measured: persist_effects act.*).</summary>
    static int NativeActionId(GameAction a) => a switch
    {
        MoveAction => 1,
        AnimationAction => 6,
        SpeakAction => 14,
        WaitAction => 30,
        DoCommandAction => 37,
        JumpAction => 48,
        _ => 65535,
    };

    public static bool HasEffectList(GameObject o) => o is Creature or Item;

    /// <summary>Adds the state fields to a serialized object.</summary>
    public static void Write(World world, GameObject o, GffStruct s)
    {
        ObjectSerializer.WriteLocals(s, o.Locals);
        if (!s.Has("VarTable")) s.SetList("VarTable", new List<GffStruct>());
        if (HasEffectList(o))
            s.SetList("EffectList", o.Effects.Where(e => e.IsValid).Select(e => NativeEffects.Write(world, e)).ToList());
        s.SetList("ActionList", o.Actions.Select((a, i) => Action(world, o, a, i)).ToList());
        s.SetStruct("Transition", new GffStruct(1)
            .SetDWord("TargetObjectId", o.TransitionResolved ? o.TransitionTargetId : GameObject.InvalidId)
            .SetString("TargetTag", ""));
    }

    /// <summary>Removes the state fields (an object serialized without its state).</summary>
    public static void Strip(GffStruct s)
    {
        s.Remove("VarTable");
        s.Remove("EffectList");
        s.Remove("ActionList");
        s.Remove("Transition");
    }

    /// <summary>
    /// An ActionList entry. The parameters are written in the original's layout for the actions measured
    /// (persist_effects act.*); GroupActionId counts up through the queue (each order the original queued got its own).
    /// </summary>
    static GffStruct Action(World world, GameObject owner, GameAction a, int index)
    {
        var s = new GffStruct(0).SetDWord("ActionId", (uint)NativeActionId(a)).SetWord("GroupActionId", index);
        var ps = new List<GffStruct>();
        GffStruct P(int type) { var p = new GffStruct(1).SetDWord("Type", (uint)type); ps.Add(p); return p; }
        void Int(long v) => P(1).SetInt("Value", (int)v);
        void Float(float v) => P(2).SetFloat("Value", v);
        void Obj(uint v) => P(3).SetDWord("Value", v);
        switch (a)
        {
            case WaitAction w: Float(w.Remaining); break;
            case MoveAction m:
                Float(m.Target.X); Float(m.Target.Y); Float(m.Target.Z);
                Obj(owner.Area?.Id ?? GameObject.InvalidId); Obj(m.TargetObject);
                Int(m.Run ? 1 : 0); Float(m.TargetObject == GameObject.InvalidId ? 0 : m.Range); Int(0); Float(0);
                break;
            case SpeakAction sp: P(4).SetString("Value", sp.Text); Int(sp.Volume); break;
            case AnimationAction an: Int(an.Animation); Float(an.Speed); Float(an.Duration); Int(0); break;
            case JumpAction: Obj(GameObject.InvalidId); Int(0); break;
            case DoCommandAction: P(5).SetStruct("Value", new GffStruct(0)); break;
        }
        s.SetWord("NumParams", ps.Count);
        s.SetList("Paramaters", ps);
        if (DetachedState.Write(world, a, typeof(GameAction)) is { } exact) s.SetString(OpenWinterActionField, exact);
        return s;
    }

    /// <summary>
    /// Reads the state fields into a newly made object: locals, transition, effects (applied again for the time
    /// they had left) and the action queue. Fields that are absent leave the object as it is.
    /// </summary>
    /// <param name="map">Translates object ids of the original's data (its save games) to this world's; null keeps them.</param>
    public static void Read(World world, GameObject o, GffStruct s, ICollection<string>? warnings = null, Func<uint, uint>? map = null)
    {
        if (s.Has("VarTable")) ObjectSerializer.ReadLocals(s, o.Locals);
        if (s.GetStruct("Transition") is { } t)
        {
            uint target = (uint)t.GetInt("TargetObjectId", GameObject.InvalidId);
            if (map != null) target = map(target);
            if (target != GameObject.InvalidId && world.Get(target) is { Area: not null })
            {
                o.TransitionTargetId = target;
                o.TransitionResolved = true;
            }
        }
        if (HasEffectList(o) && s.Has("EffectList"))
        {
            var k = EffectConstants.Of(world);
            var effects = world.System<EffectSystem>();
            foreach (var entry in s.GetList("EffectList"))
            {
                if (NativeEffects.Read(world, entry, out float remaining, warnings, map) is not { } e) continue;
                if (e.DurationType == k.Temporary && remaining <= 0) continue;
                int duration = e.DurationType == k.Temporary ? k.Temporary : k.Permanent;
                effects.Apply(e, duration, o, remaining);
            }
        }
        if (s.Has("ActionList"))
        {
            foreach (var entry in s.GetList("ActionList"))
                if (ReadAction(world, entry, warnings) is { } a) o.Actions.Add(a);
        }
    }

    static GameAction? ReadAction(World world, GffStruct s, ICollection<string>? warnings)
    {
        if (s.GetString(OpenWinterActionField) is { Length: > 0 } exact)
            return DetachedState.Read<GameAction>(world, exact, warnings);
        var ps = s.GetList("Paramaters");
        float F(int i) => i < ps.Count ? ps[i].GetFloat("Value") : 0f;
        long I(int i) => i < ps.Count ? ps[i].GetInt("Value") : 0;
        switch ((int)s.GetInt("ActionId"))
        {
            case 30: return new WaitAction { Remaining = F(0) };
            // ActionMoveToPoint: x, y, z, area, target object, run, range, ...
            case 1: return new MoveAction { Target = new Vector3(F(0), F(1), F(2)), Run = I(5) != 0, PassClosedDoors = true };
            default:
                warnings?.Add($"action {s.GetInt("ActionId")} of the original is not read yet; skipped");
                return null;
        }
    }
}
