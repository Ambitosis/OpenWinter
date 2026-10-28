using System.Numerics;

namespace OpenWinter.Client.Gameplay;

/// <summary>A trigger's polygon seen from above: whether a point is inside it, and its triangles.</summary>
public static class TriggerPolygon
{
    public static bool Contains(IReadOnlyList<Vector2> poly, Vector2 q)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if ((poly[i].Y > q.Y) != (poly[j].Y > q.Y)
                && q.X < (poly[j].X - poly[i].X) * (q.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                inside = !inside;
        return inside;
    }

    static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    /// <summary>Ear clipping of a simple polygon, either winding.</summary>
    public static List<(Vector2 A, Vector2 B, Vector2 C)> Triangulate(IReadOnlyList<Vector2> polygon)
    {
        var result = new List<(Vector2, Vector2, Vector2)>();
        if (polygon.Count < 3) return result;
        var idx = Enumerable.Range(0, polygon.Count).ToList();
        float area = 0;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++) area += Cross(polygon[j], polygon[i]);
        if (area < 0) idx.Reverse();
        while (idx.Count > 3)
        {
            bool clipped = false;
            for (int i = 0; i < idx.Count && !clipped; i++)
            {
                int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                Vector2 a = polygon[ia], b = polygon[ib], c = polygon[ic];
                if (Cross(b - a, c - b) <= 0) continue;
                bool ear = true;
                foreach (int k in idx)
                {
                    if (k == ia || k == ib || k == ic) continue;
                    var p = polygon[k];
                    if (Cross(b - a, p - a) >= 0 && Cross(c - b, p - b) >= 0 && Cross(a - c, p - c) >= 0) { ear = false; break; }
                }
                if (!ear) continue;
                result.Add((a, b, c));
                idx.RemoveAt(i);
                clipped = true;
            }
            // Self-intersecting or degenerate outlines: stop rather than loop.
            if (!clipped) return result;
        }
        result.Add((polygon[idx[0]], polygon[idx[1]], polygon[idx[2]]));
        return result;
    }
}
