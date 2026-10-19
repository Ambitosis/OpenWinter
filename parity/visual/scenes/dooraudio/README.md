# Door sounds

One installed wooden door is opened, closed, tried while locked and destroyed,
six seconds apart, with UTC-linked `door_audio` markers.

```
python tools/visual/spellvoice_audio.py dooraudio effects --native-framebuffer
python tools/visual/visual.py run dooraudio --only openwinter --openwinter-args "--ui classic --visual-delay 0 --visual-audio 1"
python tools/visual/check_door_audio.py
```

Native capture requires the existing process-audio helper's pycaw/comtypes
dependencies; the checker requires numpy/scipy. Only the harness-owned process is
recorded. Every action has nonzero output and matches the corresponding native
waveform above 0.997 normalized correlation. This does not calibrate gain or timing.
See [door evidence](../../../../docs/DOORS.md).
