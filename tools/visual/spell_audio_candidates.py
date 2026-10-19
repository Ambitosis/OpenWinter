"""Build candidate audio references from locally extracted authored spell scripts.

This is a static candidate inventory, not proof that a branch executes or sound
plays. Inputs must come from a stock resolver with an empty sandbox UserRoot.
Includes, helper call graphs, AOE scripts, model emitters and computed resource
names remain explicit gaps. No installed script or audio is copied to output.
"""
import argparse
import csv
import json
import re
from pathlib import Path


def table(path):
    if path.suffix.lower() == ".json":
        return json.loads(path.read_text(encoding="utf-8-sig"))
    with path.open(encoding="utf-8-sig", newline="") as source:
        return {str(row.pop("row")): row for row in csv.DictReader(source, delimiter="\t")}


def candidates(spells, effects, constants, scripts):
    result = []
    for spell, data in spells.items():
        script = data.get("ImpactScript", "").lower()
        path = scripts / (script + ".nss")
        row = dict(id=int(spell), label=data.get("Label", ""), impactScript=script,
                   candidateSounds=[], ignoredSoundFields=[], candidateEffects=[], builtinEffects=[], includes=[], unresolved=[])
        result.append(row)
        if script in ("", "****"):
            row["status"] = "no-impact-script"
            continue
        if not path.exists():
            row["status"] = "authored-source-not-extracted"
            continue
        text = path.read_text(encoding="utf-8-sig")
        text = re.sub(r"/\*.*?\*/|//[^\n]*", "", text, flags=re.S)
        row["status"] = "direct-script-candidates-only"
        row["includes"] = re.findall(r'#include\s+"([^"]+)"', text)
        effect_sources = []
        # Native authored model markers identify the built-in familiar arrival.
        # This is a candidate for the call, not proof that its branch executes.
        if re.search(r"\bSummonFamiliar\s*\(\s*\)", text):
            row["builtinEffects"].append(dict(function="SummonFamiliar", row=36, evidence="measured native familiar arrival"))
            effect_sources.append(("36", f"{script}.nss SummonFamiliar() -> measured built-in arrival -> visualeffects.2da 36"))
        for name in sorted(set(re.findall(r"\bVFX_\w+", text))):
            if name not in constants:
                row["unresolved"].append(name)
                continue
            effect = str(constants[name])
            row["candidateEffects"].append(dict(constant=name, row=int(effect)))
            effect_sources.append((effect, f"{script}.nss {name} -> visualeffects.2da {effect}"))
        for effect, source in effect_sources:
            for field, ref in effects.get(effect, {}).items():
                if not field.startswith("Sound") or ref in ("", "****"):
                    continue
                if field == "SoundCessastion":
                    # Native authored-column controls read SoundCessation and ignore
                    # the legacy stock typo, even with an explicit cessation model.
                    row["ignoredSoundFields"].append(dict(role=field, resref=ref.lower(),
                        source=source, reason="native ignores misspelled column"))
                    continue
                row["candidateSounds"].append(dict(role=field, resref=ref.lower(), source=source))
        for ref in sorted(set(re.findall(r'\bPlaySound\s*\(\s*"([^"]+)"', text))):
            row["candidateSounds"].append(dict(role="script-PlaySound", resref=ref.lower(), source=script+".nss"))
        row["areaEffects"] = sorted(set(re.findall(r"\bAOE_\w+", text)))
        row["unresolvedVisualArguments"] = sorted(set(
            arg.strip() for arg in re.findall(r"\bEffectVisualEffect\s*\(\s*([^,)]+)", text)
            if arg.strip() not in constants and not arg.strip().isdigit()))
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("spells", type=Path, help="stock spells TSV or row-keyed JSON")
    parser.add_argument("effects", type=Path, help="stock visualeffects TSV or row-keyed JSON")
    parser.add_argument("nwscript", type=Path, help="installed authored nwscript.nss constants")
    parser.add_argument("scripts", type=Path, help="directory of extracted authored impact NSS")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    constants = {name: int(value) for name,value in re.findall(
        r"int\s+(VFX_\w+)\s*=\s*(\d+)\s*;", args.nwscript.read_text(encoding="utf-8-sig"))}
    rows = candidates(table(args.spells), table(args.effects), constants, args.scripts)
    report = dict(scope="direct authored impact script candidates; not observed playback",
                  gaps=["untraversed includes/helper calls", "computed sound names and VFX arguments",
                        "AOE event scripts", "unmeasured built-in engine calls", "model emitter/audio events", "branch legality and execution"], rows=rows)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2)+"\n", encoding="utf-8", newline="")
    print(f"{len(rows)} spell rows; {sum(len(r['candidateSounds']) for r in rows)} candidate references; {args.output}")


if __name__ == "__main__":
    main()
