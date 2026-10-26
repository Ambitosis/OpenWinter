using System.Numerics;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Objects
{
    public sealed partial class Creature
    {
        /// <summary>The appearance the cached personal space radii were read for.</summary>
        internal int SpaceAppearance = -1;
        /// <summary>appearance.2da PERSPACE and CREPERSPACE for <see cref="SpaceAppearance"/>.</summary>
        internal float SpaceRadius, SpaceCreature;
        /// <summary>The creature that stopped this creature's scripted movement this step (consumed by the move action).</summary>
        public uint MoveBlockedBy = InvalidId;
        /// <summary>The current move may walk into a closed door and stop there (scripted moves); others plan round doors.</summary>
        public bool MovePassClosedDoors;
        /// <summary>Game time this creature's movement was last blocked by a creature (negative: never).</summary>
        public double LastBlockedAt = -1;
        /// <summary>Its step was stopped by a player it doesn't push: the move plans its path again round her.</summary>
        public bool MoveReplan;
    }
}

namespace OpenWinter.Game
{
    /// <summary>
    /// Creatures keep apart, measured on the original (parity suites coll_place and coll_move):
    /// <list type="bullet">
    /// <item>Two creatures stand at least the sum of their appearance.2da CREPERSPACE apart.</item>
    /// <item>A creature created or jumping onto a spot that is taken, or that it doesn't fit (off the walkmesh, too near a
    /// wall or the area's edge), goes to the first free spot of a search around it: square rings of points PERSPACE apart,
    /// each ring in groups (m, k) rotated a quarter turn clockwise four times, then mirrored (-m, k) likewise, m from 0
    /// to k. It fits where a box from PERSPACE west to twice PERSPACE east and PERSPACE north and south of it is all
    /// walkable, and it is more than PERSPACE inside the area's west and east edges.</item>
    /// <item>Moving creatures only keep clear of creatures they perceive. Walking into an idle friend (no actions) pushes it
    /// aside at once: sideways off the mover's line to the personal space distance plus 0.1 m, or straight on by that much
    /// when it stands right on the line (else diagonally forward right, forward left, right, left). Walking into a busy or
    /// moving one stops the mover: its move fails and OnBlocked runs with GetBlockingDoor the blocker. An idle one with
    /// nowhere to go (hemmed in by busy creatures) is walked through.</item>
    /// <item>The blocker must consider the mover friendly; neutral and hostile creatures do not yield.
    /// Direct player steering can also push stationary friends with queued actions (measured with ActionWait).</item>
    /// <item>Paths are planned around busy or non-friendly creatures within 10 m, also when one stands beside the
    /// destination (coll_goal). A destination inside such a creature's personal space moves straight out of it to
    /// the personal space distance; an object's stopping point that one takes moves round the object, to the
    /// mover's left first.</item>
    /// <item>Creatures with EffectCutsceneGhost neither bump nor are bumped.</item>
    /// <item>Players (measured on nwmain, scene summondoor; the suites above had none): a player's summon walking
    /// towards her went round her and kept going, and she walked straight on through the doorway; an idle summon on
    /// her line was pushed aside. So creatures nobody plays never push a player but plan round her even when she is
    /// idle, and a player's path doesn't go round her own associates. A standing associate in her way is pushed aside
    /// even when busy (inferred: the original's associates made way in every reading; a busy one left in a doorway
    /// would stop her click for good).</item>
    /// </list>
    /// </summary>
    public static class PersonalSpace
    {
        /// <summary>EFFECT_TYPE_CUTSCENEGHOST.</summary>
        public const int EffectCutsceneGhost = 83;
        /// <summary>A pushed creature ends up this much further than personal space from the mover's line.</summary>
        public const float PushMargin = 0.1f;
        /// <summary>Busy creatures this near are path obstacles.</summary>
        public const float PlanRange = 10f;
        /// <summary>Path obstacles are the two personal spaces plus this much (a hair, so routes don't graze a bump).</summary>
        public const float PlanMargin = 0.02f;
        /// <summary>The largest CREPERSPACE in the base appearance.2da (bounds neighbour queries).</summary>
        const float MaxCreatureSpace = 2.5f;
        /// <summary>Extra clearance from walls to a creature's west (measured: more than half a centimetre).</summary>
        const float WestMargin = 0.01f;
        /// <summary>Rings searched for a free spot before giving up and keeping the requested one.</summary>
        const int MaxRings = 30;

        [ThreadStatic] static List<Creature>? _near;
        static List<Creature> NearBuffer() => _near ??= new List<Creature>(32);

