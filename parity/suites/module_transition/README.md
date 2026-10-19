# StartNewModule without connected players

Run `python tools/parity/module_transition.py` from the repository root. It builds
an authored destination and stages it only in this suite's sandbox modules folder.
The source first requests a missing module, then a valid destination with zero
connected players. Both engines retain the source and its scheduled timer:
five readings match. Native nwserver logs that it cannot store player characters.

The actual successful-transfer control is `parity/visual/scenes/modulesource`,
documented in `docs/MODULE_TRANSITIONS.md`. This headless negative control must not
be interpreted as proof that connected players transfer.
