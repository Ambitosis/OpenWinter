"""Verify chant selector fixtures against installed local waveform references.
Every scene uses each expected chant once; whole-recording diagnostics are kept
separate from padded per-cast matches, never used as precise timing evidence.
"""
import argparse,hashlib,json,re
from pathlib import Path
import match_spell_audio as match
p=argparse.ArgumentParser()
p.add_argument('metadata',type=Path);p.add_argument('manifest',type=Path)
p.add_argument('table',type=Path);p.add_argument('clips',type=Path)
p.add_argument('--output',type=Path,required=True)
a=p.parse_args();data=json.loads(a.metadata.read_text(encoding='utf-8'));manifest=json.loads(a.manifest.read_text(encoding='utf-8'));table=json.loads(a.table.read_text(encoding='utf-8'))
logPath=Path(data['nativeLog']) if 'nativeLog' in data else a.metadata.with_name('godot.log')
log=logPath.read_text(encoding='utf-8',errors='replace')
if re.search(r'TOO MANY INSTRUCTIONS|\[script error\]|SCRIPT ERROR',log,re.IGNORECASE):
    raise ValueError('Script error in capture log')
match.RATE=44100;record=data['recording'];capture=match.decode(record['path'])
if 'nativeLog' in data and not record.get('session',{}).get('restored'):
    raise ValueError('Owned native audio session was not restored')
if not len(capture) or record.get('pcm_peak',1)==0:
    raise ValueError('Capture is empty or silent')
notes=[(stamp,line.split('VISUAL|',1)[1].strip()) for stamp,line in data['notes'] if 'VISUAL|' in line]
if not any(line=='done|1' for _,line in notes):raise ValueError('Scene did not complete')
gender=manifest['gender'];expectedRole='ConjSoundMale' if gender==0 else 'ConjSoundFemale';oppositeRole='ConjSoundFemale' if gender==0 else 'ConjSoundMale'
rows=[];sets=set();cache={}
for r in manifest['rows']:
 spell=r['id'];starts=[stamp-record['started'] for stamp,line in notes if line==f'note|start.{spell}']
 if r['expected'] != table[str(spell)][expectedRole]:
  raise ValueError(f'Manifest/table chant mismatch for spell {spell}')
 selectors=[line for _,line in notes if line.startswith(f'note|selector.{spell}.')]
 found=re.fullmatch(r'note\|selector\.\d+\.gender\.(\d+)\.soundset\.(\d+)\.target\.(\d+)',selectors[0]) if len(selectors)==1 else None
 valid=len(starts)==1 and found is not None and int(found[1])==gender and found[3]=='1'
 impact=any(line==f'note|impact.{spell}' for _,line in notes);finished=any(line==f'note|finished.{spell}' for _,line in notes)
 result=dict(id=spell,label=r['label'],selectorValid=valid,impact=impact,finished=finished,sounds=[]);rows.append(result)
 if not valid or not impact or not finished:
  result['status']='invalid-trigger';continue
 sets.add(int(found[2]));start=starts[0]
 segment=capture[max(0,int((start-3)*match.RATE)):min(len(capture),int((start+r['span'])*match.RATE))]
 for role in [expectedRole,oppositeRole]:
  ref=table[str(spell)][role]
  if ref not in cache:cache[ref]=match.decode(a.clips/(ref+'.wav'))
  result['sounds'].append(dict(role=role,resref=ref,referenceSha256=hashlib.sha256((a.clips/(ref+'.wav')).read_bytes()).hexdigest(),expected=role==expectedRole,timed=match.identify(segment,cache[ref]),wholeRecording=match.identify(capture,cache[ref])))
 result['status']='analyzed'
report=dict(metadata=str(a.metadata),manifest=str(a.manifest),gender=gender,soundsets=sorted(sets),sameSoundset=len(sets)==1,log=str(logPath),sha256={str(path):hashlib.sha256(Path(path).read_bytes()).hexdigest() for path in (a.metadata,a.manifest,a.table,logPath,record['path'])},configuration=dict(rate=44100,leadingPaddingSeconds=3,multipleAnchorThreshold=.65,singleAnchorThreshold=.85),rows=rows,limitations='Black native frames are not visual evidence. Whole-recording identity does not establish onset, gain, spatialization or a complete spell. Padding tolerates buffered native log receipt. Distinct expected references occur once each in the fixture; impact/completion/selector markers are required separately.')
a.output.parent.mkdir(parents=True,exist_ok=True)
with a.output.open('w',encoding='utf-8',newline='') as f:json.dump(report,f,indent=2)
print([(r['id'],r['status'],[(s['role'],s['timed']['status'],s['wholeRecording']['status']) for s in r['sounds']]) for r in rows])
