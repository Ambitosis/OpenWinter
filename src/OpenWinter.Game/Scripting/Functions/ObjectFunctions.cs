using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>Object identity, lookup, iteration, position and creation.</summary>
static class ObjectFunctions
{
    public const int TypeAll = 32767;

    /// <summary>OBJECT_TYPE_* for scripts (areas and the module report 0).</summary>
    public static int NwType(GameObject? o) => o == null ? 0 : (int)o.Type < 0x1000 ? (int)o.Type : 0;

    [NwFunc("GetIsObjectValid")] static object? Valid(Call c) => c.Obj(0) != null ? 1 : 0;
    /// <summary>Sound objects have no OBJECT_TYPE_* of their own: the original reports OBJECT_TYPE_ALL (audio_scripts).</summary>
    [NwFunc("GetObjectType")] static object? Type(Call c) => c.Obj(0) is Sound ? TypeAll : NwType(c.Obj(0));
    [NwFunc("GetTag")] static object? Tag(Call c) => c.Obj(0)?.Tag ?? "";
    /// <summary>Does nothing on the module; tags keep at most 63 characters.</summary>
    [NwFunc("SetTag")] static object? SetTag(Call c)
    {
        if (c.Obj(0) is { } o and not Objects.Module) { var t = CleanTag(c.S(1)); c.World.SetTag(o, t.Length > 63 ? t[..63] : t); }
        return null;
    }

    /// <summary>Tags a script sets keep only letters, digits and underscores ("x.y.z" becomes "xyz"), as in the original.</summary>
    public static string CleanTag(string tag)
    {
        foreach (char ch in tag)
            if (!(char.IsAsciiLetterOrDigit(ch) || ch == '_')) return string.Concat(tag.Where(x => char.IsAsciiLetterOrDigit(x) || x == '_'));
        return tag;
    }
    /// <summary>Sound objects keep neither their blueprint's resref nor their name (audio_scripts).</summary>
    [NwFunc("GetResRef")] static object? ResRef(Call c) => c.Obj(0) is Sound ? "" : c.Obj(0)?.ResRef.ToLowerInvariant() ?? "";
    [NwFunc("GetName")] static object? Name(Call c) =>
        c.Obj(0) is Sound ? "" : c.Obj(0) is { } o ? (c.I(1) != 0 && o.OriginalName is { } orig ? orig : o.Name).Resolve(c.World.Resources.Talk) : "";
    /// <summary>Waypoints and the module keep their names; "" restores the original name.</summary>
    [NwFunc("SetName")] static object? SetName(Call c)
    {
        if (c.Obj(0) is { } o and not (Waypoint or Objects.Module))
        {
            o.OriginalName ??= o.Name;
            o.Name = c.S(1).Length == 0 ? o.OriginalName : new LocString(c.S(1));
            c.World.MarkChanged(o);
        }
        return null;
    }
    [NwFunc("GetDescription")] static object? Description(Call c) => c.Obj(0)?.Description ?? "";
    [NwFunc("SetDescription")] static object? SetDescription(Call c) { if (c.Obj(0) is { } o) o.Description = c.S(1); return null; }
    [NwFunc("GetPlotFlag")] static object? Plot(Call c) => c.Obj(0)?.Plot == true ? 1 : 0;
    [NwFunc("SetPlotFlag")] static object? SetPlot(Call c) { if (c.Obj(0) is { } o) o.Plot = c.I(1) != 0; return null; }

