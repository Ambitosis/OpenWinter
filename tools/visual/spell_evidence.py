"""Generate reproducible spell presentation batches and an honest per-row evidence ledger.

No installed assets are copied into source control. Inventory uses an empty sandbox user root;
captures never become 'verified' automatically. Review annotations are separate authored input.
Requires the normal visual harness, Pillow for contacts, and a built OpenWinter.Tools Release DLL.
"""
import argparse
import csv
import hashlib
import io
import json
import math
import re
from pathlib import Path
import subprocess
import sys
import datetime
from spell_evidence_audio import inspect_audio, trusted_audio, interval_evidence
from spell_evidence_frames import inspect_frames, trusted_native_frame

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'out/spell-audit'
AXES = ('animation', 'casterFx', 'projectile', 'targetFx', 'audio', 'cleanup')


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('w', encoding='utf-8', newline='') as stream:
        stream.write(value if isinstance(value, str) else json.dumps(value, indent=2) + '\n')


def stock_rows():
    config = json.loads((ROOT / 'config/local.json').read_text(encoding='utf-8-sig'))
    research = OUT / 'stock'
    empty_user = research / 'user'
    empty_user.mkdir(parents=True, exist_ok=True)
    # A stock visual fixture contains only our scripts; unlike the real development module,
    # it cannot inject altered spells.2da or user overrides into an inventory.
    fixture = ROOT / 'out/visual/build/vis_calib.mod'
    if not fixture.exists():
        subprocess.run(['dotnet', 'run', '--project', 'src/OpenWinter.Tools', '-c', 'Release',
                        '--no-build', '--', 'visual-build', 'calib'], cwd=ROOT, check=True, capture_output=True)
    write(research / 'config/local.json', dict(gameRoot=config['gameRoot'], userRoot=str(empty_user),
          module=str(fixture), language=config.get('language', 'en'), devDisplay='DISPLAY2'))
    dll = ROOT / 'src/OpenWinter.Tools/bin/Release/net8.0/openwinter-tools.dll'
    result = subprocess.run(['dotnet', str(dll), '2da', 'spells', '10000', '--base'], cwd=research,
                            check=True, capture_output=True, text=True, encoding='utf-8')
    table = result.stdout[result.stdout.index('row\t'):]
    write(OUT / 'stock-spells.tsv', table)
    rows = list(csv.DictReader(io.StringIO(table), delimiter='\t'))
    write(OUT / 'inventory-source.json', dict(gameRoot=config['gameRoot'], userRoot=str(empty_user),
          rows=len(rows), sha256=hashlib.sha256(table.encode()).hexdigest(),
          note='Stock table metadata, not rendering or rules parity. No real user override/development directory.'))
    return rows


def val(row, name):
    value = row.get(name, '')
    return '' if value == '****' else value


def integer(row, name):
    try:
        return int(val(row, name), 0)
    except ValueError:
        return 0


def default_profile(row):
    target = 'enemy' if integer(row, 'HostileSetting') else 'self'
    mask = integer(row, 'TargetType')
    if val(row, 'Range') == 'P':
        target = 'self'
    elif mask & 4 and val(row, 'TargetShape'):
        target = 'ground'
    elif not mask & 2 and not mask & 1:
        target = 'manual'
    if not val(row, 'ImpactScript'):
        target = 'manual'
    return dict(target=target, caster='pc', status='inferred',
                reason='Initial table-based trigger only; legal target/caster and script execution require review.')


def read_optional(path):
    return json.loads(path.read_text(encoding='utf-8')) if path.exists() else {}


