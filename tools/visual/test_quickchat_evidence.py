import json
import hashlib
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from summarize_quickchat import summarize


class QuickChatEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name)/'capture.json'
        self.paths = Path(self.temp.name)/'paths.json'
        wave = Path(self.temp.name)/'capture.wav'
        wave.write_bytes(b'synthetic test input')
        self.doc = dict(scene='voiceactors_quickchat_slots', engine='native', validated=True,
                        recording=dict(started=0, path=str(wave), session=dict(restored=True)),
                        fixtureSha256={}, notes=[])
        slots = [0, 1, *range(4, 11), *range(22, 32), *range(34, 49)]
        inventory = [dict(index=i, slot=s, path='VWE', label=f'Slot{s}') for i, s in enumerate(slots)]
        with self.paths.open('w', encoding='utf-8', newline='') as f:
            json.dump(inventory, f)
        self.doc['fixtureSha256']['paths.json'] = hashlib.sha256(self.paths.read_bytes()).hexdigest()
        self.rows = [dict(family=f'slot{s}', onsets=[84+.3*s], durations=[.12]) for s in range(49)]
        self.note(4, 'quickchat.ready.1.pc.1.name.Aluvian.soundset.5100.master.1')
        for i, s in enumerate(slots):
            self.note(6+2*i, f'quickchat.start.{i}.slot.{s}')
            self.note(7.5+2*i, f'quickchat.end.{i}')
            for kind, value in ((18, 0), (19, 87), (19, 69)):
                self.note(6.5+2*i, f'quickchat.gui.trial.{i}.type.{kind}.integer.{value}.pc.1')
            command = {0: -3, 4: -4, 9: -7, 10: -2, 22: -5}.get(s)
            if command is not None:
                self.note(6.7+2*i, f'quickchat.order.trial.{i}.command.{command}.pattern.{command}.masterPC.1')
            actual = 3 if s == 1 else s
            self.rows[actual]['onsets'].insert(0, 6.7+2*i)
            self.rows[actual]['durations'].insert(0, .12)
        for s in range(49):
            self.note(84+.3*s, f'positive.slot.{s}')
        self.note(100, 'quickchat.completed.34')

    def note(self, time, value):
        self.doc['notes'].append([time, 'VISUAL|note|'+value])

    def run_summary(self):
        with self.path.open('w', encoding='utf-8', newline='') as f:
            json.dump(self.doc, f)
        with patch('summarize_quickchat.subprocess.check_output', return_value=json.dumps(self.rows)):
            return summarize(self.path, self.paths)

    def test_all_paths_and_battlecry_variant_identify(self):
        result = self.run_summary()
        self.assertEqual(len(result['entries']), 34)
        self.assertTrue(result['allMenuSamplesMatched'])
        self.assertTrue(result['allGuiEventsMatched'])
        self.assertTrue(result['allAssociateEventsMatched'])
        self.assertEqual(result['entries'][1]['cues'][0]['slot'], 3)

    def test_changed_path_inventory_rejects_capture(self):
        self.doc['fixtureSha256']['paths.json'] = 'changed'
        with self.assertRaisesRegex(ValueError, 'inventory does not match'):
            self.run_summary()

    def test_missing_reference_rejects_capture(self):
        self.rows[18]['onsets'] = []
        with self.assertRaisesRegex(ValueError, 'reference'):
            self.run_summary()

    def test_wrong_menu_sample_remains_unmatched(self):
        self.rows[0]['onsets'][0] = 2
        result = self.run_summary()
        self.assertFalse(result['allMenuSamplesMatched'])
        self.assertFalse(result['entries'][0]['sampleMatched'])

    def test_extra_cue_does_not_count_as_clean_identity(self):
        self.rows[18]['onsets'].insert(0, 6.8)
        self.rows[18]['durations'].insert(0, .12)
        self.assertFalse(self.run_summary()['entries'][0]['sampleMatched'])

    def test_successful_selection_must_not_emit_close(self):
        self.note(6.8, 'quickchat.gui.trial.0.type.20.integer.0.pc.1')
        self.assertFalse(self.run_summary()['entries'][0]['guiMatched'])

    def test_missing_order_remains_unmatched(self):
        self.doc['notes'] = [n for n in self.doc['notes'] if 'quickchat.order.trial.0.' not in n[1]]
        self.assertFalse(self.run_summary()['entries'][0]['associateMatched'])

    def test_raw_callback_identity_is_retained(self):
        self.note(6.65, 'quickchat.gui.trial.0.type.19.integer.101.pc.1')
        self.note(6.7, 'quickchat.order.trial.0.command.-3.pattern.-3.masterPC.1')
        result = self.run_summary()
        self.assertEqual(len(result['entries'][0]['guiEvents']), 4)
        self.assertFalse(result['entries'][0]['guiMatched'])
        self.assertEqual(len(result['entries'][0]['associateEvents']), 2)
        self.assertFalse(result['entries'][0]['associateMatched'])


if __name__ == '__main__':
    unittest.main()