    [NwFunc("GetModule")] static object? Module(Call c) => c.World.Module.Id;
    [NwFunc("GetArea")] static object? Area(Call c) => c.Obj(0) switch
    {
        Area a => a.Id,
        { Area: { } a } => a.Id,
        _ => GameObject.InvalidId,
    };
    [NwFunc("GetPosition")] static object? Position(Call c) => c.Obj(0)?.Position ?? Vector3.Zero;
    /// <summary>
    /// Whole degrees from the facing direction, 0..360: a facing just below 0 reads 360 (the location keeps the exact facing).
    /// </summary>
    [NwFunc("GetFacing")] static object? Facing(Call c)
    {
        if (c.Obj(0) is not { } o) return 0f;
        float x = MathF.Cos(o.Facing + o.BearingOffset), y = MathF.Sin(o.Facing + o.BearingOffset);
        double a = Math.Atan2(y, x) * (180 / Math.PI);
        return MathF.Round((float)(y < 0 ? a + 360 : a), MidpointRounding.AwayFromZero);
    }
    /// <summary>Radians as a float product, as the original converts (so SetFacing(360.0) faces just past east).</summary>
    [NwFunc("SetFacing")] static object? SetFacing(Call c)
    {
        if (c.ObjOrSelf(1) is { } o and not (Areas.Area or Objects.Module)) { o.Facing = c.F(0) * (float)(Math.PI / 180) - o.BearingOffset; c.World.MarkChanged(o); }
        return null;
    }
    [NwFunc("SetFacingPoint")] static object? SetFacingPoint(Call c)
    {
        if (c.ObjOrSelf(1) is { } o and not (Areas.Area or Objects.Module))
        {
            var d = c.V(0) - o.Position;
            if (d.X != 0 || d.Y != 0) { o.FacingDegrees = MathF.Atan2(d.Y, d.X) * 180f / MathF.PI; c.World.MarkChanged(o); }
        }
        return null;
    }
    [NwFunc("GetLocation")] static object? GetLocation(Call c) => c.Obj(0)?.Location ?? Location.Invalid;
    [NwFunc("Location")] static object? MakeLocation(Call c) => new Location(c.O(0), c.V(1), CoreFunctions.NormalizeFacing(c.F(2)));
    [NwFunc("GetPositionFromLocation")] static object? PosFromLoc(Call c) => c.L(0).Position;
    [NwFunc("GetAreaFromLocation")] static object? AreaFromLoc(Call c) => c.World.Get(c.L(0).Area) is Area a ? a.Id : GameObject.InvalidId;
    [NwFunc("GetFacingFromLocation")] static object? FacingFromLoc(Call c) => c.L(0).FacingDegrees;

    /// <summary>3D distance, even across areas; 0 for invalid objects. The module and areas sit at the origin.</summary>
    [NwFunc("GetDistanceBetween")] static object? Distance(Call c) =>
        c.Obj(0) is { } a && c.Obj(1) is { } b ? Vector3.Distance(a.Position, b.Position) : 0f;
    /// <summary>-1 unless both are objects in the same area (areas and the module never are).</summary>
    [NwFunc("GetDistanceToObject")] static object? DistanceTo(Call c) =>
        c.Obj(0) is { Area: { } a } o && c.ObjOrSelf(1) is { Area: { } b } s && a == b ? Vector3.Distance(o.Position, s.Position) : -1f;
    /// <summary>3D distance; -1 when the areas differ (two invalid areas count as the same).</summary>
    [NwFunc("GetDistanceBetweenLocations")] static object? DistanceLoc(Call c) =>
        c.L(0).Area == c.L(1).Area ? Vector3.Distance(c.L(0).Position, c.L(1).Position) : -1f;
    [NwFunc("GetDistanceToObject2D")] static object? DistanceTo2D(Call c) =>
        c.Obj(0) is { } o && c.Self is { } s && o.Area == s.Area ? Vector2.Distance(new(o.Position.X, o.Position.Y), new(s.Position.X, s.Position.Y)) : -1f;

    /// <summary>Objects a script can find by tag: exact case, newest first; the module is not among them.</summary>
    static IEnumerable<GameObject> TagLookup(Call c, string tag) =>
        c.World.ByTag(tag).Where(o => o.Tag == tag && o is not Objects.Module).OrderByDescending(o => o.Id);

    [NwFunc("GetObjectByTag")] static object? ByTag(Call c)
    {
        int nth = c.I(1);
        return nth >= 0 ? TagLookup(c, c.S(0)).ElementAtOrDefault(nth)?.Id ?? GameObject.InvalidId : GameObject.InvalidId;
    }
    [NwFunc("GetWaypointByTag")] static object? WaypointByTag(Call c) =>
        TagLookup(c, c.S(0)).OfType<Waypoint>().FirstOrDefault()?.Id ?? GameObject.InvalidId;
    [NwFunc("GetObjectByTagInArea")] static object? ByTagInArea(Call c)
    {
        var area = c.Self?.Area;
        var list = c.World.ByTag(c.S(0)).Where(o => o.Area == area).ToList();
        int nth = c.I(1);
        return nth >= 0 && nth < list.Count ? list[nth].Id : GameObject.InvalidId;
    }

