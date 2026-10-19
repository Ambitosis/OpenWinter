"""Check actual expiration times, independently of script phase counters."""
from pathlib import Path
import sys

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("out/parity/results/aoe_expiry")
for engine in ("nwn", "openwinter"):
    path = root / (engine + ".txt")
    data = dict(line.split("|", 1) for line in path.read_text(encoding="utf-8").splitlines() if "|" in line)
    if data.get("__done__") != "1":
        raise ValueError(f"{engine}: missing completion")
    for source in range(2):
        for phase in range(2):
            for duration in (.1, .5, 1.5, 2.5, 3.5, 6.5, 8.5, 13.5):
                key = f"source{source}.phase{phase}.duration{duration:.1f}"
                elapsed = float(data[key + ".elapsed"])
                if not duration - .01 <= elapsed <= duration + .15:
                    raise ValueError(f"{engine}: {key} expired at {elapsed}")
                if data[key + ".exits"] != "1":
                    raise ValueError(f"{engine}: {key} must emit exactly one exit")
    print(f"{engine}:32 duration/context cases within150ms, each exactly one exit")
