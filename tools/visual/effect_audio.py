"""Measure projectile/impact cue routing with sandbox-only authored tones.

Native records only its harness-owned process; client records its Master bus.
Original resources are never modified. --original preserves the real samples;
--seconds can provide a short cue to distinguish repetition from natural tails.
"""
import argparse
import csv
import hashlib
import json
import math
from pathlib import Path
import runpy
import shutil
import struct
import sys
import wave

sys.path.insert(0,str(Path(__file__).resolve().parent))
import visual

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('engine',choices=('native','client'))
parser.add_argument('--scene',choices=('fireballbare','fireballpattern','fireballquiet','familiaraudio','mirvaudio','vfxaudiotime','vfxaudioinstant','beamaudio','beamanchor','beamdeath','beamlong','beamcold','beamcurve','beamcurveedge','beamcamera','beamreentry','cessaudio','cessnegative','cessneutral','cesscolumns','cesscorrect'),default='mirvaudio')
parser.add_argument('--original',action='store_true')
parser.add_argument('--visualeffects-tsv',type=Path,help='Clean installed Tools 2da dump, required only for cesscolumns')
parser.add_argument('--tone-frequency',type=float,help='Single-resource synthetic control frequency in Hz')
parser.add_argument('--seconds',type=float,default=8)
parser.add_argument('--tag',default='baseline')
args=parser.parse_args()
if not 0.05<=args.seconds<=20:parser.error('seconds must be in0.05..20')
if not args.tag.replace('-','').replace('_','').isalnum():parser.error('tag must be alphanumeric')
if args.scene in ('fireballquiet','fireballpattern','fireballbare'):
    frequencies={'spr_fireball':700,'fpa_hit':1900}
    markers=[f'cast.{n}.valid.1.seen.1.enemy.1' for n in range(4)]+[f'impact.{n}.spell.58' for n in range(4)]
    if args.scene in ('fireballpattern','fireballbare'):
        markers=[f'cast.{n}.valid.1.seen.1.enemy.1' for n in range(2)]+[f'impact.{n}.spell.58' for n in range(2)]+['play.valid.1']
elif args.scene=='familiaraudio':
    frequencies={'sff_summund':2700}
    markers=['before.pc.1.level.1.uses.1.familiar.0','arrived.pc.1.level.1.uses.0.familiar.1','late.pc.1.level.1.uses.0.familiar.1','arrived.resref.nw_fm_bat01','play.valid.1']
elif args.scene=='mirvaudio':
    frequencies={'spr_magicmissile':700,'sim_magblue':1100}
    markers=['near.valid.1','far.valid.1','VISUAL|note|instant','VISUAL|note|temporary']
elif args.scene=='vfxaudiotime':
    frequencies={'spr_magicmissile':700,'sdr_eleshield':1500,'sdr_eleshieldlp':1900}
    markers=['near.valid.1','far.valid.1','shield.valid.1','near.found.','far.found.','shield.found.']
elif args.scene in ('cesscolumns','cesscorrect'):
    frequencies={'sce_probe_fix':700,'sce_probe_legacy':1100,'sce_probe_model':1500,'sce_probe_impact':1900}
    markers=[f'row.{n}.valid.1' for n in (7001,7002,7003)]+[f'play.{ref}.valid.1' for ref in list(frequencies)[:3]]
    if args.scene=='cesscorrect':markers=['natural_applied.valid.1.effects.1','natural_after.valid.1.effects.0','early_before.valid.1.effects.1','early_after.valid.1.effects.0','permanent_before.valid.1.effects.1','permanent_after.valid.1.effects.0','play.valid.1']
    if args.visualeffects_tsv is None or args.original:parser.error('cesscolumns requires --visualeffects-tsv and synthetic mode')
