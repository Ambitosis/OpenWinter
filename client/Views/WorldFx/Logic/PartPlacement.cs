using System.Numerics;
using OpenWinter.Content;

namespace OpenWinter.Client.Views.WorldFx.Logic;

/// <summary>Where the pieces of a part model go on a creature's skeleton.</summary>
public static class PartPlacement
{
    /// <summary>
    /// Where a skinned part model's (robe's, cloak's) rigid mesh on node <paramref name="node"/> goes: the skeleton bone
    /// (<paramref name="map"/>: the part model's nodes to skeleton bones, -1 for none) of the node or of its nearest
    /// ancestor that has one, else the root, with the node's rest transform relative to that ancestor. A robe's belt
    /// pieces on nodes the skeleton lacks ride its pelvis, as in the original (scene robenode).
    /// </summary>
    public static (int Bone, Matrix4x4 Offset) RigidPlace(ModelData part, int[] map, Matrix4x4[] globals, int node)
    {
        for (int a = node; a >= 0; a = part.Parts[a].Parent)
        {
            if (map[a] < 0) continue;
            if (a == node) return (map[a], Matrix4x4.Identity);
            return Matrix4x4.Invert(globals[a], out var inv) ? (map[a], globals[node] * inv) : (map[a], Matrix4x4.Identity);
        }
        return (0, globals[node]);
    }
}
