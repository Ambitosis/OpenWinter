"""Print a release's notes: its CHANGELOG entry followed by the install section.

    python build/release_notes.py 0.1.1 > notes.md
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    version = sys.argv[1]
    changelog = (ROOT / "CHANGELOG.md").read_text(encoding="utf-8")
    entry = re.search(r"^## " + re.escape(version) + r"\b.*?\n(.*?)(?=^## |\Z)", changelog, re.S | re.M)
    if not entry:
        sys.exit(f"CHANGELOG.md has no entry for {version}")
    install = (ROOT / "build" / "release" / "notes-install.md").read_text(encoding="utf-8")
    sys.stdout.reconfigure(encoding="utf-8")
    print(entry.group(1).strip() + "\n\n" + install.replace("{version}", version).strip())


if __name__ == "__main__":
    main()
