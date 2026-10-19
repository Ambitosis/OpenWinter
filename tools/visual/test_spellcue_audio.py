import unittest
import hashlib
import json
from pathlib import Path
import tempfile
from unittest.mock import patch
import numpy as np
from check_spellcue_audio import check
from spellcue_audio import execution_gates


class SpellCueExecutionTests(unittest.TestCase):
    def setUp(self):
        self.log = """VISUAL|done|1
VISUAL|note|ready.1
VISUAL|note|cue.completed
VISUAL|note|positive.before.spr_magicmissile
VISUAL|note|positive.after.spr_magicmissile
VISUAL|note|state.before.pc.1.name.Aluvian Darkstar.wizard.1.gender.1.soundset.227
VISUAL|note|state.before.slot.107.ready.1.known.1
VISUAL|note|state.after.slot.107.ready.0.known.1
VISUAL|note|state.before.target.1.seen.1.enemy.1.plot.0.difficulty.1.hp.10012
VISUAL|note|state.after.target.1.seen.1.enemy.1.plot.0.difficulty.1.hp.10008
VISUAL|note|execution.hooks.1.hits.1.events.1.finished.1
"""

    def valid(self, log):
        return execution_gates(log, 'spr_magicmissile', 107)[0]

    def test_real_execution_and_actual_difficulty(self):
        result = execution_gates(self.log, 'spr_magicmissile', 107)
        self.assertTrue(result[0])
        self.assertEqual(result[2], ['1', '1'])

    def test_invulnerable_target_rejected(self):
        self.assertFalse(self.valid(self.log.replace('plot.0', 'plot.1')))
        self.assertFalse(self.valid(self.log.replace('hits.1', 'hits.0')))

    def test_different_actor_or_spell_rejected(self):
        self.assertFalse(self.valid(self.log.replace('name.Aluvian Darkstar', 'name.Test')))
        self.assertFalse(self.valid(self.log.replace('slot.107', 'slot.144')))

    def test_missing_consumption_or_reference_rejected(self):
        self.assertFalse(self.valid(self.log.replace('after.slot.107.ready.0', 'after.slot.107.ready.1')))
        self.assertFalse(self.valid(self.log.replace('positive.after.', 'missing.after.')))

    def test_changed_difficulty_rejected(self):
        self.assertFalse(self.valid(self.log.replace('difficulty.1.hp.10008', 'difficulty.3.hp.10008')))

    def test_script_failure_after_completion_rejected(self):
        self.assertFalse(self.valid(self.log+'INVALID_LATE\n'))
        self.assertFalse(self.valid(self.log+'[script error]\n'))


class SpellCueIdentityTests(unittest.TestCase):
    def setUp(self):
        original = SpellCueExecutionTests()
        original.setUp()
        self.log = original.log
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        folder = Path(self.temp.name)
        self.metadata = folder/'audio.json'
        self.waveform = folder/'audio.wav'
        self.reference = folder/'spr_magicmissile.wav'
        self.scriptlog = folder/'native.log'
        self.waveform.write_bytes(b'owned capture')
        self.reference.write_bytes(b'original reference')
        self.scriptlog.write_text(self.log, encoding='utf-8', newline='')
        self.digest = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
        scene = Path(__file__).resolve().parents[2]/'parity/visual/scenes/cuemmproj/scene.json'
        self.doc = dict(recording=dict(path=str(self.waveform), started=100, session=dict(restored=True)),
                        notes=[[102,'VISUAL|note|positive.before.spr_magicmissile'],
                               [110,'VISUAL|note|start.107'],[120,'VISUAL|note|positive.after.spr_magicmissile']],
                        probe=dict(validated=True, engine='native', candidate='spr_magicmissile',
                                   scene='cuemmproj', substitutions=[], waveformSha256=self.digest(self.waveform),
                                   scriptLog=str(self.scriptlog), scriptLogSha256=self.digest(self.scriptlog),
                                   fixtureSha256={'scene.json':self.digest(scene)}))

    def analyze(self, identities=None):
        self.metadata.write_text(json.dumps(self.doc), encoding='utf-8', newline='')
        if identities is None:
            identities = [dict(status='identified') for _ in range(3)]
        with patch('check_spellcue_audio.matcher.decode', return_value=np.zeros(30*44100)), \
             patch('check_spellcue_audio.matcher.identify_candidates', side_effect=identities):
            return check(self.metadata, self.reference)

    def test_independent_positives_do_not_turn_unresolved_cast_into_match(self):
        report = self.analyze([dict(status='identified'),dict(status='unresolved'),dict(status='identified')])
        self.assertTrue(report['independentReferencesIdentified'])
        self.assertEqual(report['intervals'][1]['identity']['status'], 'unresolved')

    def test_missing_positive_is_explicit(self):
        report = self.analyze([dict(status='unresolved'),dict(status='identified'),dict(status='identified')])
        self.assertFalse(report['independentReferencesIdentified'])

    def test_candidate_substitution_rejected(self):
        self.doc['probe']['substitutions'] = [dict(resref='spr_magicmissile')]
        with self.assertRaisesRegex(ValueError,'cannot be replaced'):
            self.analyze()

    def test_changed_waveform_or_log_rejected(self):
        self.waveform.write_bytes(b'changed')
        with self.assertRaisesRegex(ValueError,'waveform changed'):
            self.analyze()
        self.doc['probe']['waveformSha256'] = self.digest(self.waveform)
        self.scriptlog.write_text('changed',encoding='utf-8',newline='')
        with self.assertRaisesRegex(ValueError,'log changed'):
            self.analyze()

    def test_metadata_success_does_not_override_bad_execution_log(self):
        self.scriptlog.write_text(self.log.replace('hits.1','hits.0'),encoding='utf-8',newline='')
        self.doc['probe']['scriptLogSha256'] = self.digest(self.scriptlog)
        with self.assertRaisesRegex(ValueError,'actual-player gates'):
            self.analyze()

    def test_duplicate_cast_marker_rejected(self):
        self.doc['notes'].append([111,'VISUAL|note|start.107'])
        with self.assertRaisesRegex(ValueError,'Exactly one marker'):
            self.analyze()

    def test_unrestored_native_session_rejected(self):
        self.doc['recording']['session']['restored'] = False
        with self.assertRaisesRegex(ValueError,'restoration required'):
            self.analyze()


if __name__ == '__main__':
    unittest.main()
