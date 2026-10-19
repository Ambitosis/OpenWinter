# Prepared spell cadence diagnostic

Eight explicit NPC Wizard 20 contexts cover ordinary, Haste, Quicken and both,
with and without an attacker. Authored spell 0 has 1500 ms conjure and 1000 ms
recovery; the standard release interval is observed separately. Actual prepared
slots, Quicken feat, Haste effect, metamagic context, combat and attack gates are
logged. Three genuine non-cheat casts consume the matching slots. ActionDoCommand
markers immediately after each cast distinguish recovery from next-cast eligibility.

Native idle normal impacts: 2.012, 8.035, 14.074 seconds; after-action markers:
3.008, 9.063, 15.059. Haste impacts: 1.277, 4.313, 7.320; Quicken:
1.000, 4.020, 7.027; both: 1.016, 4.035, 7.039. Combat controls pass,
with occasional extra native attack-of-opportunity delay. These are NPC authored
spell observations, not actual-PC or all-spell timing proof.

OpenWinter currently disagrees: Haste does not shorten conjure, ordinary casts
are spaced about 3 seconds, and Quicken is instantaneous. This suite is a failing
diagnostic until scheduling is corrected. Do not widen tolerance to hide those gaps.

An earlier fixture revision used an incorrect class-column name (Wizard instead of
Wiz_Sorc); it had no valid prepared spells or impacts and is excluded. The separate
cast_cadence cheat pilot established idle ordinary/Haste behavior only: its requested
Quicken was discarded by cheat casting and its plot targets were not attacked.
