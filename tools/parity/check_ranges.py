"""Compare retained complete native/client range controls without relaunching either engine."""
import argparse
import json
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("results", type=Path)
args = parser.parse_args()
summary = {}
for suite in ("ranges_override", "weapon_ranges", "weapon_reach"):
    def read(engine):
        lines = (args.results / suite / (engine + ".txt")).read_text(encoding="utf-8-sig").splitlines()
        result = {}
        for line in lines:
            key, value = line.split("|", 1)
            assert key not in result, (suite, "duplicate", key)
            result[key] = value
        assert result.get("__done__") == "1", (suite, engine, "incomplete")
        assert not any(key.startswith("error.") for key in result), (suite, engine, "script error")
        for key, value in result.items():
            if key.endswith((".executed", ".equipped", ".seen", ".valid")):
                assert value == "1", (suite, engine, "failed positive gate", key, value)
        return result
    native, client = read("nwn"), read("openwinter")
    assert native.keys() == client.keys(), (suite, "missing readings")
    for key in native:
        if key.endswith((".distance", ".moved", ".finalDistance")):
            assert abs(float(native[key]) - float(client[key])) <= 0.1, (suite, key, native[key], client[key])
        else:
            assert native[key] == client[key], (suite, key, native[key], client[key])
    summary[suite] = len(native) - 1
print(json.dumps({"matched": summary, "total": sum(summary.values()), "position_tolerance_metres": 0.1}, indent=2))