        static void Radii(World world, Creature c)
        {
            if (c.SpaceAppearance == c.AppearanceType) return;
            var t = world.Resources.TwoDA("appearance");
            c.SpaceRadius = t.GetFloat(c.AppearanceType, "PERSPACE", 0.3f);
            c.SpaceCreature = t.GetFloat(c.AppearanceType, "CREPERSPACE", 0.5f);
            c.SpaceAppearance = c.AppearanceType;
        }

        /// <summary>PERSPACE: the creature's own size against walls, and the step of the free spot search.</summary>
        public static float Radius(World world, Creature c)
        {
            if (c.Overrides?.PersonalSpace is float set) return set;
            Radii(world, c);
            return c.SpaceRadius;
        }

        /// <summary>CREPERSPACE: how far other creatures keep from this one (the two values add up).</summary>
        public static float CreatureSpace(World world, Creature c)
        {
            if (c.Overrides?.CreaturePersonalSpace is float set) return set;
            Radii(world, c);
            return c.SpaceCreature;
        }

        public static bool IsGhost(Creature c)
        {
            foreach (var e in c.Effects) if (e.Type == EffectCutsceneGhost) return true;
            return false;
        }

        /// <summary>
        /// No actions and not steered, or just stopped by a block (its move is about to fail): such a creature gets
        /// pushed out of the way.
        /// </summary>
        public static bool IsIdle(Creature c) => (c.Actions.Count == 0 || c.MoveBlockedBy != GameObject.InvalidId) && c.MoveDirection == Vector3.Zero;

        // coll_bump_flags: the blocker must consider the mover friendly (the reverse is insufficient).
        // pavelbump: player steering can also displace a stationary friend with a queued wait.
        static bool Yields(World world, Creature mover, Creature other) => world.Factions.IsFriend(other, mover)
            && !(other.IsPC && !mover.IsPC)
            && (IsIdle(other) || mover.IsPC && (mover.MoveGoal == null || other.Master == mover.Id && other.MoveGoal == null)
                && other.MoveDirection == Vector3.Zero);

        /// <summary>Whether <paramref name="c"/> collides with <paramref name="other"/> (it has to perceive it).</summary>
        static bool Bumps(Creature c, Creature other) =>
            other != c && !other.IsDestroyed && !other.IsDead && other.Area == c.Area
            && (c.Society.Seen.Contains(other.Id) || c.Society.Heard.Contains(other.Id)) && !IsGhost(other);

        // ---- Free spots -------------------------------------------------------------------------------------

        /// <summary>
        /// Where a creature placed at <paramref name="p"/> ends up (CreateObject, jumps, arriving in an area):
        /// the requested point if it is free, else the first free point of the ring search, with the ground height.
        /// </summary>
        public static Vector3 FindSpot(World world, Area area, Creature c, Vector3 p)
        {
            float step = Math.Max(0.01f, Radius(world, c));
            float z = Ground(area, p.X, p.Y, p.Z) ?? p.Z;
            if (Free(world, area, c, p.X, p.Y, z, requested: true)) return new Vector3(p.X, p.Y, z);
            // From a point on walkable ground, only spots it can walk to in a straight line count.
            bool onGround = area.Walkmesh.GroundHeight(p.X, p.Y, z, 1f) != null;
            var start = new Vector3(p.X, p.Y, z);
            for (int k = 1; k <= MaxRings; k++)
                for (int m = 0; m <= k; m++)
                {
                    // (m, k) and its three quarter turns clockwise, then (-m, k) likewise.
                    int turns = m == 0 || m == k ? 1 : 2;
                    for (int mirror = 0; mirror < turns; mirror++)
                    {
                        int i = mirror == 0 ? m : -m, j = k;
                        for (int r = 0; r < 4; r++)
                        {
                            // The offset in single precision, the point in double: this matches the original on
                            // points exactly personal space away from another creature.
                            double x = p.X + (double)(float)(i * step), y = p.Y + (double)(float)(j * step);
                            if (Ground(area, (float)x, (float)y, p.Z) is { } gz && Free(world, area, c, x, y, gz)
                                && (!onGround || area.Walkmesh.Trace(start, new Vector3((float)x, (float)y, gz), 1f) >= 0.999f))
                                return new Vector3((float)x, (float)y, gz);
                            (i, j) = (j, -i);
                        }
                    }
                }
            return new Vector3(p.X, p.Y, z);
        }

        static float? Ground(Area area, float x, float y, float nearZ) =>
            area.Walkmesh.GroundHeight(x, y, nearZ, 2f) ?? area.Walkmesh.HighestGround(x, y);

