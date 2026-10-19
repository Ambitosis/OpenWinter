"""Prepare actual stock-PC casting controls; no BIC or game data is modified.

The original Aluvian character has Mage Armor/Ray prepared and knows Magic
Missile; public NWScript prepares the latter into an existing legal slot. Each
weapon context starts from that same original BIC. This is fixture preparation,
never automatic parity approval.
"""
import hashlib
import json
import math
import subprocess
from pathlib import Path
import spell_evidence as evidence


OBSERVE = '''
void PlayerState(int spell,string phase){
    Observe(spell,phase);
    object pc=GetLocalObject(GetModule(),"spb_caster");
    string key="state."+IntToString(spell)+"."+phase+".";
    Note(key+"pc."+IntToString(GetIsPC(pc)));
    Note(key+"name."+GetName(pc));
    Note(key+"wizard_level."+IntToString(GetLevelByClass(CLASS_TYPE_WIZARD,pc)));
    Note(key+"gender."+IntToString(GetGender(pc)));
    Note(key+"soundset."+IntToString(GetSoundset(pc)));
    object hand=GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,pc);
    Note(key+"right_valid."+IntToString(GetIsObjectValid(hand)));
    if(GetIsObjectValid(hand)){Note(key+"right_resref."+GetResRef(hand));Note(key+"right_baseitem."+IntToString(GetBaseItemType(hand)));}
    Note(key+"left_valid."+IntToString(GetIsObjectValid(GetItemInSlot(INVENTORY_SLOT_LEFTHAND,pc))));
    if(spell!=318){
        int level=1;int slot=0;
        if(spell==107)slot=1;
        if(spell==144){level=0;slot=2;}
        Note(key+"slot_spell."+IntToString(GetMemorizedSpellId(pc,CLASS_TYPE_WIZARD,level,slot)));
        Note(key+"slot_ready."+IntToString(GetMemorizedSpellReady(pc,CLASS_TYPE_WIZARD,level,slot)));
        Note(key+"known."+IntToString(GetIsInKnownSpellList(pc,CLASS_TYPE_WIZARD,spell)));
    }
}
void EquipOriginalDagger(object pc){
    object weapon=GetItemPossessedBy(pc,"NW_WSWDG001");
    Note("fixture.dagger_found."+IntToString(GetIsObjectValid(weapon)));
    AssignCommand(pc,ActionEquipItem(weapon,INVENTORY_SLOT_RIGHTHAND));
}
'''