PREAMBLE = '''#include "visual_inc"
void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|"+s); }
void Clean(object pc) {
    AssignCommand(pc,ClearAllActions(TRUE));
    effect e=GetFirstEffect(pc);
    while(GetIsEffectValid(e)){RemoveEffect(pc,e);e=GetFirstEffect(pc);}
    object o=GetFirstObjectInArea(GetArea(pc));
    while(GetIsObjectValid(o)){
        object next=GetNextObjectInArea(GetArea(pc));
        if(o!=pc && (GetObjectType(o)==OBJECT_TYPE_CREATURE || GetObjectType(o)==OBJECT_TYPE_AREA_OF_EFFECT || GetObjectType(o)==OBJECT_TYPE_PLACEABLE))  {SetIsDestroyable(TRUE,FALSE,FALSE,o);SetPlotFlag(o,FALSE);DestroyObject(o);}
        o=next;
    }
}
void Quiet(object o){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");AssignCommand(o,ClearAllActions(TRUE));SetAILevel(o,AI_LEVEL_NORMAL);}
void Finished(int spell){Note("finished."+IntToString(spell));}
void Observe(int spell,string phase){
    object caster=GetLocalObject(GetModule(),"spb_caster");
    object target=GetLocalObject(GetModule(),"spb_target");
    string key="state."+IntToString(spell)+"."+phase+".";
    int feat=GetLocalInt(GetModule(),"spb_feat");
    if(feat>=0){Note(key+"feat_present."+IntToString(GetHasFeat(feat,caster)));Note(key+"feat_uses."+IntToString(GetFeatRemainingUses(feat,caster)));}
    Note(key+"caster_valid."+IntToString(GetIsObjectValid(caster)));
    Note(key+"target_valid."+IntToString(GetIsObjectValid(target)));
    if(GetIsObjectValid(target)){
        Note(key+"hp."+IntToString(GetCurrentHitPoints(target)));
        Note(key+"friendly."+IntToString(GetIsReactionTypeFriendly(target,caster)));
        Note(key+"enemy."+IntToString(GetIsEnemy(target,caster)));
        Note(key+"enemy_reverse."+IntToString(GetIsEnemy(caster,target)));
        Note(key+"seen."+IntToString(GetObjectSeen(target,caster)));
        effect e=GetFirstEffect(target);int count=0;
        while(GetIsEffectValid(e)){
            Note(key+"effect."+IntToString(GetEffectType(e))+"."+IntToString(GetEffectSpellId(e)));
            count++;e=GetNextEffect(target);
        }
        Note(key+"effects."+IntToString(count));
        if(GetObjectType(target)==OBJECT_TYPE_ITEM){
            itemproperty ip=GetFirstItemProperty(target);count=0;
            while(GetIsItemPropertyValid(ip)){
                Note(key+"itemproperty."+IntToString(GetItemPropertyType(ip))+"."+IntToString(GetItemPropertySubType(ip)));
                count++;ip=GetNextItemProperty(target);
            }
            Note(key+"itemproperties."+IntToString(count));
        }
    }
    int kind;
    for(kind=1;kind<=5;kind++){
        object associate=GetAssociate(kind,caster);
        Note(key+"associate."+IntToString(kind)+"."+IntToString(GetIsObjectValid(associate)));
        if(GetIsObjectValid(associate))Note(key+"associate_resref."+GetResRef(associate));
    }
}

'''


