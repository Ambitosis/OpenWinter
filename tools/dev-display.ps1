# Keeps dev windows (Godot client runs, nwmain) on a chosen monitor.
#   dev-display.ps1                    prints "x,y" near the dev display's top-left, for Godot --position
#   dev-display.ps1 -Move nwmain       moves that process's main window onto the dev display
# The display comes from "devDisplay" in config/local.json (e.g. "DISPLAY2"), or -Display.
# Falls back to another secondary monitor; prints nothing and exits 1 when none is configured or only the primary is left.
param([string]$Display = "", [string]$Move = "", [int]$TimeoutSeconds = 30)

if (-not $Display) {
    $config = Join-Path $PSScriptRoot "..\config\local.json"
    if (Test-Path $config) { $Display = (Get-Content $config -Raw | ConvertFrom-Json).devDisplay }
}
if (-not $Display) { exit 1 }

Add-Type -AssemblyName System.Windows.Forms
$screen = [System.Windows.Forms.Screen]::AllScreens | Where-Object { $_.DeviceName -like "*$Display" } | Select-Object -First 1
# That monitor switched off: use another secondary one, never the primary.
if (-not $screen) { $screen = [System.Windows.Forms.Screen]::AllScreens | Where-Object { -not $_.Primary } | Select-Object -First 1 }
if (-not $screen) { exit 1 }
$area = $screen.WorkingArea

# Godot measures window positions from the top-left of the whole desktop, not the primary monitor.
$desktop = [System.Windows.Forms.SystemInformation]::VirtualScreen
if (-not $Move) { "$($area.X - $desktop.X + 20),$($area.Y - $desktop.Y + 20)"; exit 0 }

Add-Type -Namespace DevDisplay -Name Win32 -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int cy, uint flags);
'@
# SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE: move only, never steal focus.
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
do {
    $windows = Get-Process -Name $Move -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 }
    if ($windows) {
        foreach ($p in $windows) { [void][DevDisplay.Win32]::SetWindowPos($p.MainWindowHandle, [IntPtr]::Zero, $area.X, $area.Y, 0, 0, 0x15) }
        exit 0
    }
    Start-Sleep -Milliseconds 250
} while ((Get-Date) -lt $deadline)
exit 1
