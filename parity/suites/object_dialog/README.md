# EE object dialog hooks

The shared public event catalog and SignalEvent routing are tested separately in
`event_context`.

## Measurements

`dotnet .../openwinter-tools.dll parity-run object_dialog` matches **10/10** native
readings. Native GetEventScript confirms GFF `OnDialog` for both placeable and door
when the field has **ResRef** type. The initial ExoString fixture was rejected
(empty handlers, zero callbacks); it establishes no loading behavior. The recipe
builder now accepts `{"resref":"od_dialog"}` for fields absent from legacy
blueprints. SignalEvent invokes each loaded hook once. The optional NPC action is
not an actual-PC interaction gate and does not justify NPC-specific conclusions.

Two short silent native-client controls use actual PCs on DISPLAY2 and public
scripted actions, without mouse/keyboard input or screenshots:

* `objectdialog`: actual-PC and target-valid gates pass; empty and explicit
  ActionStartConversation calls each invoke placeable/door OnDialog once. A hook
  that does nothing suppresses dialog startup. Both queues finish. Separate
  ActionInteractObject calls OnUsed rather than OnDialog.
* `objectdialogbegin`: before the hook begins its tree, GetLastSpeaker and
  GetPCSpeaker are invalid, and GetLastUsedBy/GetClickingObject are not the PC.
  Nevertheless BeginConversation() succeeds, using the initiating PC and the
  explicit dialog resref supplied to ActionStartConversation. The original
  conversation is active at finish.

The original ran with the sandbox sound volumes set to zero; no real user settings
were changed. Commands were `python tools/visual/visual.py run objectdialog[begin]
--only nwn --profile plain --silent --tag _reference`. Source/output hashes and exact native notes are in evidence.json.

## Implementation and checks

AreaLoader reads OnDialog from both object types. Player talk and scripted
ActionStartConversation share `ConversationService.RunObjectDialog`; a hook may
veto by doing nothing. Private initiating-PC/resref context lets BeginConversation
use defaults without exposing a premature public speaker. Default dialog lookup
reads the object's Conversation template field. Normal door use, placeable use,
and all existing conversation abort conditions stay unchanged.

Object-dialog tests sit beside the event-context and conversation tests. GFF and
script-routing native/client
comparison is headless; the natural-PC controls measured original behavior only.
Mouse-input talk dispatch, private-conversation inheritance through hooks, delayed
BeginConversation, and NPC-initiated object dialogs are not separately measured.
