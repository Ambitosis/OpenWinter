import json
from pathlib import Path
import tempfile
import unittest
from summarize_neardeath_voice import summarize


class NearDeathEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup)
        self.path=Path(self.tmp.name)/'capture.json';wave=Path(self.tmp.name)/'audio.wav';wave.write_bytes(b'test')
        notes=['near.positive.slot.17','near.ready.1.pc.1.name.Aluvian Darkstar.soundset.5100.max.11']
        for phase,hp in [('set9',9),('set6',6),('set2',2),('set1',1),('reset1',11)]:
            notes.append(f'near.state.{phase}.hp.{hp}.max.11.dead.0.damage.0')
        notes+=['near.damage.event.1.phase.script.amount.10.hp.1','near.state.scriptlow.hp.1.max.11.dead.0.damage.1',
                'near.state.reset2.hp.11.max.11.dead.0.damage.1','near.combat.ready.1',
                'near.damage.event.2.phase.combat.amount.10.hp.1','near.state.combatlow.hp.1.max.11.dead.0.damage.2',
                'near.positive.slot.17','near.completed']
        self.doc=dict(scene='voiceactors_neardeath_slots',validated=True,engine='native',recording=dict(started=100,path=str(wave),session=dict(restored=True)),notes=[[100+i,'VISUAL|note|'+n] for i,n in enumerate(notes)],fixtureSha256={})
        self.tones=[dict(family='neardeath17',onsets=[.1,13.1],durations=[.12,.12])]

    def result(self):
        self.path.write_text(json.dumps(self.doc),encoding='utf-8')
        return summarize(self.path,self.tones)

    def test_complete_context_retains_bounded_silence(self):
        self.assertEqual([],self.result()['automaticCues'])
        self.assertEqual(8,len(self.result()['states']))

    def test_missing_sample_positive_is_not_silence(self):
        self.tones[0]['onsets'].pop();self.tones[0]['durations'].pop()
        with self.assertRaisesRegex(ValueError,'positives'):self.result()

    def test_failed_hp_context_is_rejected(self):
        self.doc['notes'][5][1]=self.doc['notes'][5][1].replace('hp.1','hp.2')
        with self.assertRaisesRegex(ValueError,'HP state'):self.result()

    def test_failed_hostility_is_rejected(self):
        self.doc['notes'][10][1]='VISUAL|note|near.combat.ready.0'
        with self.assertRaisesRegex(ValueError,'combat positive'):self.result()

    def test_script_damage_does_not_stand_in_for_melee(self):
        self.doc['notes'].pop(11)
        with self.assertRaisesRegex(ValueError,'ordinary combat'):self.result()

    def test_mortal_context_requires_explicit_nonimmortal_flag(self):
        self.doc['scene']='voiceactors_nearmortal_slots'
        self.doc['notes']=[r for r in self.doc['notes'] if not any(s in r[1] for s in ['reset2','combat','event.2'])]
        with self.assertRaisesRegex(ValueError,'Nonimmortal'):self.result()
        self.doc['notes'].append([120,'VISUAL|note|near.immortal.0'])
        self.assertTrue(self.result()['nonimmortal'])

    def test_unrestored_native_audio_session_rejected(self):
        self.doc['recording']['session']['restored']=False
        with self.assertRaisesRegex(ValueError,'restoration'):self.result()

    def test_startup_gate_may_precede_master_capture_but_requires_original_log(self):
        self.doc['scene']='voiceactors_nearmortal_slots'
        self.doc['engine']='client'
        self.doc['notes']=[r for r in self.doc['notes'] if not any(s in r[1] for s in ['reset2','combat','event.2'])]
        log=Path(self.tmp.name)/'godot.log'
        log.write_text('[log] VISUAL|note|near.immortal.0\n',encoding='utf-8')
        self.doc['scriptLog']=str(log)
        self.assertTrue(self.result()['nonimmortal'])
        self.assertIsNotNone(self.result()['scriptLogSha256'])
        log.write_text('[log] VISUAL|note|near.immortal.1\n',encoding='utf-8')
        with self.assertRaisesRegex(ValueError,'Nonimmortal'):self.result()

    def test_explicit_failure_rejected(self):
        self.doc['notes'].append([120,'VISUAL|note|INVALID_HP_CONTEXT'])
        with self.assertRaisesRegex(ValueError,'context failure'):self.result()


if __name__=='__main__':unittest.main()