        /// <summary>
        /// A creature fits at (x, y): inside the area, on walkable ground clear of walls, apart from other creatures.
        /// The point asked for must be clear of the west edge by more than PERSPACE; search points may touch it.
        /// </summary>
        public static bool Free(World world, Area area, Creature c, double x, double y, float z, Creature? ignore = null, bool requested = false)
        {
            float r = Radius(world, c);
            float width = area.Width * Area.TileSize, height = area.Height * Area.TileSize;
            if ((requested ? x - r <= 1e-5 : x - r < -1e-5) || x + r > width + 1e-5 || y < 0 || y >= height) return false;
            if (Crowded(world, area, c, x, y, ignore)) return false;
            return Fits(area, (float)x, (float)y, z, r);
        }

        /// <summary>Another creature (not <paramref name="ignore"/>) is within personal space of (x, y).</summary>
        static bool Crowded(World world, Area area, Creature c, double x, double y, Creature? ignore)
        {
            float own = CreatureSpace(world, c);
            var near = NearBuffer();
            area.CreatureGrid.Near(new Vector3((float)x, (float)y, 0), own + MaxCreatureSpace, near);
            foreach (var o in near)
            {
                if (o == c || o == ignore || o.IsDestroyed || o.Area != area || o.IsDead) continue;
                double d = own + CreatureSpace(world, o);
                double dx = o.Position.X - x, dy = o.Position.Y - y;
                if (dx * dx + dy * dy < d * d) return true;
            }
            return false;
        }

        /// <summary>
        /// The ground from <paramref name="r"/> (and a centimetre) west to 2 <paramref name="r"/> east and
        /// <paramref name="r"/> north and south of (x, y) is walkable at about height z. As measured on the original: it
        /// keeps creatures twice as far from walls to their east, only looks at the row of tiles the point is in (a wall
        /// in the tile to the north or south doesn't count, one to the east or west does), and a placeable only counts
        /// under the point itself (doors, open or shut, count all round).
        /// </summary>
        public static bool Fits(Area area, float x, float y, float z, float r)
        {
            var wm = area.Walkmesh;
            float ty = MathF.Floor(y / Area.TileSize) * Area.TileSize;
            float x0 = x - r - WestMargin, x1 = x + 2 * r, y0 = y - r, y1 = y + r;
            float width = area.Width * Area.TileSize;
            // Off the area's west and east edges is the edge check's business; other tile rows aren't looked at.
            // Placeables only count under the point itself.
            bool Ok(float sx, float sy) => sx < 0 || sx > width || sy < ty || sy >= ty + Area.TileSize
                || wm.GroundHeight(sx, sy, z, 1f, noPlaceables: true) != null;
            if (wm.GroundHeight(x, y, z, 1f) == null) return false;
            // The corners first: most misses show there.
            if (!Ok(x0, y0) || !Ok(x1, y0) || !Ok(x0, y1) || !Ok(x1, y1)) return false;
            const float Edge = 0.05f, Inner = 0.1f;
            int nx = Math.Max(1, (int)MathF.Ceiling((x1 - x0) / Edge)), ny = Math.Max(1, (int)MathF.Ceiling((y1 - y0) / Edge));
            for (int i = 1; i < nx; i++)
            {
                float sx = x0 + (x1 - x0) * i / nx;
                if (!Ok(sx, y0) || !Ok(sx, y1)) return false;
            }
            for (int j = 1; j < ny; j++)
            {
                float sy = y0 + (y1 - y0) * j / ny;
                if (!Ok(x0, sy) || !Ok(x1, sy)) return false;
            }
            int ix = Math.Max(1, (int)MathF.Ceiling((x1 - x0) / Inner)), iy = Math.Max(1, (int)MathF.Ceiling((y1 - y0) / Inner));
            for (int i = 1; i < ix; i++)
                for (int j = 1; j < iy; j++)
                    if (!Ok(x0 + (x1 - x0) * i / ix, y0 + (y1 - y0) * j / iy)) return false;
            return true;
        }

        // ---- Bumping while moving ------------------------------------------------------------------------------