    /// <summary>
    /// Whether an object passes an OBJECT_TYPE_* filter. Sound objects pass OBJECT_TYPE_ALL only: shapes and
    /// nearest-object searches find them then, never under a single type (audio_scripts).
    /// </summary>
    static bool Matches(GameObject o, int filter) => o is Sound ? (filter & TypeAll) == TypeAll : (NwType(o) & filter) != 0;

    /// <summary>
    /// The order the original keeps an area's objects in, which iteration and shapes follow:
    /// by x, and the newest first among equal x.
    /// </summary>
    public static List<GameObject> AreaOrder(Area area) =>
        area.Objects.Where(o => !o.IsDestroyed).OrderBy(o => o.Position.X).ThenByDescending(o => o.Id).ToList();

    /// <summary>Nth (1-based; less than 1 means 1) nearest object to a point in an area by 3D distance, the newest first on ties.</summary>
    static uint Nearest(Area? area, Vector3 from, GameObject? exclude, int nth, Func<GameObject, bool> filter)
    {
        if (area == null) return GameObject.InvalidId;
        var found = area.Objects.Where(o => o != exclude && !o.IsDestroyed && filter(o))
            .OrderBy(o => Vector3.DistanceSquared(o.Position, from)).ThenByDescending(o => o.Id).Skip(Math.Max(nth, 1) - 1).FirstOrDefault();
        return found?.Id ?? GameObject.InvalidId;
    }

    [NwFunc("GetNearestObject")] static object? NearestObject(Call c)
    {
        var target = c.ObjOrSelf(1);
        int filter = c.I(0, TypeAll);
        return Nearest(target?.Area, target?.Position ?? default, target, c.I(2, 1), o => Matches(o, filter));
    }
    [NwFunc("GetNearestObjectByTag")] static object? NearestByTag(Call c)
    {
        var target = c.ObjOrSelf(1);
        string tag = c.S(0);
        return Nearest(target?.Area, target?.Position ?? default, target, c.I(2, 1), o => o.Tag == tag);
    }
    [NwFunc("GetNearestObjectToLocation")] static object? NearestToLocation(Call c)
    {
        var loc = c.L(1);
        int filter = c.I(0, TypeAll);
        return Nearest(c.World.Get(loc.Area) as Area, loc.Position, null, c.I(2, 1), o => Matches(o, filter));
    }

    // Area iteration keeps one cursor per area, like the original: a position in the area's object
    // list, so GetNext with another filter carries on from wherever GetFirst stopped.
    static readonly Dictionary<(ScriptHost, uint), (List<GameObject> List, int Index)> _areaCursors = new();

    static object? AreaStep(Call c, bool first)
    {
        var area = c.Obj(0) as Area ?? c.Self?.Area;
        if (area == null) return GameObject.InvalidId;
        int filter = c.Args.Length > 1 ? c.I(1, TypeAll) : TypeAll;
        List<GameObject> list;
        int i;
        if (first) { list = AreaOrder(area); i = 0; }
        else if (_areaCursors.TryGetValue((c.Host, area.Id), out var cur)) { list = cur.List; i = cur.Index + 1; }
        else return GameObject.InvalidId;
        // Area iteration never returns sound objects, even for OBJECT_TYPE_ALL (audio_scripts).
        while (i < list.Count && (list[i].IsDestroyed || list[i] is Sound || !Matches(list[i], filter))) i++;
        _areaCursors[(c.Host, area.Id)] = (list, i);
        return i < list.Count ? list[i].Id : GameObject.InvalidId;
    }
    [NwFunc("GetFirstObjectInArea")] static object? FirstInArea(Call c) => AreaStep(c, true);
    [NwFunc("GetNextObjectInArea")] static object? NextInArea(Call c) => AreaStep(c, false);

    // Shape iteration (spheres, cubes, cones, cylinders) keeps one cursor per script.
    static readonly Dictionary<ScriptThread, (List<uint> List, int Index)> _shapeCursors = new();