elif args.scene in ('cessaudio','cessnegative','cessneutral'):
    frequencies={dict(cessaudio='sce_positive',cessnegative='sce_negative',cessneutral='sce_neutral')[args.scene]:3100}
    markers=['natural_applied.valid.1.effects.1','natural_after.valid.1.effects.0','early_before.valid.1.effects.1','early_after.valid.1.effects.0','permanent_before.valid.1.effects.1','permanent_after.valid.1.effects.0','play.valid.1']
elif args.scene=='beamreentry':
    frequencies={'sim_raylight':2700}
    markers=[f'{tag}.{n}.valid.11' for n in range(3) for tag in ('start','fresh')]+[f'{tag}.{n}.distance.{distance}.000.valid.1' for n in range(3) for tag,distance in (('out',40),('back',3))]
elif args.scene=='beamcamera':
    frequencies={'sim_raylight':2700}
    markers=['start.valid.11','camera.6','camera.14','camera.24']
elif args.scene in ('beamcurve','beamcurveedge'):
    frequencies={'sim_raylight':2700}
    distances=(1,3,6,10,20,30,40,50) if args.scene=='beamcurve' else (30,32,34,36,38,40,45,3)
    markers=['start.valid.11','remove.valid.1']+[f'distance.{d}.valid.1.actual.' for d in distances]
elif args.scene=='beamcold':
    frequencies={'sim_raycold':2700}
    markers=['short.valid.11','long.valid.11','play.valid.1']
elif args.scene=='beamlong':
    frequencies={'sim_raylight':2700}
    markers=['start.valid.11','near_start.valid.1','far.valid.1','near_return.valid.1','before_remove.valid.1','after_remove.valid.0']
elif args.scene=='beamdeath':
    frequencies={'sim_raylight':2700}
    markers=['start.valid.11','after_kill.valid.1.dead.1','after_decay.valid.','eight_after.valid.']
elif args.scene=='beamanchor':
    frequencies={'sim_raylight':2700}
    markers=[f'anchor.{n}.valid.11' for n in range(3)]
elif args.scene=='beamaudio':
    frequencies={'sim_raylight':2700}
    markers=[f'start.{n}.valid.11' for n in range(8)]+[f'end.{n}' for n in range(8)]
else:
    frequencies={'sdr_eleshield':1500,'sdr_eleshieldlp':1900,'sar_conecold':2300,'sim_raylight':2700}
    markers=['instant.valid.1']
if args.tone_frequency is not None:
    if args.original or len(frequencies)!=1 or not 40<=args.tone_frequency<=10000:
        parser.error('tone-frequency requires one synthetic resource and40..10000Hz')
    frequencies={ref:args.tone_frequency for ref in frequencies}
durations={ref:args.seconds for ref in frequencies}
if args.scene in ('fireballquiet','fireballpattern','fireballbare'):durations['fpa_hit']=.12
muted=['sco_mehanfire03','vs_chant_evoc_lf','vs_chant_evoc_lm','sff_explfire','sim_exp2flame'] if args.scene in ('fireballquiet','fireballpattern','fireballbare') else []
patterns={'spr_fireball':dict(stepSeconds=.25,frequencies=[500+400*n for n in range(12)])} if args.scene in ('fireballpattern','fireballbare') else {}
if patterns and (args.original or args.seconds!=3):parser.error('fireballpattern requires synthetic --seconds 3')
def sample_frequency(ref,i,default):
    pattern=patterns.get(ref)
    return pattern['frequencies'][min(11,int(i/44100/pattern['stepSeconds']))] if pattern else default
