"""Isolate an original spell cue by silencing enumerated sandbox-only overlaps.

The candidate is never replaced. Explicit before/after PlaySound controls and
actual-player prepared-slot/execution gates are independent of waveform identity.
Native captures use the existing owned-process recorder and framebuffer helper.
"""
import argparse
import hashlib
import json
import re
from pathlib import Path
import runpy
import shutil
import subprocess
import sys
import wave

sys.path.insert(0, str(Path(__file__).resolve().parent))
import visual


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def execution_gates(text, candidate, spell):
    required = ['VISUAL|done|1', 'ready.1', 'cue.completed',
                'positive.before.'+candidate, 'positive.after.'+candidate,
                'state.before.pc.1.name.Aluvian Darkstar.wizard.1.gender.1.soundset.227',
                'state.before.slot.'+str(spell)+'.ready.1.known.1',
                'state.after.slot.'+str(spell)+'.ready.0.known.1',
                'state.before.target.1.seen.1.enemy.1.plot.0.',
                'execution.hooks.1.hits.1.events.1.finished.1']
    valid = all(marker in text for marker in required) and not any(
        marker in text for marker in ('INVALID_', 'TOO MANY INSTRUCTIONS', 'SCRIPT ERROR', '[script error]'))
    difficulty = re.findall(r'state\.(?:before|after)\.target\.1\.seen\.1\.enemy\.1\.plot\.0\.difficulty\.(\d+)', text)
    return valid and len(difficulty) == 2 and len(set(difficulty)) == 1, required, difficulty


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('engine', choices=('native', 'client'))
    parser.add_argument('scene', choices=('cuemmconj', 'cuemmproj', 'cueraybeam'))
    parser.add_argument('--tag', required=True)
    parser.add_argument('--client-root', type=Path,
                        help='Use an already-built client from another checkout; sandbox/output stay in this one')
    args = parser.parse_args()
    if not args.tag.replace('-', '').replace('_', '').isalnum():
        parser.error('tag must contain only letters, digits, hyphens or underscores')
    if args.client_root and args.engine != 'client':
        parser.error('--client-root requires client')
    repo = visual.REPO
    scene = visual.scene_json(args.scene)
    control = scene['cueControl']
    candidate = control['candidate']
    muted = control['silenced']
    if candidate in muted or len(muted) != len(set(muted)):
        raise ValueError('Candidate must remain original and substitutions must be unique')
    output = repo/'out'/'spell-cues'/(args.engine+'-'+args.scene+'-'+args.tag)
    output.mkdir(parents=True, exist_ok=False)
    original_prepare = visual.prepare_user
    original_run = visual.run_nwn
    original_argv = sys.argv
    created = []
    substitutions = []

    def prepare(*a, **kw):
        user = original_prepare(*a, **kw)
        if not user.resolve().is_relative_to((repo/'out').resolve()):
            raise ValueError('Audio substitutions require the owned harness sandbox')
        folder = user/'override'
        folder.mkdir(exist_ok=True)
        if (folder/(candidate+'.wav')).exists() or (folder/(candidate+'.bmu')).exists():
            raise ValueError('Candidate already overridden; original-source control invalid')
        for ref in muted:
            if not ref.replace('_', '').isalnum():
                raise ValueError('Invalid resource name')
            path = folder/(ref+'.wav')
            if (folder/(ref+'.bmu')).exists():
                raise ValueError('Conflicting BMU override for '+ref)
            with path.open('xb') as raw:
                created.append(path)
                with wave.open(raw, 'wb') as stream:
                    stream.setparams((1, 2, 44100, 0, 'NONE', 'not compressed'))
                    stream.writeframes(bytes(88200))
            substitutions.append(dict(resref=ref, kind='one-second-silent-PCM', sha256=sha(path)))
        return user

    def native(*a, **kw):
        kw['native_framebuffer'] = True
        return original_run(*a, **kw)

    visual.prepare_user = prepare
    visual.run_nwn = native
    try:
        if args.engine == 'native':
            sys.argv = ['spellvoice_audio.py', args.scene, 'all']
            run = runpy.run_path(str(repo/'tools'/'visual'/'spellvoice_audio.py'), run_name='__main__')
            source = repo/'out'/(run['run_name']+'.json')
            data = json.loads(source.read_text(encoding='utf-8'))
            shutil.copyfile(data['recording']['path'], output/'audio.wav')
            data['recording']['path'] = str(output/'audio.wav')
            shutil.copyfile(run['user']/'logs'/'nwclientLog1.txt', output/'native.log')
            shots = visual.OUT/'shots'/run['run_name']/'nwn'
            for path in shots.iterdir():
                if path.suffix in ('.png', '.tga', '.jsonl'):
                    shutil.copyfile(path, output/path.name)
            log = output/'native.log'
            runtime = repo
            binaries = {}
        else:
            cfg = visual.config()
            mod = visual.build(args.scene)[0]
            user = prepare(cfg, 'aluviandarkstar', (1280, 720), 'plain')
            runtime = args.client_root.resolve() if args.client_root else repo
            binaries = {str(p.relative_to(runtime)): sha(p) for p in (runtime/'client'/'.godot'/'mono'/'temp'/'bin'/'Debug').glob('OpenWinter.*.dll')}
            if 'client/.godot/mono/temp/bin/Debug/OpenWinter.Client.dll' not in {p.replace('\\', '/') for p in binaries}:
                raise ValueError('Built client assembly required')
            visual.REPO = runtime
            visual.run_openwinter(cfg, mod, user, output, scene['seconds'],
                               'aluviandarkstar', (1280, 720),
                               ['--visual-audio', '--visual-gui', '--visual-delay', '0', '--ui', 'classic'])
            visual.REPO = repo
            data = json.loads((output/'audio.json').read_text(encoding='utf-8'))
            log = output/'godot.log'
        text = log.read_text(encoding='utf-8', errors='replace')
        valid, required, difficulty = execution_gates(text, candidate, control['spell'])
        if args.engine == 'client':
            valid = valid and '[harness] exit code 0' in text
        fixture = repo/'parity'/'visual'/'scenes'/args.scene
        shutil.copytree(fixture, output/'fixture')
        data['probe'] = dict(engine=args.engine, scene=args.scene, candidate=candidate,
                             validated=valid, requiredMarkers=required, substitutions=substitutions,
                             actualDifficulty=difficulty, runtimeBinaries=binaries,
                             fixtureSha256={p.name: sha(p) for p in (repo/'parity'/'visual'/'scenes'/args.scene).iterdir() if p.suffix in ('.nss', '.json')},
                             runtimeRoot=str(runtime), runtimeRevision=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=runtime, text=True).strip(),
                             scriptLog=str(log), scriptLogSha256=sha(log), waveformSha256=sha(output/'audio.wav'),
                             limitation='Original candidate with explicit silent overlap substitutions; routing/identity control, not original mix or exact timing parity.')
        (output/'audio.json').write_text(json.dumps(data, indent=2)+'\n', encoding='utf-8', newline='')
        if not valid:
            raise RuntimeError('Actual-player spell cue context failed; retained as rejected evidence')
        print(output/'audio.json')
    finally:
        visual.REPO = repo
        visual.prepare_user = original_prepare
        visual.run_nwn = original_run
        sys.argv = original_argv
        for path in created:
            path.unlink()


if __name__ == '__main__':
    main()
