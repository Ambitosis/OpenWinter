"""Reject incomplete/noncasting controls in the cast_commit parity fixture."""
from pathlib import Path
import sys
root=Path(sys.argv[1]) if len(sys.argv)>1 else Path("out/parity/results/cast_commit")
for engine in ("nwn","openwinter"):
    text=(root/(engine+".txt")).read_text(encoding="utf-8")
    assert "error." not in text.lower(), engine+": script error"
    rows=dict(line.split("|",1) for line in text.splitlines() if "|" in line)
    assert rows.get("__done__")=="1", engine+": incomplete"
    for i in range(25):
        alive=i<22
        impact=alive and i%11 not in (1,2,3,10)
        ready=(11<=i<22) or i in (1,2,3,10,24)
        expected={"class":1,"prepared":102,"preparedReady":1,"valid":1,
                  "dead":int(not alive),"impacts":int(impact),"ready":int(ready),
                  "after":int(i in (0,11))}
        for key,value in expected.items():
            assert rows.get(f"{i}.{key}")==str(value), (engine,i,key,rows.get(f"{i}.{key}"),value)
        if impact:
            elapsed=float(rows[f"{i}.impactTime"].split("|")[0])
            assert 1.9<=elapsed<=2.4, (engine,i,"invalid release time",elapsed)
print("cast commitment: prepared/cheat, early/late clear, queue removal and death controls pass")
