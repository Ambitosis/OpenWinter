# Embedded module resource replacement

Wholly authored pyramid geometry overrides `c_rat.mdl` inside each module, with
red material in `modcontenta` and blue in `modcontentb`. Neither has a HAK. An
ordinary spawned creature is assigned installed appearance 386, whose model name
was read from the installed appearance table; no original model is copied.

Run:

```
python tools/visual/module_content.py --only nwn
python tools/visual/module_content.py --only openwinter
python tools/visual/check_module_content.py
```

The sandboxed silent runs both show red in the source and blue in the destination
under the same resource name. All four frames were inspected, and the simple color
gate records frame hashes. Client geometry survives the transition and uses fresh
module content. The client's material is visibly brighter/desaturated compared
with native; this is not material/lighting parity approval.

Additional headless actual-client checks cover interrupted downloads, stale chunks,
resource precedence, old-window closure, suppressed stale requests and deferred
archive disposal and transient cutscene/pause/rest recovery.
Classic art after reopening every individual panel and native custom-shield
attachment remain outside this visual control.
