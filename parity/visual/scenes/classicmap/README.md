# Classic map sizing controls

Authored square2, square4, rectangle8x3 and large16 areas. The optional width/height
fields preserve `size` fallback and clamp each dimension to 1..32; Tile_List always
contains exactly width*height entries. All tiles use installed ttr01 tile 109.

The initial native frame must establish actual header positions before interpreting
the two zoom-in and one zoom-out attempts. The final L-shaped **exploration** mask
is a fog/content-bounds discriminator, not an L-shaped geometry fixture; actual
irregular-area contours remain a separate reference control. Area dimensions are
logged by the original public API. Reject missing/black frames and unsuccessful
input controls. All four framebuffer variants are accepted for geometry; scrolling limits remain.

The companion classicpanels scene checks opening/closing order and conversation
panel dismissal.

Before the L mask, SetCreatureExploresMinimap(FALSE) disables automatic reveal.
At the shot, GetTileExplored reads every cell: require auto=0, mismatches=0 and
count=width+height-1 (small3, square7, rectangle10, large31).

With the serialized monitor lease, from the repository root:

```powershell
python tools/visual/visual.py run classicmap --only nwn --native-framebuffer --tag _layout1
python tools/visual/visual.py run classicmap --only openwinter --openwinter-args "--ui classic" --tag _layout1
python tools/visual/visual.py compare classicmap --tag _layout1
python tools/visual/visual.py run classicpanels --only nwn --native-framebuffer --tag _layout1
python tools/visual/visual.py run classicmappin --only nwn --native-framebuffer --tag _layout1
```

The existing CLI runs all four classicmap variants in scene.json order (small,
square, rectangle, large); it has no single-variant selector. Separate outputs are
`out/visual/shots/vis_classicmap_<variant>_layout1/{nwn,openwinter}`. Inspect the first
native default frame before accepting subsequent candidate zoom coordinates.
