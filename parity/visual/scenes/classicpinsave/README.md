# Bounded native pin export control

The first two frames isolate ordinary Journal-left/Inventory-right promotion.
The later attempted pin flow avoids PrintScreen while its text modal would be
open. Its final visual text/pin gate MUST pass before interpreting any export as
pin persistence evidence. Current layout1 run failed that gate: letters triggered
ordinary hotkeys. The export and autosave are retained as rejected pin evidence.

Run through visual.py run_nwn with a dedicated sandbox out/visual/pin-save-user,
not the real vault. Copy localvault/saves immediately into the run's shot folder
and retain hashes; prepare_user resets the shared harness localvault between runs.
The dedicated user folder prevents overwriting any other probe's autosave. Never
reuse an old saved-files.json as proof of a fresh successful pin.
