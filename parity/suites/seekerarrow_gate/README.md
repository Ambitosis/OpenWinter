# Seeker Arrow dispatch preflight

Run `parity-run seekerarrow_gate --only nwn` before visual interpretation.
This uses public `ActionUseFeat(451)` on an equipped, authored AA4 NPC and leaves
the real stock impact script unchanged. Require feat/use/equipment/perception
gates, event 601 with expected caster, one event, consumed feat use and HP
loss. Random HP magnitude is only an unparsed diagnostic. Action completion
precedes the travelling arrow's stock event.

The corresponding replacement-clip/ordinary-bow visual control and measured
limitations are documented in `parity/visual/scenes/seekerarrow/README.md`.