        /// <summary>
        /// Before a creature steps from its position to <paramref name="next"/>: pushes yielding friends it would walk into
        /// out of the way (or walks through one that can't be moved), and returns a busy or moving creature that blocks
        /// the step.
        /// </summary>
        public static Creature? Step(World world, Creature c, Vector3 next)
        {
            var area = c.Area;
            if (area == null || IsGhost(c)) return null;
            var move = new Vector2(next.X - c.Position.X, next.Y - c.Position.Y);
            float moved = move.Length();
            if (moved < 1e-6f) return null;
            var forward = move / moved;
            float own = CreatureSpace(world, c);
            var near = NearBuffer();
            area.CreatureGrid.Near(next, own + MaxCreatureSpace, near);
            // Copy: pushing moves creatures between cells.
            Creature[]? hits = null;
            int count = 0;
            foreach (var o in near)
            {
                if (!Bumps(c, o)) continue;
                float d = own + CreatureSpace(world, o);
                float ax = o.Position.X - next.X, ay = o.Position.Y - next.Y;
                float after = ax * ax + ay * ay;
                if (after >= d * d) continue;
                float bx = o.Position.X - c.Position.X, by = o.Position.Y - c.Position.Y;
                if (after >= bx * bx + by * by) continue; // moving apart (or round it): never trapped
                hits ??= new Creature[near.Count];
                hits[count++] = o;
            }
            for (int i = 0; i < count; i++)
            {
                var o = hits![i];
                // A player's own associate on the move makes way: it plans round her again and she walks on (summondoor).
                if (c.IsPC && o.Master == c.Id && o.MoveGoal != null) { o.MoveReplan = true; continue; }
                if (!Yields(world, c, o)) return o;
                // Nowhere to push it (hemmed in by busy creatures): the original lets the walker through.
                Push(world, c, next, forward, o);
            }
            return null;
        }

        /// <summary>Moves an idle creature out of a mover's way; false when there is nowhere to put it.</summary>
        static bool Push(World world, Creature mover, Vector3 moverNext, Vector2 forward, Creature o)
        {
            var area = o.Area!;
            float d = CreatureSpace(world, mover) + CreatureSpace(world, o) + PushMargin;
            var left = new Vector2(-forward.Y, forward.X);
            var rel = new Vector2(o.Position.X - mover.Position.X, o.Position.Y - mover.Position.Y);
            float lateral = Vector2.Dot(rel, left);
            var from = new Vector2(o.Position.X, o.Position.Y);
            float diagonal = d + 0.05f;
            Span<Vector2> tries = stackalloc Vector2[6];
            int n = 0;
            if (MathF.Abs(lateral) > 1e-4f && MathF.Abs(lateral) < d)
                tries[n++] = from + left * (MathF.Sign(lateral) * (d - MathF.Abs(lateral)));
            tries[n++] = from + forward * d;
            tries[n++] = from + (forward - left) * diagonal;
            tries[n++] = from + (forward + left) * diagonal;
            tries[n++] = from - left * d;
            tries[n++] = from + left * d;
            for (int i = 0; i < n; i++)
            {
                var t = tries[i];
                if (Ground(area, t.X, t.Y, o.Position.Z) is not { } z) continue;
                // The mover counts at the point it is stepping to.
                float space = CreatureSpace(world, mover) + CreatureSpace(world, o);
                if (Vector2.DistanceSquared(t, new Vector2(moverNext.X, moverNext.Y)) < space * space) continue;
                if (!Free(world, area, o, t.X, t.Y, z, ignore: mover)) continue;
                var oldPosition = o.Position;
                o.Position = new Vector3(t.X, t.Y, z);
                area.CreatureGrid.Update(o);
                // Native changes the authoritative position immediately, but presents a directional step
                // without turning the creature. An explicit event distinguishes this from a script jump.
                world.Output?.Notify("creature-bump", o, new
                {
                    from = new[] { oldPosition.X, oldPosition.Y, oldPosition.Z },
                    to = new[] { o.Position.X, o.Position.Y, o.Position.Z },
                    facing = o.Facing,
                });
                world.MarkChanged(o);
                return true;
            }
            return false;
        }

        // ---- Path planning -------------------------------------------------------------------------------------

        /// <summary>
        /// Busy or non-friendly creatures near a mover that its path should go round: circles of the two personal
        /// spaces plus <see cref="PlanMargin"/>.
        /// </summary>
        public static List<(Vector2 Centre, float Radius)>? Obstacles(World world, Creature c, GameObject? target)
        {
            var area = c.Area;
            if (area == null || IsGhost(c) || c.Society.Seen.Count + c.Society.Heard.Count == 0) return null;
            float own = CreatureSpace(world, c);
            var near = NearBuffer();
            area.CreatureGrid.Near(c.Position, PlanRange, near);
            List<(Vector2, float)>? list = null;
            foreach (var o in near)
            {
                // Planning is for scripted movement, which goes around busy or non-friendly creatures, and round players
                // (nobody pushes them); a player's own associates make way for her instead.
                if (o == target || !Bumps(c, o) || c.IsPC && o.Master == c.Id) continue;
                if (IsIdle(o) && world.Factions.IsFriend(o, c) && !(o.IsPC && !c.IsPC)) continue;
                if (Vector2.DistanceSquared(new(o.Position.X, o.Position.Y), new(c.Position.X, c.Position.Y)) > PlanRange * PlanRange) continue;
                (list ??= new()).Add((new Vector2(o.Position.X, o.Position.Y), own + CreatureSpace(world, o) + PlanMargin));
            }
            return list;
        }
    }
}
