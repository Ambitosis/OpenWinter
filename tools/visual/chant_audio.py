"""Record one native chant fixture and preserve its full script log.

Requires the process-only recorder (process_audio.py) and its line cursor.
This wrapper does not change sound resources, audio routing or native input.
"""
import argparse
import json
from pathlib import Path
import runpy
import shutil
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("scene", choices=("chantmale", "chantfemale",
                        "chantmalehigh", "chantfemalehigh", "chantmaleextra"))
    args = parser.parse_args()
    tools = Path(__file__).resolve().parent
    if not (tools / "process_audio.py").exists():
        parser.error("The process-only recorder (process_audio.py) is missing")
    original = sys.argv
    try:
        sys.argv = [str(tools / "spellvoice_audio.py"), args.scene, "all"]
        run = runpy.run_path(sys.argv[0], run_name="__main__")
    finally:
        sys.argv = original
    metadata = Path("out") / (run["run_name"] + ".json")
    data = json.loads(metadata.read_text(encoding="utf-8"))
    if not data.get("recording"):
        raise RuntimeError("Recorder did not produce process-only PCM metadata")
    log = metadata.with_suffix(".log").resolve()
    shutil.copyfile(run["user"] / "logs" / "nwclientLog1.txt", log)
    data["nativeLog"] = str(log)
    with metadata.open("w", encoding="utf-8", newline="") as output:
        json.dump(data, output, indent=2)


if __name__ == "__main__":
    main()