    [NwFunc("GetFirstObjectInShape")] static object? FirstInShape(Call c)
    {
        // int nShape, float fSize, location lTarget, int bLineOfSight, int nObjectFilter, vector vOrigin
        var loc = c.L(2);
        var area = c.World.Get(loc.Area) as Area;
        int filter = c.I(4, 1);
        var test = ShapeTest(c.I(0), c.F(1), loc.Position, c.V(5), c.Self);
        var list = new List<uint>();
        if (area != null && test != null)
            foreach (var o in AreaOrder(area))
                if (Matches(o, filter) && test(o.Position)) list.Add(o.Id);
        _shapeCursors[c.Thread] = (list, 0);
        return list.Count > 0 ? list[0] : GameObject.InvalidId;
    }
    /// <summary>
    /// Membership tests measured on the original (all 3D, boundaries inclusive):
    /// sphere: distance within fSize (fSize must be positive; unknown shapes are spheres);
    /// cube: each coordinate within the target plus or minus fSize;
    /// spell cylinder: from vOrigin towards the target, within fSize of the origin, at most 1.5m off the axis, not behind;
    /// spell cone: from the caller towards the target, a 60 degree cone fSize long (vOrigin is ignored);
    /// cone: from vOrigin, as wide as fSize at the target and no further in x than fSize past the ends.
    /// </summary>
    static Func<Vector3, bool>? ShapeTest(int shape, float size, Vector3 target, Vector3 origin, GameObject? self)
    {
        switch (shape)
        {
            case 0: // SHAPE_SPELLCYLINDER
            {
                var axis = target - origin;
                axis = axis == Vector3.Zero ? Vector3.UnitX : Vector3.Normalize(axis); // no direction: east
                return p =>
                {
                    var d = p - origin;
                    float along = Vector3.Dot(d, axis);
                    return along >= 0 && d.Length() <= size && (d - along * axis).Length() <= 1.5f;
                };
            }
            case 1: // SHAPE_CONE
            {
                var axis = target - origin;
                float length = axis.Length();
                if (length == 0) return null;
                axis /= length;
                float cos = MathF.Cos(MathF.Atan(size / length));
                float minX = MathF.Min(origin.X, target.X) - size, maxX = MathF.Max(origin.X, target.X) + size;
                return p =>
                {
                    var d = p - origin;
                    float dist = d.Length();
                    return p.X >= minX && p.X <= maxX && (dist == 0 || Vector3.Dot(d, axis) > 0 && Vector3.Dot(d, axis) >= dist * cos);
                };
            }
            case 2: // SHAPE_CUBE
            {
                Vector3 min = target - new Vector3(size), max = target + new Vector3(size);
                return p => p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y && p.Z >= min.Z && p.Z <= max.Z;
            }
            case 3: // SHAPE_SPELLCONE
            {
                if (self?.Area == null) return null;
                var apex = self.Position;
                var axis = target - apex;
                if (axis == Vector3.Zero) return null;
                axis = Vector3.Normalize(axis);
                float cos30 = MathF.Cos(MathF.PI / 6);
                return p =>
                {
                    var d = p - apex;
                    float along = Vector3.Dot(d, axis);
                    return along > 0 && along <= size && along >= d.Length() * cos30;
                };
            }
            default: // SHAPE_SPHERE
                if (size <= 0) return null;
                return p => Vector3.Distance(p, target) <= size;
        }
    }

    [NwFunc("GetNextObjectInShape")] static object? NextInShape(Call c)
    {
        if (!_shapeCursors.TryGetValue(c.Thread, out var cur)) return GameObject.InvalidId;
        int i = cur.Index + 1;
        _shapeCursors[c.Thread] = (cur.List, i);
        return i < cur.List.Count ? cur.List[i] : GameObject.InvalidId;
    }

    [NwFunc("GetFirstPC")] static object? FirstPC(Call c)
    {
        var pcs = Pcs(c);
        _pcCursor[c.Host] = 0;
        return pcs.Count > 0 ? pcs[0].Id : GameObject.InvalidId;
    }
    [NwFunc("GetNextPC")] static object? NextPC(Call c)
    {
        var pcs = Pcs(c);
        int i = _pcCursor.GetValueOrDefault(c.Host) + 1;
        _pcCursor[c.Host] = i;
        return i < pcs.Count ? pcs[i].Id : GameObject.InvalidId;
    }
    static readonly Dictionary<ScriptHost, int> _pcCursor = new();
    static List<Creature> Pcs(Call c) => Players.FamiliarPossession.Players(c.World).ToList();

