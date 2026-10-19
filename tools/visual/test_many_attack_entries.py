import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from summarize_many_attack_entries import summarize


class AttackEntryEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name)/'capture.json'
        wave = Path(self.temp.name)/'capture.wav'
        wave.write_bytes(b'synthetic test input')
        self.doc = dict(scene='voiceactors_entrynpcmany8_slots', validated=True,
                        recording=dict(started=0, path=str(wave), session=dict(restored=True)),
                        fixtureSha256={}, notes=[])
        self.rows = []
        for actor in range(20):
            start = 9+1.5*actor
            self.note(start, f'entry.start.{actor}.ready.1.pc.0.apr.1')
            self.note(start+.2, f'entry.attack.{actor}.attackerPC.0')
            self.note(start+2.5, f'entry.stop.{actor}.attacks.1.dead.0')
            for slot in range(4):
                positive = 50+1.7*actor+.4*slot
                self.note(positive, f'positive.actor.{actor}.slot.{slot}')
                self.rows.append(dict(actor=actor, family='attack0' if slot == 0 else f'battlecry{slot}',
                                      onsets=[positive], durations=[.12]))
        self.note(41, 'entry.completed.20')

    def note(self, time, value):
        self.doc['notes'].append([time, 'VISUAL|note|'+value])

    def run_summary(self):
        with self.path.open('w', encoding='utf-8', newline='') as stream:
            json.dump(self.doc, stream)
        with patch('summarize_many_attack_entries.subprocess.check_output', return_value=json.dumps(self.rows)):
            return summarize(self.path)

    def test_silence_is_counted_only_after_every_positive(self):
        result = self.run_summary()
        self.assertEqual(len(result['entries']), 20)
        self.assertTrue(all(not e['cues'] for e in result['entries']))
        self.assertEqual(len(result['positiveControls']), 80)

    def test_voice_belongs_to_its_actor_even_when_windows_overlap(self):
        row = next(r for r in self.rows if r['actor'] == 1 and r['family'] == 'battlecry2')
        row['onsets'].insert(0, 10.6); row['durations'].insert(0, .12)
        result = self.run_summary()
        self.assertEqual(len(result['entries'][1]['cues']), 1)
        self.assertEqual(result['entries'][0]['cues'], [])

    def test_missing_positive_rejects_whole_count(self):
        self.rows[-1]['onsets'] = []
        with self.assertRaisesRegex(ValueError, 'eighty'):
            self.run_summary()

    def test_missing_attack_rejects_whole_count(self):
        self.doc['notes'] = [[t, n] for t, n in self.doc['notes'] if 'entry.attack.7.' not in n]
        with self.assertRaisesRegex(ValueError, 'callback'):
            self.run_summary()

    def test_duplicate_context_does_not_replace_missing_actor(self):
        self.doc['notes'] = [[t, n.replace('entry.start.7.', 'entry.start.6.')]
                             for t, n in self.doc['notes']]
        with self.assertRaisesRegex(ValueError, 'distinct'):
            self.run_summary()

    def test_unattributed_cue_remains_visible(self):
        row = self.rows[0]
        row['onsets'].insert(0, 2); row['durations'].insert(0, .12)
        self.assertEqual(len(self.run_summary()['unattributedPreControlCues']), 1)


if __name__ == '__main__':
    unittest.main()
