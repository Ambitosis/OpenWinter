"""Report sandbox tone lifetimes at several fixed detector thresholds.

This measures synthetic routing/lifetime, not original-sample identity or gain.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import wave

import numpy as np
from scipy.signal import stft

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('metadata',type=Path)
parser.add_argument('--output',type=Path,required=True)
parser.add_argument('--clips',type=Path,help='Installed WAV/BMU references for --original captures')
args=parser.parse_args()
data=json.loads(args.metadata.read_text(encoding='utf-8'))
record=data['recording'];probe=data['probe']
if 'nativeLog' in data and not record.get('session',{}).get('restored'):
    raise ValueError('Owned native audio session was not restored')
log=Path(data.get('nativeLog',args.metadata.with_name('godot.log')))
text=log.read_text(encoding='utf-8',errors='replace')
if re.search(r'TOO MANY INSTRUCTIONS|SCRIPT ERROR|\[script error\]',text,re.IGNORECASE):
    raise ValueError('Script error invalidates the fixture')
for marker in ['VISUAL|done|1',*probe.get('requiredMarkers',['near.valid.1','far.valid.1','VISUAL|note|instant','VISUAL|note|temporary'])]:
    if marker not in text:raise ValueError('Missing fixture marker: '+marker)
wav=Path(record['path'])
if probe['original']:
    if args.clips is None:parser.error('--clips is required for original references')
    import match_spell_audio as matcher
    matcher.RATE=44100
    capture=matcher.decode(wav)
    sounds=[];inputs=[args.metadata,log,wav]
    # These prefixes follow the separately measured one-second MIRV and .8s
    # sustained-effect lifetimes. Preserve full-reference attempts as well.
    for resref in probe['frequencies']:
        path=next((args.clips/(resref+ext) for ext in ('.wav','.bmu') if (args.clips/(resref+ext)).exists()),None)
        if path is None:
            sounds.append(dict(resref=resref,status='reference-not-extracted'));continue
        inputs.append(path)
        prefixes=(.5,1.0) if resref=='spr_magicmissile' else ((.5,.8) if resref=='sdr_eleshieldlp' else ())
        reference=matcher.decode(path)
        if probe['scene'] in ('cessaudio','cessnegative','cessneutral'):
            for tag in ('natural','early','permanent','play'):
                marks=[stamp-record['started'] for stamp,line in data['notes'] if 'VISUAL|note|'+tag+'.valid.' in line]
                if len(marks)!=1:raise ValueError('Expected exactly one cessation marker: '+tag)
                start=max(0,marks[0]-.5);end=marks[0]+5.5
                segment=capture[int(start*matcher.RATE):int(end*matcher.RATE)]
                sounds.append(dict(resref=resref,phase=tag,windowStart=start,windowEnd=end,**matcher.identify_candidates(segment,reference,())))
        elif probe['scene']=='beamcold':
            for tag,span in (('short',4.0),('long',9.0),('play',8.0)):
                marks=[stamp-record['started'] for stamp,line in data['notes'] if 'VISUAL|note|'+tag+'.valid.' in line]
                if len(marks)!=1:raise ValueError('Expected exactly one cold-beam marker: '+tag)
                start=max(0,marks[0]-.5)
                segment=capture[int(start*matcher.RATE):int((marks[0]+span)*matcher.RATE)]
                sounds.append(dict(resref=resref,phase=tag,windowStart=start,windowEnd=marks[0]+span,**matcher.identify_candidates(segment,reference,(1.0,1.7))))
        else:
            sounds.append(dict(resref=resref,**matcher.identify_candidates(capture,reference,prefixes)))
    result=dict(metadata=str(args.metadata),probe=probe,sounds=sounds,configuration=dict(analysisRate=44100,multipleAnchorThreshold=.65,singleAnchorThreshold=.85,requiredMultipleAnchors=2,originToleranceSeconds=.025),sha256={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs},limitations='Cue identity only, not every occurrence; beamcold has explicit isolated phase windows. Prefix identification proves only that portion. Unresolved does not prove absent. No volume, spatialization, onset, complete playback or visual parity claim.')
    with args.output.open('w',encoding='utf-8',newline='') as f:json.dump(result,f,indent=2)
    print(json.dumps(sounds,indent=2));raise SystemExit(0)
with wave.open(str(wav),'rb') as w:
    rate=w.getframerate();channels=w.getnchannels()
    if w.getsampwidth()!=2:raise ValueError('Expected16-bit PCM')
    samples=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2').reshape(-1,channels).T
frequencies,times,spectrum=stft(samples,rate,nperseg=round(rate*.02),noverlap=round(rate*.015),axis=-1)
tones=[]
for resref,frequency in probe['frequencies'].items():
    # Average channel magnitudes, avoiding cancellation of spatial stereo phase.
    # A fixed +/-100Hz band permits motion Doppler without choosing a new threshold.
    power=np.abs(spectrum[:,abs(frequencies-frequency)<=100,:]).max(axis=1).mean(axis=0)
    detections=[]
    for fraction in (.025,.05,.10):
        threshold=max(5,float(power.max())*fraction)
        indices=np.flatnonzero(power>threshold)
        groups=np.split(indices,np.where(np.diff(indices)>3)[0]+1)
        segments=[dict(start=round(float(times[g[0]]),4),end=round(float(times[g[-1]]),4),duration=round(float(times[g[-1]]-times[g[0]]),4),peak=round(float(power[g].max()),3)) for g in groups if len(g)>10]
        detections.append(dict(relativeThreshold=fraction,absoluteFloor=5,threshold=threshold,segments=segments))
    tone=dict(resref=resref,frequency=frequency,detections=detections)
    if probe['scene']=='familiaraudio':
        # A silent/failed process capture must not look like a valid absence result.
        phases=[]
        for tag,marker in (('arrival','VISUAL|note|start.318'),('direct','VISUAL|note|play.valid.1')):
            marks=[stamp-record['started'] for stamp,line in data['notes'] if line.endswith(marker)]
            if len(marks)!=1:raise ValueError('Expected one familiar phase marker: '+tag)
            start=marks[0]-.25;end=marks[0]+6
            readings=[]
            for detection in detections:
                selected=[g for g in detection['segments'] if start<=g['start']<=end]
                if len(selected)!=1:raise ValueError('Missing or ambiguous familiar positive control: '+tag)
                if abs(selected[0]['duration']-probe['durationSeconds'])>.06:
                    raise ValueError('Familiar tone lifetime differs from authored duration: '+tag)
                readings.append(dict(relativeThreshold=detection['relativeThreshold'],segment=selected[0]))
            phases.append(dict(phase=tag,thresholds=readings))
        tone['positiveControls']=phases
    if probe['scene'] in ('cessaudio','cessnegative','cessneutral','cesscorrect','cesscolumns'):
        phases=[]
        tags=([f'row.{n}' for n in (7001,7002,7003)]+[f'play.{ref}' for ref in list(probe['frequencies'])[:3]]) if probe['scene']=='cesscolumns' else ['natural','early','permanent','play']
        for tag in tags:
            marks=[stamp-record['started'] for stamp,line in data['notes'] if 'VISUAL|note|'+tag+'.valid.' in line]
            if len(marks)!=1:raise ValueError('Expected exactly one phase marker: '+tag)
            start=max(0,marks[0]-.5);end=marks[0]+(1.5 if tag.startswith('play.') else 5.5)
            phases.append(dict(phase=tag,windowStart=start,windowEnd=end,thresholds=[dict(relativeThreshold=d['relativeThreshold'],segments=[g for g in d['segments'] if g['end']>=start and g['start']<=end]) for d in detections]))
        tone['phases']=phases
    if probe['scene']=='beamlong':
        # Native script coordinates, rather than command submission, establish movement.
        for tag,expected in (('near_start',3.162),('far',20.025),('near_return',3.162)):
            match=re.search(rf'{tag}\.valid\.1\.x\.[-0-9.]+\.y\.[-0-9.]+\.distance\.([-0-9.]+)',text)
            if match is None or abs(float(match[1])-expected)>.02:
                raise ValueError('Target movement not established: '+tag)
        detected=detections[1]['segments']
        if not detected:raise ValueError('No long-beam tone detected')
        onset=detected[0]['start']
        # Wide interior windows avoid interpolation and buffered log timestamps.
        phase=[]
        for name,start,end in (('near_initial',3,10),('far',18,26),('near_return',34,46)):
            selected=power[(times>=onset+start)&(times<=onset+end)]
            if not len(selected):raise ValueError('Incomplete beam phase: '+name)
            phase.append(dict(name=name,startAfterOnset=start,endAfterOnset=end,medianBandMagnitude=float(np.median(selected))))
        tone['movementPhases']=phase
        tone['movementClaim']='Distance verified by script. Band magnitudes test following, not attenuation equivalence.'
    if probe['scene'] in ('beamcurve','beamcurveedge'):
        detected=detections[1]['segments']
        if not detected:raise ValueError('No beam curve tone detected')
        onset=detected[0]['start'];phase=[]
        distances=(1,3,6,10,20,30,40,50) if probe['scene']=='beamcurve' else (30,32,34,36,38,40,45,3)
        for i,distance in enumerate(distances):
            match=re.search(rf'distance\.{distance}\.valid\.1\.actual\.([-0-9.]+)',text)
            if match is None or abs(float(match[1])-distance)>.02:
                raise ValueError('Beam curve target distance not established: '+str(distance))
            selected=power[(times>=onset+i*5+1.5)&(times<=onset+i*5+3.5)]
            if not len(selected):raise ValueError('Incomplete beam distance phase')
            phase.append(dict(distance=distance,actualDistance=float(match[1]),medianBandMagnitude=float(np.median(selected))))
        reference=phase[0]['medianBandMagnitude']
        for entry in phase:entry['relativeToFirstDistance']=entry['medianBandMagnitude']/reference
        tone['distancePhases']=phase
    if probe['scene']=='beamreentry':
        detected=detections[1]['segments']
        if not detected:raise ValueError('No reentry control tone detected')
        onset=detected[0]['start'];phase=[]
        for n,kind in enumerate(('instant','temporary','permanent')):
            expected=0 if n==0 else 1
            for tag,distance in (('initial',3),('out',40),('back',3)):
                marker=f'{tag}.{n}.distance.{distance}.000.valid.1.beams.{expected}'
                if marker not in text:raise ValueError('Missing reentry state: '+marker)
            values={}
            for tag,start,end in (('initial',.5,1.5),('out',3.0,4.5),('back',6.0,7.5),('fresh',8.5,10.5)):
                chosen=power[(times>=onset+n*18+start)&(times<=onset+n*18+end)]
                if not len(chosen):raise ValueError('Incomplete reentry phase')
                values[tag]=float(np.median(chosen))
            phase.append(dict(durationType=kind,effectListCount=expected,medianBandMagnitude=values))
        tone['reentryPhases']=phase
        tone['reentryClaim']='Audio only: geometry disappearance or reconstruction is unobserved. Script verifies retained effect lists and actual distances.'
    if probe['scene']=='beamcamera':
        detected=detections[1]['segments']
        if not detected:raise ValueError('No beam camera tone detected')
        onset=detected[0]['start'];phase=[]
        for i,distance in enumerate((6,14,24)):
            selected=power[(times>=onset+i*6+1.5)&(times<=onset+i*6+3.5)]
            if not len(selected):raise ValueError('Incomplete camera phase')
            phase.append(dict(requestedCameraDistance=distance,medianBandMagnitude=float(np.median(selected))))
        tone['cameraPhases']=phase
        tone['cameraClaim']='SetCameraFacing commands only; native black images cannot independently confirm camera positions.'
    tones.append(tone)
result=dict(metadata=str(args.metadata),probe=probe,rate=rate,windowSeconds=.02,hopSeconds=.005,bandWidthHz=200,tones=tones,sha256={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in (args.metadata,log,wav)},limitations='Synthetic waveform routing/lifetime only. Native frames may be black. Script log receipt is buffered and is not sample-accurate onset evidence. Fixed detector thresholds are all retained; amplitude, Doppler and spatialization are not declared equivalent.')
with args.output.open('w',encoding='utf-8',newline='') as f:json.dump(result,f,indent=2)
print(json.dumps(tones,indent=2))