created=[]
prepare_user=visual.prepare_user
def prepare(*a,**kw):
    user=prepare_user(*a,**kw)
    if args.scene in ('cesscolumns','cesscorrect'):
        raw=args.visualeffects_tsv.read_bytes()
        rows=list(csv.DictReader(raw.decode('utf-16' if raw[:2]==b'\xff\xfe' else 'utf-8-sig').splitlines(),delimiter='\t'))
        headers=list(rows[0])
        if 'SoundCessation' not in headers:headers.append('SoundCessation')
        base=next(r for r in rows if r['row']=='206')
        for n,ref in ((7001,'sce_probe_fix'),(7002,'sce_probe_legacy'),(7003,'sce_probe_model')):
            row=next(r for r in rows if r['row']==str(n));row.update(base);row['row']=str(n)
            row['SoundImpact']='sce_probe_impact';row['SoundCessastion']='****';row['SoundCessation']='****'
            row['SoundCessation' if n==7001 else 'SoundCessastion']=ref
            if n==7003:row['Ces_HeadCon_Node']='vce_positive'
        path=user/'override'/'visualeffects.2da';path.parent.mkdir(exist_ok=True)
        with path.open('x',encoding='utf-8',newline='') as f:
            created.append(path);f.write('2DA V2.0\n\n'+' '.join(headers[1:])+'\n')
            for row in rows:f.write(' '.join(row.get(h,'****') or '****' for h in headers)+'\n')
    if not args.original:
        for resref in muted:
            path=user/'override'/(resref+'.wav');path.parent.mkdir(exist_ok=True)
            with path.open('xb') as raw:
                created.append(path)
                with wave.open(raw,'wb') as wav:
                    wav.setparams((1,2,44100,0,'NONE','not compressed'));wav.writeframes(bytes(8820))
        for resref,frequency in frequencies.items():
            path=user/'override'/(resref+'.wav');path.parent.mkdir(exist_ok=True)
            with path.open('xb') as raw:
                created.append(path)
                with wave.open(raw,'wb') as wav:
                    wav.setparams((1,2,44100,0,'NONE','not compressed'))
                    wav.writeframes(b''.join(struct.pack('<h',int(6000*math.sin(i*2*math.pi*sample_frequency(resref,i,frequency)/44100))) for i in range(round(44100*durations[resref]))))
    return user
output=visual.REPO/'out'/'effect-audio'/(args.engine+'-'+args.tag)
output.mkdir(parents=True,exist_ok=True)
visual.prepare_user=prepare
saved_argv=sys.argv
try:
    if args.engine=='client':
        cfg=visual.config();mod=visual.build(args.scene)[0]
        user=prepare(cfg,'aluviandarkstar',(1280,720),'plain')
        visual.run_openwinter(cfg,mod,user,output,150 if args.scene=='beamaudio' else 80,'aluviandarkstar',(1280,720),['--visual-audio','--visual-delay','0','--fx-verbose','--ui','classic'])
        metadata=output/'audio.json'
        data=json.loads(metadata.read_text(encoding='utf-8'))
    else:
        sys.argv=['spellvoice_audio.py',args.scene,'all']
        run=runpy.run_path(str(Path(__file__).with_name('spellvoice_audio.py')),run_name='__main__')
        source=visual.REPO/'out'/(run['run_name']+'.json')
        data=json.loads(source.read_text(encoding='utf-8'))
        shutil.copyfile(data['recording']['path'],output/'audio.wav')
        data['recording']['path']=str(output/'audio.wav')
        shutil.copyfile(run['user']/'logs'/'nwclientLog1.txt',output/'native.log')
        data['nativeLog']=str(output/'native.log')
        metadata=output/'audio.json'
    fixture=visual.REPO/'parity'/'visual'/'scenes'/args.scene
    data['probe']=dict(scene=args.scene,original=args.original,frequencies=frequencies,requiredMarkers=markers,durationSeconds=args.seconds,resourceDurations=durations,muted=muted,patterns=patterns,rate=44100,amplitude=6000,fixtureSha256={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(fixture.iterdir()) if p.is_file() and p.suffix in ('.json','.nss')})
    if args.visualeffects_tsv is not None:data['probe']['tableSourceSha256']=hashlib.sha256(args.visualeffects_tsv.read_bytes()).hexdigest()
    with metadata.open('w',encoding='utf-8',newline='') as f:json.dump(data,f,indent=2)
    print(metadata)
finally:
    visual.prepare_user=prepare_user;sys.argv=saved_argv
    for path in created:path.unlink()
