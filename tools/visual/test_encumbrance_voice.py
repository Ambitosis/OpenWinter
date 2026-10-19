import json
from pathlib import Path
import tempfile
import unittest

from summarize_encumbrance_voice import summarize


class EncumbranceEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        folder = Path(self.tmp.name)
        wave = folder/'audio.wav'
        wave.write_bytes(b'fixture audio hash placeholder')
        self.path = folder/'capture.json'
        texts = ['encumbered.positive.slot.32',
                 'encumbered.ready.1.pc.1.name.Aluvian Darkstar.soundset.5100.str.10.base.98.normal.860.heavy.1100.dartweight.1.dartstack.50']
        for phase, weight in [('normal',860), ('crossnormal',861), ('remain',871), ('reset1',98),
                              ('crossheavy',1101), ('reset2',98), ('prepickup',840), ('pickup',880),
                              ('reset3',98), ('recross',861)]:
            state = 2 if weight > 1100 else 1 if weight > 860 else 0
            texts.append(f'encumbered.state.{phase}.weight.{weight}.normal.860.heavy.1100.derived.{state}.expected.{weight}.str.10')
        texts += ['encumbered.pickup.complete.valid.1.owned.1.weight.880.acquire.1',
                  'encumbered.positive.slot.32', 'encumbered.completed']
        self.doc = dict(scene='voiceactors_encumbered_slots', validated=True, engine='native',
                        recording=dict(started=100, path=str(wave), session=dict(restored=True)),
                        notes=[[100+i, 'VISUAL|note|'+n] for i,n in enumerate(texts)], fixtureSha256={})
        self.tones = [dict(family='encumbered32', onsets=[.1,13.1], durations=[.12,.12])]

    def run_summary(self):
        self.path.write_text(json.dumps(self.doc), encoding='utf-8')
        return summarize(self.path, self.tones)

    def test_complete_context_keeps_silent_transitions_silent(self):
        result = self.run_summary()
        self.assertEqual([], result['automaticCues'])
        self.assertEqual(10, len(result['states']))

    def test_missing_reference_rejected(self):
        self.tones[0]['onsets'].pop()
        self.tones[0]['durations'].pop()
        with self.assertRaisesRegex(ValueError, 'positive controls'):
            self.run_summary()

    def test_failed_pickup_does_not_establish_silence(self):
        self.doc['notes'][12][1] = self.doc['notes'][12][1].replace('owned.1', 'owned.0')
        with self.assertRaisesRegex(ValueError, 'pickup ownership'):
            self.run_summary()

    def test_missing_threshold_state_rejected(self):
        self.doc['notes'].pop(3)
        with self.assertRaisesRegex(ValueError, 'threshold/reset'):
            self.run_summary()

    def test_explicit_context_failure_rejected(self):
        self.doc['notes'].append([120, 'VISUAL|note|INVALID_PICKUP_WEIGHT'])
        with self.assertRaisesRegex(ValueError, 'context/weight'):
            self.run_summary()

    def test_unrestored_audio_session_rejected(self):
        self.doc['recording']['session']['restored'] = False
        with self.assertRaisesRegex(ValueError, 'restored'):
            self.run_summary()

    def bag_context(self):
        texts = ['encumbered.positive.slot.32',
                 'encumberedbag.ready.1.pc.1.name.Aluvian Darkstar.soundset.5100.base.98.normal.860.heavy.1100']
        for phase, weight, state in [('directoverload',1458,2), ('directreset',98,0),
                                     ('encumbered',1008,1), ('carriedoverload',1458,2),
                                     ('carriedencumbered',1008,1), ('reset',98,0)]:
            texts.append(f'encumberedbag.state.{phase}.weight.{weight}.normal.860.heavy.1100.derived.{state}.expected.{state}')
        texts += ['encumberedbag.owned.1', 'encumberedbag.owned.1',
                  'encumberedbag.shield.valid.1.owned.1.weight.450',
                  'encumbered.positive.slot.32', 'encumberedbag.completed']
        self.doc['scene'] = 'voiceactors_encumberedbag_slots'
        self.doc['notes'] = [[100+i, 'VISUAL|note|'+n] for i,n in enumerate(texts)]
        self.tones[0]['onsets'] = [.1,11.1]

    def test_bag_context_requires_all_six_measured_states(self):
        self.bag_context()
        self.assertEqual(6, len(self.run_summary()['states']))

    def test_bag_context_rejects_nested_weight_assumption(self):
        self.bag_context()
        self.doc['notes'][5][1] = self.doc['notes'][5][1].replace('weight.1458', 'weight.1008')
        with self.assertRaisesRegex(ValueError, 'derivation inconsistent'):
            self.run_summary()

    def test_bag_context_requires_both_completed_pickups(self):
        self.bag_context()
        self.doc['notes'][9][1] = 'VISUAL|note|encumberedbag.owned.0'
        with self.assertRaisesRegex(ValueError, 'Both bag pickup'):
            self.run_summary()


    def test_drop_requires_completed_ground_ownership(self):
        self.bag_context()
        self.doc['scene'] = 'voiceactors_encumbereddrop_slots'
        with self.assertRaisesRegex(ValueError, 'ground drop'):
            self.run_summary()
        self.doc['notes'].append([120, 'VISUAL|note|encumberedbag.shield.ground.1'])
        self.assertEqual(6, len(self.run_summary()['states']))


if __name__ == '__main__':
    unittest.main()
