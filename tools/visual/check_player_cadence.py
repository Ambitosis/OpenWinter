"""Summarize paired actual-player and copied-NPC attack timing; reject invalid contexts.

This checks the pccadence fixture's event evidence, not animation or audio parity.
Usage: python tools/visual/check_player_cadence.py <full-engine-log> [output.json]
"""
import hashlib,json,re,statistics,sys
from pathlib import Path

source=Path(sys.argv[1]);text=source.read_text(encoding="utf-8-sig",errors="replace")
if "VISUAL|done|1" not in text or re.search(r"TOO MANY INSTRUCTIONS|SCRIPT ERROR|Script error:|\[script error\]",text,re.I):
    raise SystemExit("Incomplete or failed cadence fixture")
observations={}
for key,value in re.findall(r"VISUAL\|note\|([^|\r\n]+)\|([^\r\n]+)",text):
    observations.setdefault(key,[]).append(value.strip())
if observations.get("copy.valid")!=["1"]:raise SystemExit("Invalid copied-character control")
identity={}
for actor,is_pc in (("pc",1),("copy",0)):
    rows=observations.get("identity."+actor,[])
    if len(rows)!=1:raise SystemExit("Missing/duplicate identity: "+actor)
    parts=rows[0].split(".");identity[actor]=dict(zip(parts[::2],map(int,parts[1::2])))
    expected=dict(valid=1,pc=is_pc,level=1,weapon=22,seen=1,enemy=1)
    if any(identity[actor].get(k)!=v for k,v in expected.items()):raise SystemExit("Invalid actor: "+actor+" "+str(identity[actor]))
for field in ("appearance","gender"):
    if identity['pc'][field]!=identity['copy'][field]:raise SystemExit("Mismatched "+field)
result=dict(source=str(source),sha256={str(source):hashlib.sha256(source.read_bytes()).hexdigest(),**{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in Path("parity/visual/scenes/pccadence").glob("*") if p.is_file()}},identity=identity,contexts=[],limits="Actual-player versus copied-NPC scripted melee only. A 50ms script clock quantizes event times. No pose, sound or player-input approval.")
for count,start,end in ((1,9,45),(2,53,89),(6,97,133)):
    for actor in ("pc","copy"):
        key=f"n{count}.{actor}"
        if observations.get(key+".apr")!=[str(count)]:raise SystemExit("APR context mismatch: "+key)
        rounds=[int(x)/20 for x in observations.get(key+".round",[]) if start<=int(x)/20<end]
        attacks=[int(x)/20 for x in observations.get(key+".attack",[]) if start<=int(x)/20<end]
        if len(rounds)<5 or not attacks:raise SystemExit("Too few complete rounds: "+key)
        intervals=[round(b-a,3) for a,b in zip(rounds,rounds[1:])]
        per_round=[sum(a<=t<b for t in attacks) for a,b in zip(rounds,rounds[1:])]
        # Native callbacks can straddle OnEndCombatRound under load (e.g.5 then7).
        # Require the exact total over complete intervals and retain every bin.
        if sum(per_round)!=count*len(per_round):raise SystemExit("Complete-round callback total does not establish APR: "+key+" "+str(per_round))
        result['contexts'].append(dict(key=key,roundSeconds=intervals,medianRoundSeconds=statistics.median(intervals),attacksPerCompleteRound=per_round,attackTimes=attacks,roundTimes=rounds))
print(json.dumps(result,indent=2))
if len(sys.argv)>2:
    with Path(sys.argv[2]).open('w',encoding='utf-8',newline='') as f:json.dump(result,f,indent=2);f.write('\n')
