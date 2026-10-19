import json
from pathlib import Path
import tempfile
import unittest
from summarize_nearhench_voice import summarize


class StockHenchmanEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.path = Path(self.tmp.name) / 'capture.json'
        wave = Path(self.tmp.name) / 'audio.wav'
        wave.write_bytes(b'test')
        notes = ['nearhench.positive.slot.17', 'nearhench.ready.1.type.1.hp.1.max.12.masterPC.1']
        notes += [f'nearhench.attempt.{i}.hp.1.max.12.type.1.masterPC.1.bindings.1.result.0.afterHP.1' for i in range(1, 101)]
        notes += ['nearhench.positive.slot.17', 'nearhench.completed.100']
        self.doc = dict(scene='voiceactors_nearhench_slots', validated=True, engine='native',
                        recording=dict(started=100, path=str(wave), session=dict(restored=True)),
                        notes=[[100+i, 'VISUAL|note|'+n] for i, n in enumerate(notes)], fixtureSha256={})
        self.tones = [dict(family='neardeath17', onsets=[.1, 10.1, 102.1], durations=[.12]*3)]

    def result(self):
        self.path.write_text(json.dumps(self.doc), encoding='utf-8')
        return summarize(self.path, self.tones)

    def test_gated_script_call_is_distinct_from_automatic_player_trigger(self):
        report = self.result()
        self.assertEqual(100, report['observed']['eligibleCalls'])
        self.assertEqual(1, report['observed']['cues'])
        self.assertAlmostEqual(.95**100, report['sourceModel']['zeroProbabilityIfIndependent'])

    def test_missing_call_is_not_a_complete_probability_sample(self):
        self.doc['notes'].pop(50)
        with self.assertRaisesRegex(ValueError, 'contiguous'): self.result()

    def test_healed_target_is_ineligible(self):
        self.doc['notes'][50][1] = self.doc['notes'][50][1].replace('afterHP.1', 'afterHP.12')
        with self.assertRaisesRegex(ValueError, 'unhealed'): self.result()

    def test_lost_pc_master_is_ineligible(self):
        self.doc['notes'][50][1] = self.doc['notes'][50][1].replace('masterPC.1', 'masterPC.0')
        with self.assertRaisesRegex(ValueError, 'eligibility'): self.result()

    def test_missing_reference_is_not_silent_script(self):
        self.tones[0]['onsets'].pop()
        self.tones[0]['durations'].pop()
        with self.assertRaisesRegex(ValueError, 'positives'): self.result()

    def test_automatic_event_binding_invalidates_authored_call_attribution(self):
        self.doc['notes'][50][1] = self.doc['notes'][50][1].replace('bindings.1', 'bindings.0')
        with self.assertRaisesRegex(ValueError, 'eligibility'): self.result()

    def test_cue_outside_call_window_is_not_counted(self):
        self.tones[0]['onsets'].append(120)
        self.tones[0]['durations'].append(.12)
        with self.assertRaisesRegex(ValueError, 'outside'): self.result()


if __name__ == '__main__': unittest.main()
