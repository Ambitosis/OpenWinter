# Event identity and public SignalEvent routing

Measured on the installed original dedicated server, through the sandbox parity
harness. No UI, audio capture or game assets are included.

```
dotnet src/OpenWinter.Tools/bin/Debug/net10.0/openwinter-tools.dll parity-run event_context
```

Result: **193 matched, 0 different, 0 missing**. Native object-validity and public
SetEventScript gates pass. Handlers run once: creature count 4 (spawn plus three
signals), placeable and door count 3 each. Module load, area enter, and module
activation/user events are also observed.

The original reports the matching EVENT_SCRIPT_* identity inside each handler.
ExecuteScript and ExecuteScriptChunk inherit it by default, but their direct
`GetCurrentlyRunningEvent(FALSE)` is zero. On return the parent still reports its
own identity. DelayCommand and AssignCommand report zero for both forms.

EventConversation routes to creature ScriptDialogue (5007), placeable OnDialog
(9014), and door OnDialog (10013). Public setters install the latter two handlers
in this suite: this isolates routing from blueprint-field loading, which
`object_dialog` measures. No conversation begins and no abort policy changes.

The installed public nwscript.nss declares exactly four Event* constructors:
EventUserDefined, EventSpellCastAt, EventConversation and EventActivateItem.
NWNXPopEvent is a separate extension bridge. There are no public EventHeartbeat,
EventDamaged, etc. constructors to implement. The existing four constructor
implementations are retained; only confirmed Conversation target routing changes.

Implementation uses one shared EVENT_SCRIPT_* field catalog for Get/SetEventScript
and World.RunEvent. RunEvent stamps a cloned context, preserving caller data and
preventing reused/nested context mutation. GetCurrentlyRunningEvent distinguishes
ExecuteScript/Chunk direct context from inherited context. Ordinary queued command
scheduling is unchanged. Tests cover all catalogued handlers,
context isolation, nested scripts/chunks, deferred commands, and asynchronous
conversation signals.

Limits: the fixture samples handlers from five object classes; all nine catalog
classes are covered by regression tests, not exhaustive native dispatch probes.
Reentrant engine callbacks such as damage/death within a parent script can have
native inheritance quirks documented in nwscript.nss; those were not measured here.
The suite does not claim to fix their parent-inheritance behavior, GFF OnDialog
loading, cutscene cancellation, blocked-door movement, or arbitrary NWNX events.
GetUserDefinedEventNumber is checked only inside user events: an initial exploratory
read outside that event returned native undefined values, so it is not a comparison.

Evidence hashes and complete readings are in evidence.json.

## Mutable event payload follow-up

A silent actual-PC original control in `familiarchat` establishes that nested ExecuteScript edits to SetPCChatMessage and
SetPCChatVolume survive into the parent OnPlayerChat callback and delivered chat.
Native child event=3017/direct=0; parent=3017/direct=3017. The screenshot shows
EDITED_FAMILIAR with the familiar speaker and Whisper channel. Direct empty-string
suppression also succeeds. This is synchronous event payload, not scoped script
identity or named call parameters.

RunEvent, ExecuteScript, and ExecuteScriptChunk now return payload writes while
retaining their isolated event identities and script parameters.
ChatEventMutationTests cover actual network delivery/edit/suppression, nested and
chunk readback, and parameter/identity isolation. Chunk mutation has regression
coverage; this new native control specifically used ExecuteScript.

Native evidence: `out/visual/shots/vis_familiarchat_edited/nwn`; transcript subset
and hashes are appended to evidence.json. All authored phases completed and
screenshots were inspected, but the harness post-run exact character-fixture
marker was omitted (exit 1). The script separately rejects the wrong PC name,
and actual possession states and speaker identity are positive.
The prior framebuffer attempt was interrupted by F12 being consumed in quickchat.
