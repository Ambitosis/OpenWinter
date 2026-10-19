#!/usr/bin/env bash
# Runs the Godot .NET console binary against the client project.
#   tools/godot.sh                          play (single-player)
#   tools/godot.sh -- --capture out/demo    scripted demo with screenshots
# With "devDisplay" in config/local.json the window opens on that monitor
# (unless --position, --screen or --headless is given).
GODOT="${GODOT:-$(ls "$LOCALAPPDATA"/Microsoft/WinGet/Packages/GodotEngine.GodotEngine.Mono_*/Godot_v4*/Godot_v4*_mono_win64_console.exe 2>/dev/null | head -1)}"
here="$(cd "$(dirname "$0")" && pwd)"
place=()
if [[ " $* " != *" --position "* && " $* " != *" --screen "* && " $* " != *" --headless "* ]]; then
    pos="$(powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(cygpath -w "$here/dev-display.ps1")" 2>/dev/null | tr -d '\r')"
    [[ -n "$pos" ]] && place=(--position "$pos")
fi
cd "$here/../client" && exec "$GODOT" --path . "${place[@]}" "$@"