def make_cast(row, profile, ordinal):
    spell = int(row['row'])
    target_kind = profile['target']
    trigger = profile.get('trigger', 'spell')
    if trigger not in ('spell', 'feat'):
        raise ValueError(f'{spell}: unsupported trigger {trigger}')
    feat = profile.get('feat', -1)
    if trigger == 'feat' and (type(feat) is not int or not 0 <= feat <= 65535):
        raise ValueError(f'{spell}: feat trigger requires a numeric feat in 0..65535')
    if trigger == 'feat' and target_kind in ('ground', 'location'):
        raise ValueError(f'{spell}: feat location targeting requires an authored fixture')
    caster = f'spc{spell:03d}' if profile.get('casterFixture') else profile.get('caster', 'pc')
    distance = float(profile.get('distance', 1.5 if val(row, 'Range').upper() == 'T' else 6.0))
    lines = [f'void Prepare{ordinal}(object pc){{', 'object caster=pc;',
             'object target=pc;']
    if caster != 'pc':
        lines += [f'caster=VSpawn(pc,"{caster}",-2.0,0.0,90.0);', 'Quiet(caster);ChangeToStandardFaction(caster,STANDARD_FACTION_DEFENDER);']
    lines += ['target=caster;object weapon=GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,caster);']
    if target_kind in ('ally', 'enemy', 'ground', 'corpse', 'undead', 'animal'):
        blueprint = profile.get('targetResRef', {'undead': 'nw_skeleton', 'animal': 'nw_dog'}.get(target_kind, 'nw_bandit001'))
        lines += [f'target=VSpawn(pc,"{blueprint}",0.0,{distance:.2f},270.0);', 'Quiet(target);',
                  'SetIsDestroyable(FALSE,FALSE,FALSE,target);SetPlotFlag(target,FALSE);',
                  'SetEventScript(target,5002,"spb_event");SetEventScript(target,5004,"spb_damage");']
        if target_kind in ('enemy', 'ground', 'undead'):
            lines += ['ChangeToStandardFaction(target,STANDARD_FACTION_HOSTILE);',
                      f'Note("fixture.{spell}.enemy_before."+IntToString(GetIsEnemy(target,caster)));',
                      'SetIsTemporaryEnemy(caster,target);SetIsTemporaryEnemy(target,caster);']
        if target_kind in ('ally', 'animal'):
            lines += ['ChangeToStandardFaction(target,STANDARD_FACTION_DEFENDER);']
        if target_kind == 'corpse':
            lines += ['SetPlotFlag(target,FALSE);ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(),target);']
        else:
            lines += ['ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectTemporaryHitpoints(10000),target);']
    elif target_kind in ('weapon', 'armor', 'item'):
        if target_kind == 'armor':
            lines += ['target=GetItemInSlot(INVENTORY_SLOT_CHEST,caster);']
        elif target_kind == 'weapon':
            lines += ['target=weapon;']
        else:
            lines += [f'target=CreateItemOnObject("{profile.get("targetResRef", "nw_it_mpotion001")}",caster);']
    elif target_kind == 'location':
        lines += [f'vector point=GetPosition(pc);point.y+={distance:.2f};',
                  'SetLocalLocation(GetModule(),"spb_location",Location(GetArea(pc),point,0.0));']
    elif target_kind == 'placeable':
        lines += [f'target=VPlaceable(pc,"{profile.get("targetResRef", "plc_chest1")}",0.0,5.0);']
    elif target_kind != 'self':
        raise ValueError(f'{spell}: unsupported automated target {target_kind}; mark manual until fixture is authored')
    for extra in profile.get('extraTargets', []):
        dx, dy = float(extra['x']), float(extra['y'])
        faction = 'HOSTILE' if extra.get('enemy', True) else 'DEFENDER'
        lines += [f'{{object extra=VSpawn(pc,"{extra.get("resref", "nw_bandit001")}",{dx:.2f},{dy:.2f},270.0);',
                  f'Quiet(extra);ChangeToStandardFaction(extra,STANDARD_FACTION_{faction});SetPlotFlag(extra,FALSE);',
                  'ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectTemporaryHitpoints(10000),extra);',
                  'SetEventScript(extra,5002,"spb_event");SetEventScript(extra,5004,"spb_damage");']
        if extra.get('enemy', True):
            lines += ['SetIsTemporaryEnemy(caster,extra);SetIsTemporaryEnemy(extra,caster);']
        lines += ['}']
    for cls in profile.get('casterFixture', {}).get('classLevels', []):
        lines += [f'Note("fixture.{spell}.class.{cls["class"]}."+IntToString(GetLevelByClass({cls["class"]},caster)));']
    lines += [f'SetLocalInt(GetModule(),"spb_feat",{feat if trigger == "feat" else -1});']
    lines += profile.get('setup', [])
    if trigger == 'feat':
        action = f'AssignCommand(caster,ActionUseFeat({feat},target));'
    else:
        cheat = profile.get('cheat', True)
        if type(cheat) is not bool:
            raise ValueError(f'{spell}: cheat must be a boolean')
        action = f'AssignCommand(caster,ActionCastSpellAt{"Location" if target_kind in ("ground", "location") else "Object"}({spell},' + ('GetLocalLocation(GetModule(),"spb_location")' if target_kind == 'location' else 'GetLocation(target)' if target_kind == 'ground' else 'target') + f',METAMAGIC_NONE,{"TRUE" if cheat else "FALSE"}));'

    lines += [f'SetLocalObject(GetModule(),"spb_caster",caster);SetLocalObject(GetModule(),"spb_target",target);', '}',
              f'void Cast{ordinal}(object pc){{object caster=GetLocalObject(GetModule(),"spb_caster");object target=GetLocalObject(GetModule(),"spb_target");',
              f'SetLocalInt(GetModule(),"spb_active",{spell});Observe({spell},"before");',
              f'Note("start.{spell}");Note("target.{spell}."+IntToString(GetIsObjectValid(target)));',
              action,
              f'AssignCommand(caster,ActionDoCommand(Finished({spell})));', '}']
    return '\n'.join(lines)