    [NwFunc("GetIsPC")] static object? IsPC(Call c) => c.Obj<Creature>(0)?.IsPC == true ? 1 : 0;
    // A DM avatar is a DM; a creature it possesses is not (GetIsDMPossessed), though both are PCs.
    [NwFunc("GetIsDM")] static object? IsDM(Call c) => c.Obj<Creature>(0)?.IsDMAvatar == true ? 1 : 0;
    [NwFunc("GetIsDMPossessed")] static object? IsDMPossessed(Call c) => c.Obj<Creature>(0)?.IsDMPossessed == true ? 1 : 0;
    [NwFunc("GetIsPossessedFamiliar")] static object? IsPossessedFamiliar(Call c) => c.Obj<Creature>(0)?.Player?.FamiliarMaster != null ? 1 : 0;
    [NwFunc("GetPCPlayerName")] static object? PlayerName(Call c) => Session(c)?.PlayerName ?? "";
    [NwFunc("GetPCPublicCDKey")] static object? CdKey(Call c) => Session(c)?.PublicKey ?? "";
    [NwFunc("GetPCIPAddress")] static object? Ip(Call c) => Session(c)?.Address ?? "";
    // The connection behind a character, also while its player controls the familiar.
    static PlayerSession? Session(Call c) => c.Obj<Creature>(0) is { } pc ? Players.FamiliarPossession.SessionOf(pc) : null;

    [NwFunc("DestroyObject")] static object? Destroy(Call c)
    {
        var o = c.Obj(0);
        if (o == null || o is Area || o is Module) return null;
        // Queued even without a delay: the object stays valid until the outermost running script has finished,
        // and commands assigned to it before this still run. SetIsDestroyable(FALSE) creatures stay.
        // A door with a transition stays and opens instead (DoorService.KeepOnDestroy).
        c.World.After(c.F(1), () =>
        {
            if (o is Creature { IsDestroyable: false } or Creature { Player.FamiliarMaster: not null }) return;
            if (o is Door d && WorldObjects.DoorService.KeepOnDestroy(c.World, d)) return;
            c.World.Destroy(o);
        });
        return null;
    }

    [NwFunc("CreateObject")] static object? Create(Call c)
    {
        // int nObjectType, string sTemplate, location lLocation, int bUseAppearAnimation, string sNewTag
        int type = c.I(0);
        var loc = c.L(2);
        if (c.World.Get(loc.Area) is not Area area) return GameObject.InvalidId;
        var o = Spawn(c.World, type, c.S(1), area, loc.Position, loc.FacingDegrees * MathF.PI / 180f);
        if (o == null) return GameObject.InvalidId;
        // A new tag is cleaned like SetTag's, except a waypoint's.
        if (c.S(4).Length > 0) c.World.SetTag(o, o is Waypoint ? c.S(4) : CleanTag(c.S(4)));
        return o.Id;
    }

    /// <summary>Creates an object from a blueprint at a position (CreateObject and encounters).</summary>
    public static GameObject? Spawn(World world, int type, string resref, Area area, Vector3 pos, float facing)
    {
        GameObject? o = null;
        switch (type)
        {
            case 1: // creature
            {
                var cr = world.Creatures.FromBlueprint(resref);
                if (cr == null) return null;
                cr.IdleStance = false; // made while the module runs: the combat-ready stance until it first stops
                if (area.Walkmesh.GroundHeight(pos.X, pos.Y, pos.Z, 2f) is { } z) pos.Z = z;
                world.Inventory.FireSpawnAcquires(cr);
                world.AddToArea(cr, area, pos, facing);
                world.QueueSpawn(cr);
                return cr;
            }
            case 64: // placeable
            {
                var utp = world.Resources.ReadGff(resref, ResType.Utp);
                if (utp == null || world.Loader == null) return null;
                var p = world.Loader.CreatePlaceable(area, utp, pos, GameObject.BearingFor(facing));
                p.ResRef = resref;
                world.AddToArea(p, area, pos, GameObject.BearingFor(facing));
                return p;
            }
            case 2: // item on the ground
            {
                var item = world.Items.FromBlueprint(resref);
                if (item == null) return null;
                world.AddToArea(item, area, pos, facing);
                return item;
            }
            case 32: // waypoint
            {
                var utw = world.Resources.ReadGff(resref, ResType.Utw);
                o = new Waypoint { ResRef = resref, Tag = utw?.GetString("Tag") ?? resref, Name = utw?.GetLocString("LocalizedName") ?? new LocString() };
                break;
            }
            case 128: // store
            {
                var utm = world.Resources.ReadGff(resref, ResType.Utm);
                if (utm == null) return null;
                o = new Store { ResRef = resref, Tag = utm.GetString("Tag"), Name = utm.GetLocString("LocName"), Template = utm };
                break;
            }
            default:
                return null;
        }
        world.AddToArea(o, area, pos, facing);
        return o;
    }
}
