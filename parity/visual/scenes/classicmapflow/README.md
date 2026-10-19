# Client panel and personal-pin flow

A quiet authored module for --capture-classic-map-flow. It reveals the area and
has no scheduled input, so the client capture owns its own UI actions. Build via
`dotnet run --project src/OpenWinter.Tools -c Release -- visual-build classicmapflow`.
Run each emitted module through tools/godot.sh with --ui classic, --character
aluviandarkstar and --capture-classic-map-flow ../out/classic-map-flow-square
(or rectangle). Inspect all seven PNGs and require the COMPLETE checks line;
exceptions are logged before the client exits. This exercises authenticated pin
requests and real GUI button handlers, not native keyboard/text equivalence.
Actual conversation frames use classicpaneltalk/classicpaneltype instead.
