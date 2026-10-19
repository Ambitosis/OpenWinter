using System.Numerics;
using OpenWinter.Aurora.Models;
using OpenWinter.Content;
using Xunit;

namespace OpenWinter.Tests;

public class TileFadeTests
{
    [Fact]
    public void RolesFollowTheTileFadeValue()
    {
        Assert.Equal(0, TileFade.Role(0));
        Assert.Equal(TileFade.Fade, TileFade.Role(1));
        Assert.Equal(TileFade.Base, TileFade.Role(2));
        Assert.Equal(TileFade.Neighbour, TileFade.Role(4));
    }

    [Fact]
    public void NeighbourPlugsFindTheirEdgeAndTurnWithTheTile()
    {
        // A plug standing in the plane x = -5 (west edge) and one in y = +5 (north edge).
        Assert.Equal(2, TileFade.EdgeOf(new Vector3(-5f, 0.8f, 3f), new Vector3(-5f, 3f, 4.3f)));
        Assert.Equal(1, TileFade.EdgeOf(new Vector3(-5f, 4.9f, 3f), new Vector3(1.5f, 5.2f, 22.5f)));
        Assert.Equal(-1, TileFade.EdgeOf(new Vector3(-1f, -1f, 3f), new Vector3(1f, 1f, 4f)));
        // A quarter turn counter-clockwise takes west to south.
        Assert.Equal(3, TileFade.RotateEdge(2, 1));
        Assert.Equal(0, TileFade.RotateEdge(1, 3));
        Assert.Equal((0, -1), TileFade.Offset(3));
    }

    static TileFade.Footprint?[] WholeTiles(int width, int height, float top) =>
        Enumerable.Range(0, width * height).Select(i => (TileFade.Footprint?)new TileFade.Footprint(
            new Vector2(i % width * 10f, i / width * 10f), new Vector2(i % width * 10f + 10f, i / width * 10f + 10f), top)).ToArray();

    [Fact]
    public void FadesTilesBetweenFocusAndCameraOnly()
    {
        var tiles = WholeTiles(5, 5, 6f);
        // Focus in the middle tile, camera two tiles south and high above.
        var faded = TileFade.TilesToFade(5, 5, new Vector3(25, 25, 0), new Vector3(25, 5, 12), tiles);
        Assert.Contains(2 * 5 + 2, faded);      // the focus tile
        Assert.Contains(1 * 5 + 2, faded);      // the tile south of it, under the line of sight
        Assert.DoesNotContain(3 * 5 + 2, faded); // behind the focus
        Assert.DoesNotContain(2 * 5 + 4, faded); // off to the side
        // Where the line of sight has climbed above the geometry it no longer fades anything.
        Assert.DoesNotContain(0 * 5 + 2, faded);
    }

    [Fact]
    public void TilesWithoutFadingGeometryOrBelowTheSightlineStay()
    {
        var tiles = new TileFade.Footprint?[9];
        tiles[4] = new TileFade.Footprint(new Vector2(12, 12), new Vector2(14, 14), 6f);
        // The focus is in tile 4 but nowhere near its fading geometry, which isn't between it and the camera.
        Assert.Empty(TileFade.TilesToFade(3, 3, new Vector3(18, 18, 0), new Vector3(18, 28, 8), tiles));
        Assert.Single(TileFade.TilesToFade(3, 3, new Vector3(18, 18, 0), new Vector3(8, 8, 8), tiles));
    }

    [DataFact]
    public void MergedRenderMeshesDrawTheirWholeIndexList()
    {
        // The barracks bed tile keeps most of its merged wall/floor/bed-frame triangles only in
        // the mesh's vertex index list; the face array holds a fraction of them.
        var tile = new ModelCache(TestData.Module).Get("tic01_e01_01")!;
        int Triangles(string node) => tile.Parts.First(p => p.Name.Equals(node, StringComparison.OrdinalIgnoreCase)).Mesh!.Indices.Length / 3;
        Assert.Equal(209, Triangles("Line43"));   // bed frames
        Assert.Equal(32, Triangles("Shape16"));   // walls
        Assert.Equal(36, Triangles("Box5568"));   // an ordinary mesh: faces and list agree
        foreach (var mesh in tile.Parts.Select(p => p.Mesh).OfType<MeshData>())
            Assert.All(mesh.Indices, i => Assert.InRange(i, 0, mesh.Vertices.Length - 1));
        var walk = tile.Parts.Select(p => p.Mesh).OfType<MeshData>().First(m => m.IsWalkmesh);
        Assert.Equal(walk.FaceSurfaces.Length * 3, walk.Indices.Length);
    }

    [DataFact]
    public void TileFadeValuesAreKept()
    {
        var tile = new ModelCache(TestData.Module).Get("tic01_j08_01")!;
        var modes = tile.Parts.Where(p => p.Mesh != null).Select(p => p.TileFadeMode).Distinct().OrderBy(m => m).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 4 }, modes);
    }
}
