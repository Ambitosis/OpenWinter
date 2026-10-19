# Familiar chat and quickchat

Silent original client control, using the generated actual-PC
familiar fixture. All game sound volumes were zero; no audio capture was made.

`python tools/visual/visual.py run familiarchat --only nwn --profile plain --tag _edited --silent`

Actual OnPlayerChat notes prove master speaker before possession, familiar speaker
while master GetIsPC=0/familiar GetIsPC=1/GetIsPossessedFamiliar=1, and master speaker
after keyboard release. Native screenshots b/c show V quickchat is available while
possessed and V/S/S selects/closes it. Master soundset=227, familiar soundset=6.
No audible or native emitted-resource identity claim follows from this silent run.
Native single-player GetPCPlayerName is blank in all three callbacks.

Nested ExecuteScript edits survive into OnPlayerChat and delivered text: master
text becomes EDITED_MASTER, familiar text EDITED_FAMILIAR and volume changes to
whisper. Nested event identity is 3017/direct 0; parent remains 3017/direct 3017.
Direct SetPCChatMessage("") suppresses the return-phase message. Screenshots were
inspected; b shows actual changed speaker and whisper caption, d restores the
master portrait and does not display the suppressed message. Pause/unpause lines
occur during the keyboard sequence, so there is no precise timing claim.

Both harness limitations are retained: the first _reference run stopped at the
quickchat menu because the native PrintScreen screenshot was consumed. The corrected
GDI _edited run completed every phase and captured four valid images, but its
post-run identity-marker check exited 1 because the exact marker was omitted.
Ready independently rejects a PC whose name is not `000 Familiar Possession`;
notes and screenshots show the correct PC. The standard marker is now emitted.
evidence.json records source hashes from the actual run
and this distinction; no native run is presented as an unqualified harness pass.

The installed English NWN_OnlineManual.pdf, PDF page 9, states that /P sends to the
other party members. Party messages now route to original-player party membership
across areas, retaining the currently controlled creature as speaker. Nearby
outsiders do not receive them. This recipient rule is manual-sourced, not a newly
measured two-client native control.

FamiliarCommunicationTests go through actual GameServer/loopback requests: talk/whisper and public script speaker identity before/during/after
possession; quickchat exact subject/soundset/resref without audio playback; party
sender+recipient possession, cross-area delivery, outsider exclusion, and return.
Installed soundsets with different populated hello slots make accidental use of
the master's resource detectable. The client CreatureVoice already resolves the
server resource and parents playback under the notification's speaker object.
No production speaker/soundset mapping needed changing. Party routing is the only
runtime edit; event-context mutable chat fixes are a separate follow-up.

Evidence: out/visual/shots/vis_familiarchat_{reference,edited}/nwn and
out/familiar-communication-tests.txt. No installed assets are committed.