def generate():
    rows = {int(row['row']): row for row in evidence.stock_rows()}
    config = json.loads((evidence.ROOT / 'config/local.json').read_text(encoding='utf-8-sig'))
    character = Path(config['gameRoot']) / 'data/lcv/aluviandarkstar.bic'
    character_hash = hashlib.sha256(character.read_bytes()).hexdigest()
    for equipped in (False, True):
        scene = ('pcblade' if equipped else 'pcalu') + '102'
        directory = evidence.generated_directory(scene)
        profiles = []
        for spell in (102, 107, 144, 318):
            profile = dict(target='self' if spell in (102, 318) else 'enemy', caster='pc', cheat=False,
                distance=6, status='authored-awaiting-execution', character='aluviandarkstar',
                weaponContext='equipped-original-dagger' if equipped else 'original-empty-hands',
                reason='Original installed Aluvian Wizard1 PC; known Magic Missile prepared into existing level1 slot1 using public NWScript; genuine non-cheat actions and phase observations.')
            profile['expectedBefore'] = dict(pc=1,name='Aluvian Darkstar',wizard_level=1,
                right_valid=int(equipped),left_valid=0)
            if equipped:
                profile['expectedBefore']['right_baseitem'] = 22
            if spell == 318:
                profile.update(trigger='feat', feat=303)
                profile['expectedBefore']['feat_uses'] = 1
            else:
                profile['expectedBefore'].update(slot_spell=spell,slot_ready=1,known=1)
            profiles.append(profile)
        script = evidence.PREAMBLE + OBSERVE + '\n'
        for ordinal, (spell, profile) in enumerate(zip((102, 107, 144, 318), profiles)):
            script += evidence.make_cast(rows[spell], profile, ordinal).replace(
                f'Observe({spell},"before");', f'PlayerState({spell},"before");') + '\n'
        script += 'void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);\n'
        script += 'SetLocalString(GetModule(),"X2_S_UD_SPELLSCRIPT","spb_hook");Note("difficulty."+IntToString(GetGameDifficulty()));\n'
        script += 'Note("fixture.original_slot1."+IntToString(GetMemorizedSpellId(pc,CLASS_TYPE_WIZARD,1,1)));\n'
        script += 'Note("fixture.magic_missile_known."+IntToString(GetIsInKnownSpellList(pc,CLASS_TYPE_WIZARD,107)));\n'
        script += 'SetMemorizedSpell(pc,CLASS_TYPE_WIZARD,1,1,107,TRUE);\n'
        script += 'DelayCommand(1.0,VCamera(pc,90.0,5.0,65.0));\n'
        if equipped:
            script += 'DelayCommand(.5,EquipOriginalDagger(pc));\n'
        cursor, records = 2.25, []
        for ordinal, (spell, profile) in enumerate(zip((102, 107, 144, 318), profiles)):
            preparation = 1 if profile['target'] == 'self' else 6
            now = cursor + .75 + preparation
            span = 8
            phases = dict(conjure=.75, release=2.1, impact=3.3, late=4.7, cleanup=7.5,
                          body_025=.25, body_125=1.25, body_160=1.6, body_185=1.85,
                          body_260=2.6, body_310=3.1)
            if spell == 318:
                phases.update(release=.75, impact=1.25, spawn=2.5)
            script += f'DelayCommand({cursor:.2f},Clean(pc));\nDelayCommand({cursor+.75:.2f},Prepare{ordinal}(pc));\n'
            script += f'DelayCommand({now:.2f},Cast{ordinal}(pc));\n'
            for phase, when in phases.items():
                script += f'DelayCommand({now+when:.2f},VShot("s{spell:03d}_{phase}"));\n'
                script += f'DelayCommand({now+when:.2f},PlayerState({spell},"{phase}"));\n'
            script += f'DelayCommand({now+span-1.5:.2f},Note("forced_cleanup.{spell}"));\n'
            script += f'DelayCommand({now+span-1.5:.2f},Clean(pc));\n'
            records.append(dict(id=spell,label=evidence.val(rows[spell],'Label'),profile=profile,phases=phases,
                start=now,span=span,preparationSeconds=preparation,
                cameraPlan=[dict(at='throughout',direction=90,distance=5,pitch=65)],
                cleanup='forced fixture removal; PC remains; not natural duration expiry'))
            cursor = now + span
        script += f'DelayCommand({cursor:.2f},VDone());}}\n'
        evidence.write(directory / 'vis_main.nss', script)
        evidence.write(directory / 'spb_hook.nss', '#include "visual_inc"\nvoid main(){WriteTimestampedLogEntry("VISUAL|note|impact."+IntToString(GetSpellId()));}\n')
        evidence.write(directory / 'spb_event.nss', 'void main(){WriteTimestampedLogEntry("VISUAL|note|spell_event."+IntToString(GetLocalInt(GetModule(),"spb_active"))+"."+IntToString(GetLastSpell())+"."+GetResRef(OBJECT_SELF));}\n')
        evidence.write(directory / 'spb_damage.nss', 'void main(){WriteTimestampedLogEntry("VISUAL|note|damage_event."+IntToString(GetLocalInt(GetModule(),"spb_active"))+"."+IntToString(GetTotalDamageDealt())+"."+GetResRef(OBJECT_SELF));}\n')
        evidence.write(directory / 'scene.json', dict(character='aluviandarkstar',tileset='tde01',tile=4,size=4,
            pc=[20,14,0],facing=90,seconds=math.ceil(cursor+4),difficulty=3,
            characterSha256=character_hash))
        batch = dict(scene=scene,rows=records,seconds=math.ceil(cursor+4),character='aluviandarkstar',
            characterSha256=character_hash,fixtureSha256=evidence.fixture_hash(scene),
            revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=evidence.ROOT,text=True).strip())
        evidence.write(evidence.OUT / 'batches' / (scene+'.json'), batch)
        print(scene,batch['seconds'],batch['fixtureSha256'])


if __name__ == '__main__':
    generate()
