"""Compare per-actor grunt counts with damage callbacks in a bow control capture.
Distinct synthetic frequencies separate speakers; this is a hit-gating control,
not a substitute for independent single-speaker probability trials.
"""
import json, subprocess, sys
from pathlib import Path
p=Path(sys.argv[1]);data=json.loads(p.read_text(encoding='utf-8'))
if not data['recording'].get('session',{}).get('restored'):
    raise ValueError('Owned session restoration was not recorded')
notes=[line.split('VISUAL|',1)[1] for _,line in data['notes'] if 'VISUAL|' in line]
if 'done|1' not in notes: raise ValueError('Missing scene completion')
audio=json.loads(subprocess.check_output([sys.executable,str(Path(__file__).with_name('check_voice_actors.py')),str(p)],encoding='utf-8'))
rows=[]
for actor in range(20):
    def tagged(kind): return [line.split('|')[2:] for line in notes if line.startswith(f'{kind}|{actor}|')]
    seen=tagged('voice_seen');initial=tagged('bow_equipped');final=tagged('bow_ammo_remaining')
    if seen != [['1']] or len(initial)!=1 or len(final)!=1 or initial[0]!=['8','99']:
        raise ValueError(f'Actor{actor}: perception/equipment assertions failed')
    count=99-int(final[0][0])
    if not 0<count<99: raise ValueError(f'Actor{actor}: invalid ammunition consumption')
    tones=[row for row in audio if row['actor']==actor and row['family']=='grunt']
    if len(tones)!=1: raise ValueError(f'Actor{actor}: missing synthetic tone definition')
    swings=sum(line==f'voice_swing|{actor}' for line in notes)
    damaged=len(tagged('voice_damage'))
    rows.append(dict(actor=actor,shots=count,targetAttackEvents=swings,damageCallbacks=damaged,grunts=len(tones[0]['onsets'])))
groups=[]
for label,subset in [('highHit',rows[:10]),('lowHit',rows[10:])]:
    groups.append(dict(context=label,**{key:sum(row[key] for row in subset) for key in ['shots','targetAttackEvents','damageCallbacks','grunts']}))
print(json.dumps(dict(source=str(p),groups=groups,actors=rows,limitations='Synthetic multi-speaker separation controls hit gating. Use independent single-speaker trials for probability inference; damage callbacks do not identify every underlying attack roll.'),indent=2))