def generated_directory(scene):
    if not re.fullmatch(r'[a-z][a-z0-9_]{0,7}[0-9]{3}', scene):
        raise ValueError('Invalid generated spell batch name.')
    directory = ROOT / 'parity/visual/scenes' / scene
    if not directory.resolve().is_relative_to((ROOT / 'parity/visual/scenes').resolve()):
        raise ValueError('Generated scene resolves outside the scene tree.')
    marker = directory / '.spell-evidence-owned'
    identity = f'spell-evidence-v1\n{scene}\n'
    if directory.exists() and (not marker.exists() or marker.read_text(encoding='utf-8') != identity):
        previous = read_optional(OUT / 'batches' / (scene + '.json'))
        if not (previous.get('scene') == scene and previous.get('fixtureSha256') == fixture_hash(scene)):
            raise FileExistsError(f'Refusing to overwrite an unowned or changed scene: {directory}. Choose another --prefix.')
    directory.mkdir(parents=True, exist_ok=True)
    # Generator metadata is not a runtime asset, so neither file alters fixture_hash.
    write(marker, identity)
    write(directory / '.gitignore', '*\n')
    return directory


def generate(args):
    prefix = getattr(args, 'prefix', 'spb')
    if not re.fullmatch(r'[a-z][a-z0-9_]{0,7}', prefix):
        raise ValueError('Batch prefix must be 1..8 lowercase ASCII letters/digits/underscores, starting with a letter')
    rows = stock_rows()
    profiles = read_optional(ROOT / 'parity/visual/spell_profiles.json')
    wanted = {int(x) for x in args.ids.split(',')} if args.ids else None
    selected = [r for r in rows if args.start <= int(r['row']) <= args.end and val(r, 'Label')
                and (wanted is None or int(r['row']) in wanted)]
    batches, skipped = [], []
    usable = []
    for row in selected:
        profile = {**default_profile(row), **profiles.get(row['row'], {})}
        if profile['target'] in ('manual', 'door'):
            skipped.append(dict(id=int(row['row']), profile=profile))
        else:
            usable.append((row, profile))
    for offset in range(0, len(usable), args.batch_size):
        entries = usable[offset:offset + args.batch_size]
        # Stable name; capture manifests bind exact content to prevent stale evidence reuse.
        scene = f'{prefix}{int(entries[0][0]["row"]):03d}'
        directory = generated_directory(scene)
        for row, profile in entries:
            if profile.get('casterFixture'):
                write(directory / f'spc{int(row["row"]):03d}.utc.json', profile['casterFixture'])
        script = PREAMBLE + '\n' + '\n'.join(make_cast(r, p, i) for i, (r, p) in enumerate(entries))
        script += '\nvoid main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);\n'
        script += 'SetLocalString(GetModule(),"X2_S_UD_SPELLSCRIPT","spb_hook");Note("difficulty."+IntToString(GetGameDifficulty()));\n'
        script += 'DelayCommand(1.0,VCamera(pc,90.0,13.0,60.0));\n'
        cursor, records = 2.25, []
        for i, (row, profile) in enumerate(entries):
            spell = int(row['row'])
            preparation = float(profile.get('prepareSeconds', 6 if profile.get('casterFixture') or profile.get('caster', 'pc') != 'pc' else 1))
            now = cursor + .75 + preparation
            conjure = integer(row, 'ConjTime') / 1000
            recover = integer(row, 'CastTime') / 1000
            span = max(8.0, conjure + recover + 5.0, profile.get('durationSeconds', 0))
            phases = dict(conjure=min(.75, max(.1, conjure / 2)), release=conjure + .6,
                          impact=conjure + 1.8, late=conjure + 3.2, cleanup=span - .5)
            script += f'DelayCommand({cursor:.2f},Clean(pc));\nDelayCommand({cursor + .75:.2f},Prepare{i}(pc));\nDelayCommand({now:.2f},Cast{i}(pc));\n'
            phases.update(profile.get('phases', {}))
            camera_plan = []
            if getattr(args, 'close_caster', False):
                phases['caster_postconjure'] = conjure + .25
                camera_plan = [dict(at=-.25, direction=90, distance=6.5, pitch=65),
                               dict(at=conjure + .45, direction=90, distance=13, pitch=60)]
                for camera in camera_plan:
                    script += f'DelayCommand({now + camera["at"]:.2f},VCamera(pc,{camera["direction"]:.1f},{camera["distance"]:.1f},{camera["pitch"]:.1f}));\n'
            for phase, when in phases.items():
                script += f'DelayCommand({now + when:.2f},VShot("s{spell:03d}_{phase}"));\n'
                script += f'DelayCommand({now + when:.2f},Observe({spell},"{phase}"));\n'
            script += f'DelayCommand({now + span - 1.5:.2f},Note("forced_cleanup.{spell}"));\n'
            script += f'DelayCommand({now + span - 1.5:.2f},Clean(pc));\n'
            records.append(dict(id=spell, label=val(row, 'Label'), profile=profile, phases=phases,
                                start=now, span=span, preparationSeconds=preparation, cameraPlan=camera_plan, cleanup='forced removal, not natural duration expiry'))
            cursor = now + span
        script += f'DelayCommand({cursor:.2f},VDone());}}\n'
        write(directory / 'vis_main.nss', script)
        write(directory / 'spb_hook.nss', '#include "visual_inc"\nvoid main(){WriteTimestampedLogEntry("VISUAL|note|impact."+IntToString(GetSpellId()));}\n')
        write(directory / 'spb_event.nss', 'void main(){WriteTimestampedLogEntry("VISUAL|note|spell_event."+IntToString(GetLocalInt(GetModule(),"spb_active"))+"."+IntToString(GetLastSpell())+"."+GetResRef(OBJECT_SELF));}\n')
        write(directory / 'spb_damage.nss', 'void main(){WriteTimestampedLogEntry("VISUAL|note|damage_event."+IntToString(GetLocalInt(GetModule(),"spb_active"))+"."+IntToString(GetTotalDamageDealt())+"."+GetResRef(OBJECT_SELF));}\n')
        write(directory / 'scene.json', dict(tileset='tde01', tile=4, size=4, pc=[20, 14, 0], facing=90, seconds=math.ceil(cursor + 4), difficulty=3))
        batch = dict(scene=scene, rows=records, seconds=math.ceil(cursor + 4), revision=subprocess.check_output(
            ['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip())
        batch['fixtureSha256'] = fixture_hash(scene)
        write(OUT / 'batches' / (scene + '.json'), batch)
        batches.append(batch)
    write(OUT / 'generation.json', dict(batches=[b['scene'] for b in batches], skipped=skipped))
    print(f'{len(batches)} batches, {len(usable)} initial triggers, {len(skipped)} require authored profiles.')
    print('These are inferred triggers, not validated legal scenarios or parity evidence.')
    for batch in batches:
        print(batch['scene'], batch['seconds'])


def fixture_hash(scene):
    digest = hashlib.sha256()
    for path in sorted((ROOT / 'parity/visual/scenes' / scene).iterdir()):
        if path.suffix in ('.nss', '.json', '.mdl', '.tga'):
            digest.update(path.name.encode())
            digest.update(path.read_bytes())
    return digest.hexdigest()


def trigger_observations(events, spell):
    """Facts about execution; completing an action or taking a PNG is not proof."""
    prefix = f'state.{spell}.'
    states = [dict(time=when, note=line) for when, line in events
              if line.startswith((prefix, f'spell_event.{spell}.', f'damage_event.{spell}.'))]
    phases = {}
    for _, line in events:
        if not line.startswith(prefix):
            continue
        phase, field, *values = line[len(prefix):].split('.')
        phases.setdefault(phase, {}).setdefault(field, []).append('.'.join(values))
    start = next((when for when, line in events if line == f'start.{spell}'), None)
    finish = next((when for when, line in events if line == f'finished.{spell}'), None)
    proof = []
    if any(line == f'impact.{spell}' for _, line in events): proof.append('script-hook')
    if any(line.startswith(f'spell_event.{spell}.') for _, line in events): proof.append('target-spell-event')
    if any(line.startswith(f'damage_event.{spell}.') for _, line in events): proof.append('target-damage-event')
    before = phases.get('before', {})
    for phase, state in phases.items():
        if phase in ('before', 'cleanup'): continue
        if any(x.endswith('.1') and x not in before.get('associate', []) for x in state.get('associate', [])):
            proof.append('associate-created')
        if state.get('effect', []) != before.get('effect', []): proof.append('target-effect-change')
        if state.get('itemproperty', []) != before.get('itemproperty', []): proof.append('item-property-change')
    proof = sorted(set(proof))
    return dict(targetValid=any(line == f'target.{spell}.1' for _, line in events),
                impactHookObserved='script-hook' in proof,
                actionFinished=any(line == f'finished.{spell}' for _, line in events),
                finishDelaySeconds=finish-start if start is not None and finish is not None else None,
                executionEvidence=proof, triggerStatus='execution-observed' if proof else 'execution-unproven',
                stateEvents=states, phases=phases,
                note='State/event observations do not prove correct rendering, audio, exact rules or natural cleanup.')


def context_mismatches(rows, observations):
    failures = []
    for row in rows:
        observed = observations[str(row['id'])]['phases'].get('before', {})
        for field, value in row['profile'].get('expectedBefore', {}).items():
            if observed.get(field) != [str(value)]:
                failures.append(dict(spell=row['id'],field=field,expected=value,actual=observed.get(field)))
    return failures


def capture(args):
    native_framebuffer = getattr(args, 'native_framebuffer', False)
    if native_framebuffer and args.engine != 'nwn':
        raise SystemExit('--native-framebuffer requires --engine nwn.')
    if not re.fullmatch(r'[a-z][a-z0-9_]{0,7}[0-9]{3}', args.scene):
        raise SystemExit('Invalid generated spell batch name.')
    batch = read_optional(OUT / 'batches' / (args.scene + '.json'))
    if not batch or batch.get('scene') != args.scene or fixture_hash(args.scene) != batch['fixtureSha256']:
        raise SystemExit('Generate the scene manifest before capturing; fixture content changed.')
    if batch.get('characterSha256'):
        name = batch.get('character', '')
        if not re.fullmatch(r'[a-z0-9_]+', name):
            raise SystemExit('Invalid installed character name.')
        config = json.loads((ROOT / 'config/local.json').read_text(encoding='utf-8-sig'))
        character = Path(config['gameRoot']) / 'data/lcv' / (name + '.bic')
        if hashlib.sha256(character.read_bytes()).hexdigest() != batch['characterSha256']:
            raise SystemExit('Installed character changed; regenerate this PC context before capture.')
    suffix = '_all' if args.engine == 'nwn' else ''
    folder = ROOT / 'out/visual/shots' / ('vis_' + args.scene + suffix) / args.engine
    # Only this exact generated scene directory may be cleaned; never a computed ancestor.
    allowed = (ROOT / 'out/visual/shots').resolve()
    if not folder.resolve().is_relative_to(allowed):
        raise SystemExit('Refusing a capture outside the spell batch output tree.')
    folder.mkdir(parents=True, exist_ok=True)
    for path in folder.glob('*.png'):
        path.unlink()
    # These are the exact native artifacts for this already validated batch folder.
    # A later Windows-only run must not inherit raw evidence from an older run.
    for path in list(folder.glob('*.tga')) + [folder / 'native-framebuffer.jsonl']:
        if path.exists(): path.unlink()
    waveform = (ROOT / 'out' / ('vis_' + args.scene + '_all.wav')
                if args.engine == 'nwn' else folder / 'audio.wav')
    audio_metadata = waveform.with_suffix('.json')
    # These are exact artifacts for this generated scene, not directory removal.
    for path in (waveform, audio_metadata):
        if path.exists(): path.unlink()
    manifest = OUT / 'captures' / (args.scene + '.' + args.engine + '.json')
    if manifest.exists(): manifest.unlink()
    revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    dirty = subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT, text=True).splitlines()
    command = ([sys.executable, 'tools/visual/spellvoice_audio.py', args.scene, 'all'] if args.engine == 'nwn'
               else [sys.executable, 'tools/visual/visual.py', 'run', args.scene, '--only', 'openwinter',
                     '--profile', 'plain',
                     '--openwinter-args=--visual-delay 0 --visual-audio --visual-graphics Glow=false;Ssao=false'])
    if native_framebuffer:
        command.append('--native-framebuffer')
    result = subprocess.run(command, cwd=ROOT)
    expected = {f's{row["id"]:03d}_{phase}.png' for row in batch['rows'] for phase in row['phases']}
    images = inspect_frames(folder, expected, native_framebuffer)
    shots, blank = images['shots'], images['blankShots']
    audio, notes = inspect_audio(waveform, audio_metadata)
    if not notes and args.engine == 'openwinter' and (folder / 'godot.log').exists():
        notes = [[None, line] for line in (folder / 'godot.log').read_text(encoding='utf-8').splitlines()]
    observations = {}
    for row in batch['rows']:
        spell = row['id']
        events = [(when, line.split('VISUAL|note|', 1)[-1].strip()) for when, line in notes if 'VISUAL|note|' in line]
        observations[str(spell)] = trigger_observations(events, spell)
    unproven = [row['id'] for row in batch['rows'] if not observations[str(row['id'])]['executionEvidence']]
    context_failures = context_mismatches(batch['rows'], observations)
    visual_complete = result.returncode == 0 and images['complete']
    scene_done = any('VISUAL|done|' in line for _, line in notes)
    record = dict(scene=args.scene, engine=args.engine, revision=revision, started=started, unprovenTriggers=unproven,
                  fixtureSha256=batch['fixtureSha256'], profile='plain', size=[1280, 720], workingTreeChanges=dirty,
                  captureCommand=command, renderConfigurationVersion=2,
                  requestedGraphics=({'nativePostProcessing': 'plain'} if args.engine == 'nwn'
                                     else {'Glow': False, 'Ssao': False}),
                  exitCode=result.returncode, shots=shots, blankShots=blank, folder=str(folder), observations=observations,
                  visualComplete=visual_complete, sceneDone=scene_done, contextFailures=context_failures,
                  complete=visual_complete and audio['valid'] and scene_done and not context_failures)
    record['imageSource'] = 'native-framebuffer' if native_framebuffer else ('window' if args.engine == 'nwn' else 'client-framebuffer')
    record['nativeFramebuffer'] = images['nativeFramebuffer']
    record['imageErrors'] = images['imageErrors']
    record['audio'] = audio
    write(manifest, record)
    if unproven: print('Execution remains unproven for rows: ' + ', '.join(map(str, unproven)))
    if context_failures: print('Fixture context mismatch: ' + json.dumps(context_failures))
    if images['imageErrors']: print('Image errors: ' + json.dumps(images['imageErrors']))
    if native_framebuffer and not images['nativeFramebuffer']['valid']:
        print('Native frame errors: ' + json.dumps(images['nativeFramebuffer']['errors']))
    if not record['complete']:
        raise SystemExit(f'Incomplete capture: {len(shots)}/{len(expected)} frames, {len(blank)} blank, '
                         f'audio valid={audio["valid"]}, scene done={scene_done}, exit {result.returncode}')
    print(manifest)


def ledger(args):
    with (OUT / 'stock-spells.tsv').open(encoding='utf-8') as stream:
        rows = list(csv.DictReader(stream, delimiter='\t'))
    profiles = read_optional(ROOT / 'parity/visual/spell_profiles.json')
    reviews = read_optional(ROOT / 'parity/visual/spell_reviews.json')
    batches = {}
    for path in sorted((OUT / 'batches').glob('*.json')):
        batch = read_optional(path)
        for row in batch['rows']:
            batches.setdefault(row['id'], []).append((batch, row))
    result = []
    audio_cache = {}
    for row in rows:
        spell = int(row['row'])
        record = dict(id=spell, label=val(row, 'Label'), profile={**default_profile(row), **profiles.get(row['row'], {})},
                      axes={a: 'unverified' if val(row, 'Label') else 'not-applicable' for a in AXES}, evidence=[])
        for batch, capture in batches.get(spell, []):
            record.setdefault('contexts', []).append(dict(scene=batch['scene'],
                fixtureSha256=batch['fixtureSha256'], profile=capture['profile']))
            captured = set()
            for engine in ('nwn', 'openwinter'):
                run = read_optional(OUT / 'captures' / (batch['scene'] + '.' + engine + '.json'))
                if not run or run.get('fixtureSha256') != batch['fixtureSha256']:
                    continue
                record.setdefault('triggerEvidence', []).append(dict(engine=engine, scene=batch['scene'],
                    fixtureSha256=run['fixtureSha256'], observations=run.get('observations', {}).get(str(spell), {}),
                    note='Retained independently of blank visual frames; not parity approval.'))
                folder = Path(run['folder'])
                present = {}
                for phase in capture['phases']:
                    name = f's{spell:03d}_{phase}.png'
                    path = folder / name
                    if (name not in run.get('blankShots', []) and path.exists()
                            and run['shots'].get(name) == hashlib.sha256(path.read_bytes()).hexdigest()
                            and trusted_native_frame(run, name)):
                        present[phase] = str(path)
                        captured.add(phase)
                if present:
                    record['evidence'].append(dict(scene=batch['scene'], engine=engine, shots=present,
                        captureRevision=run['revision'], fixtureSha256=run['fixtureSha256'],
                        imageSource=run.get('imageSource', 'window' if engine == 'nwn' else 'client-framebuffer'),
                        nativeFrames={phase: run['nativeFramebuffer']['frames'][f's{spell:03d}_{phase}.png'] for phase in present}
                            if run.get('imageSource') == 'native-framebuffer' else {},
                        observations=run.get('observations', {}).get(str(spell), {}),
                        status='captured-unreviewed', completeBatch=run['complete'],
                        note='Capture presence does not prove a legal trigger, script impact, or parity.'))
                cache_key = (batch['scene'], engine)
                if cache_key not in audio_cache:
                    audio_cache[cache_key] = trusted_audio(run)
                audio_interval = interval_evidence(audio_cache[cache_key], spell, capture['span'])
                if audio_interval:
                    record['evidence'].append(dict(engine=engine, scene=batch['scene'], fixtureSha256=batch['fixtureSha256'], **audio_interval))
                    # Capturing a waveform, even a nonzero one, does not review its
                    # cues. Leave the audio axis unverified until authored review.
            stage_axes = {'conjure': ('animation', 'casterFx'), 'release': ('animation', 'projectile'),
                          'impact': ('targetFx',), 'late': ('targetFx',), 'cleanup': ('cleanup',)}
            for phase in captured:
                for axis in stage_axes.get(phase, ('targetFx',) if phase == 'spawn' else ('projectile', 'targetFx')): record['axes'][axis] = 'captured-unreviewed'
        review = reviews.get(row['row'], {})
        record['axes'].update(review.get('axes', {}))
        record['evidence'] += review.get('evidence', [])
        result.append(record)
    write(OUT / 'presentation-ledger.json', dict(note='Each axis needs explicit reviewed evidence. No automatic parity approval.',
          source=read_optional(OUT / 'inventory-source.json'), rows=result))
    print(f'Wrote {len(result)} rows; verified axes are exclusively authored review annotations.')


def contact(args):
    from PIL import Image, ImageDraw
    batch = read_optional(OUT / 'batches' / (args.scene + '.json'))
    phases = ('conjure', 'impact', 'cleanup')
    canvas = Image.new('RGB', (1440, len(batch['rows']) * 170), '#202020')
    draw = ImageDraw.Draw(canvas)
    for y, row in enumerate(batch['rows']):
        for side, engine in enumerate(('nwn', 'openwinter')):
            run = read_optional(OUT / 'captures' / (args.scene + '.' + engine + '.json'))
            for x, phase in enumerate(phases):
                name = f's{row["id"]:03d}_{phase}.png'
                path = Path(run['folder']) / name if run.get('folder') else None
                left, top = (side * 3 + x) * 240, y * 170
                valid = (run.get('fixtureSha256') == batch['fixtureSha256']
                         and path is not None and path.exists()
                         and name not in run.get('blankShots', [])
                         and run.get('shots', {}).get(name) == hashlib.sha256(path.read_bytes()).hexdigest()
                         and trusted_native_frame(run, name))
                if valid:
                    with Image.open(path) as image: canvas.paste(image.resize((240, 135)), (left, top + 30))
                else:
                    draw.text((left + 3, top + 85), 'No valid recorded frame', fill='#ffaaaa')
                draw.text((left + 3, top + 2), f'{row["id"]} {row["label"][:20]}', fill='white')
                draw.text((left + 3, top + 16), engine + ' ' + phase, fill='#bbbbbb')
    destination = OUT / 'contacts' / (args.scene + '.jpg')
    destination.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(destination, quality=88)
    print(destination)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    generate_parser = commands.add_parser('generate')
    generate_parser.add_argument('--start', type=int, default=0)
    generate_parser.add_argument('--end', type=int, default=839)
    generate_parser.add_argument('--batch-size', type=int, default=6)
    generate_parser.add_argument('--close-caster', action='store_true', help='Closer conjure/post-conjure frames, then wide projectile view')
    generate_parser.add_argument('--prefix', default='spb', help='Distinct context batch prefix (1..8 lowercase ASCII characters)')
    generate_parser.add_argument('--ids', help='Optional comma-separated explicit row IDs within the selected range')
    commands.add_parser('ledger')
    capture_parser = commands.add_parser('capture')
    capture_parser.add_argument('scene')
    capture_parser.add_argument('--engine', required=True, choices=['nwn', 'openwinter'])
    capture_parser.add_argument('--native-framebuffer', action='store_true', help='Use native raw frames instead of Windows screenshots')
    contact_parser = commands.add_parser('contact')
    contact_parser.add_argument('scene')
    args = parser.parse_args()
    {'generate': generate, 'ledger': ledger, 'contact': contact, 'capture': capture}[args.command](args)
