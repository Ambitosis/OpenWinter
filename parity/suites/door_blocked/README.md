# Closed-door movement and OnBlocked

Authored native dedicated-server control, using the sandbox parity harness. `parity-run door_blocked` reports **45 matched, 0 different,
0 missing**. Final segment-collision refinement was rerun on OpenWinter against
those retained native readings with the original source tolerances: still 45/45.

Four quiet goblins start at exact authored coordinates and walk across matched
installed metal doors. Closed unlocked, closed locked, and hook-recovery cases
fire one OnBlocked each. GetBlockingDoor identifies the correct door and
GetCurrentAction is 65535 inside the callback (the move has left the queue).
The following ActionDoCommand runs next. Closed actors stop before their slabs.
The recovery handler queues ActionOpenDoor then a new move, opens the door and
reaches the destination. The already-open control has zero callbacks and reaches
its destination. Callback/queue times are compared within 0.2/0.25 s; positions
within 0.15 m, preserving the tolerances authored before the comparison.

Implementation marks only the closed DWK slab owner. A separate movement path
cache permits planning through that slab while physical collision remains in
force; ordinary path queries are unchanged. Scripted movement detects a closed
slab across the attempted segment (including steps longer than a thin slab), then
uses the existing next-tick blocked-action path. ReportBlocked now has an actual
movement caller. Open leaves, walls and placeables remain obstacles. There is no
automatic door opening: the creature's module script decides what to do.

Scope: only ActionMoveToLocation and ActionMoveToObject moves (`MoveAction.PassClosedDoors`) use the door-passing
planner and stop against a closed slab. Every other move (player clicks and approaches, combat, conversation, item and
spell approaches, follow, random walk, force moves) plans round closed doors as before and never raises OnBlocked
for a door; with a gap elsewhere in the wall it walks through the gap (`OrdinaryMovesGoRoundAClosedDoorThroughTheGap`).
The native controls had no alternate route, so a scripted move's choice between a closed door and a longer open route
is not measured.

Tests cover callback identity/order/single delivery, scripted opening/retry,
open/wall negative controls, cache invalidation and no tunneling through a slab.

Limits: accepted native controls use ActionMoveToLocation, one installed door
model and ordinary walk speed. Keyboard steering, force moves, unusual door
geometry and exact contact skin are not separately measured. OpenWinter contact
positions differ by up to about 0.13 m from native; no fitted radius or timing
constant was introduced. Extending collision by appearance radius stops too
early. The separate cache shares all
existing walkmesh version invalidation; it does not mutate physical door state.
