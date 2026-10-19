# Authored self-cast hand lifetime control

This uses the real stock Aluvian Darkstar Wizard 1 and an actual prepared slot.
The authored spell row has 4 seconds of conjuring and 5 seconds of recovery,
with the installed Mage Armor hand model and a logging-only impact script.
It is a timing/attachment control, not the complete stock Mage Armor effect.

Identity, known spell, authored table values, slot readiness, consumption,
impact and completed recovery must pass before any images count as evidence.
Reserved rows before 102 are required for native table indexing. A first sparse
native table failed these gates and is excluded.

`hastelong` covers repeated Haste flips from both initial states and a late
expiry followed by removal of Haste. `hastelongcap` adds a final removal during
release: the server action extends to 9.5 seconds while the initial fast hand
lifetime cap is 7.5 seconds. Use `tools/visual/check_haste_hands.py` on each
notes file (`--cap` for the latter); inspect the images separately.
