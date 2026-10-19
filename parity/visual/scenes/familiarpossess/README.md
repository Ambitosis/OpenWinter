# Familiar possession

Actual player fixture: a sandbox copy of `aluviandarkstar`, renamed
`000 Familiar Possession`, Wizard 1, bat `nw_fm_bat01` named Thamat. The fixture
checks class, player identity, familiar identity and association before input.
No installed assets or character files are committed. All runs are silent.

The accepted `vis_familiarpossess_access2/nwn` control uses Num0, Num4, Num1
to select Possess Familiar; Num0, Num2 selects Unpossess. It proves:

- Control changes GetIsPC from original player to familiar; GetFirstPC still
  returns the original character. Only the familiar reports possessed.
- Actual W input moves the familiar 6.4075m while the original body stays at
  (20,15). This is an input-ownership observation, not a speed comparison.
- I refuses inventory with installed TLK 68708. C opens Thamat's Animal 1 sheet.
- The familiar has an empty quickbar and the original bindings return on release.
- Campaign player keys follow the possessed familiar while the inactive original
  body uses the global key, as other nonplayers do. ExportAllCharacters still
  exports the original player BIC. Internal vault identity therefore differs
  deliberately from script-visible campaign/IsPC identity.

Earlier accepted state/release/death controls additionally establish that
UnpossessFamiliar(original master) is a no-op, including when called as the
master; passing the controlled familiar returns control. This differs from the
installed nwscript.nss parameter comment. DestroyObject on a possessed familiar
is ignored; EffectDeath returns control and the stock familiar death script
removes the associate. The reproducible death variant is `familiarpossess_death`.

Run the current fixture (the native framebuffer helper may consume input while
an interactive menu is open, so it takes no intermediate possession-menu shots):

```powershell
python tools/visual/visual.py run familiarpossess --only nwn --silent --native-framebuffer --tag _access
python tools/visual/visual.py run familiarpossess --only openwinter --silent --tag _classic --openwinter-args '--ui classic --visual-state --visual-familiar-proof'
python tools/visual/visual.py run familiarpossess --only openwinter --silent --tag _modern --openwinter-args '--ui modern --visual-state --visual-familiar-proof'
python tools/visual/check_familiar_possession.py out/visual/shots/vis_familiarpossess_classic/openwinter
```

The optional client proof deliberately opens the old owner's inventory and
character sheet and starts targeting before possession. State records assert
that these clear, camera/HUD follow the controlled ID, self is absent from its
party list, all 36 owner slots return unchanged, inventory is denied and the
character sheet reports the familiar. PNG inspection remains required.

The first access run failed the possession state gate and moved the original
body. Its I/C results are rejected. Initial client captures exposed stale camera,
duplicate familiar party membership and stale HUD on return; these are failure
baselines, superseded by the final captures. Intermediate mouse/portrait discovery
attempts did not prove activation and are not behavioral evidence.

See the separate `familiarchat` fixture for chat and quickchat while possessed.
