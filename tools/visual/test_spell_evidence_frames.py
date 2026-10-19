import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from PIL import Image
from spell_evidence_frames import digest, inspect_frames, trusted_native_frame


class NativeSpellFrames(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.folder = Path(self.tmp.name)
        self.name = 's001_conjure.png'
        self.make_frame((12, 34, 56))

    def make_frame(self, color):
        raw = self.folder / 's001_conjure.tga'
        png = self.folder / self.name
        window = self.folder / 's001_conjure_window.png'
        Image.new('RGB', (4, 3), color).save(raw)
        with Image.open(raw) as image:
            image.save(png)
        Image.new('RGB', (4, 3)).save(window)
        self.record = dict(shot='s001_conjure', source='native_0000.tga', size=[4, 3],
                           rawSha256=digest(raw), windowSha256=digest(window),
                           requestedUnixSeconds=100.0, latencySeconds=.1, nonblack=any(color))
        self.write_records([self.record])

    def write_records(self, records):
        (self.folder / 'native-framebuffer.jsonl').write_text(
            '\n'.join(json.dumps(r) for r in records) + '\n', encoding='utf-8', newline='')

    def inspect(self):
        return inspect_frames(self.folder, {self.name}, True)

    def test_black_window_negative_does_not_reject_valid_native_frame(self):
        result = self.inspect()
        self.assertTrue(result['complete'])
        self.assertEqual(set(result['shots']), {self.name})
        self.assertEqual(result['blankShots'], [])
        run = dict(result, imageSource='native-framebuffer', folder=str(self.folder))
        self.assertTrue(trusted_native_frame(run, self.name))

    def test_decoded_png_must_match_retained_native_pixels(self):
        Image.new('RGB', (4, 3), (99, 34, 56)).save(self.folder / self.name)
        result = self.inspect()
        self.assertFalse(result['complete'])
        self.assertIn('does not decode', result['nativeFramebuffer']['errors'][0])

    def test_duplicate_or_missing_native_phase_records_are_rejected(self):
        for records in ([], [self.record, self.record]):
            with self.subTest(records=len(records)):
                self.write_records(records)
                self.assertFalse(self.inspect()['complete'])

    def test_raw_or_metadata_changes_invalidate_ledger_provenance(self):
        result = self.inspect()
        run = dict(result, imageSource='native-framebuffer', folder=str(self.folder))
        raw = self.folder / 's001_conjure.tga'
        original = raw.read_bytes()
        raw.write_bytes(original + b'changed')
        self.assertFalse(trusted_native_frame(run, self.name))
        raw.write_bytes(original)
        self.record['latencySeconds'] = .2
        self.write_records([self.record])
        self.assertFalse(trusted_native_frame(run, self.name))

    def test_black_primary_frame_remains_rejected(self):
        self.make_frame((0, 0, 0))
        result = self.inspect()
        self.assertTrue(result['nativeFramebuffer']['valid'])
        self.assertFalse(result['complete'])
        self.assertEqual(result['blankShots'], [self.name])

    def test_missing_raw_frame_cannot_pass_from_png_alone(self):
        (self.folder / 's001_conjure.tga').unlink()
        self.assertFalse(self.inspect()['complete'])

    def test_unexpected_primary_frames_are_not_silently_ignored(self):
        Image.new('RGB', (4, 3), 'red').save(self.folder / 'stale.png')
        self.assertFalse(self.inspect()['complete'])

    def test_only_expected_window_comparisons_are_excluded(self):
        Image.new('RGB', (4, 3)).save(self.folder / 'stale_window.png')
        self.assertFalse(self.inspect()['complete'])

    def test_legacy_captures_do_not_require_native_artifacts(self):
        for path in self.folder.glob('*'):
            if path.name != self.name:
                path.unlink()
        result = inspect_frames(self.folder, {self.name})
        self.assertTrue(result['complete'])
        self.assertTrue(trusted_native_frame(dict(folder=str(self.folder)), self.name))

    def test_ledger_rechecks_native_raw_hash_without_approving_parity(self):
        import spell_evidence as evidence
        result = self.inspect()
        run = dict(result, imageSource='native-framebuffer', folder=str(self.folder),
                   revision='test', fixtureSha256='fixture', observations={})
        root, out = self.folder / 'root', self.folder / 'ledger'
        with patch.object(evidence, 'ROOT', root), patch.object(evidence, 'OUT', out):
            evidence.write(out / 'stock-spells.tsv', 'row\tLabel\n1\tFixture\n')
            evidence.write(out / 'batches/probe001.json', dict(scene='probe001', fixtureSha256='fixture',
                rows=[dict(id=1, profile={}, phases={'conjure': .75}, span=8)]))
            evidence.write(out / 'captures/probe001.nwn.json', run)
            evidence.ledger(None)
            row = evidence.read_optional(out / 'presentation-ledger.json')['rows'][0]
            self.assertEqual(row['axes']['animation'], 'captured-unreviewed')
            self.assertNotIn('verified', row['axes'].values())
            self.assertEqual(row['evidence'][0]['nativeFrames']['conjure']['latencySeconds'], .1)
            (self.folder / 's001_conjure.tga').unlink()
            evidence.ledger(None)
            row = evidence.read_optional(out / 'presentation-ledger.json')['rows'][0]
            self.assertEqual(row['evidence'], [])
            self.assertEqual(row['axes']['animation'], 'unverified')


    def test_contact_uses_manifest_paths_and_rejects_stale_fixture_or_raw(self):
        import spell_evidence as evidence
        from types import SimpleNamespace
        result = self.inspect()
        run = dict(result, imageSource='native-framebuffer', folder=str(self.folder),
                   revision='test', fixtureSha256='fixture')
        root, out = self.folder / 'root', self.folder / 'contact'
        with patch.object(evidence, 'ROOT', root), patch.object(evidence, 'OUT', out):
            evidence.write(out / 'batches/probe001.json', dict(scene='probe001', fixtureSha256='fixture',
                rows=[dict(id=1, label='Fixture')]))
            def pixel():
                evidence.write(out / 'captures/probe001.nwn.json', run)
                evidence.contact(SimpleNamespace(scene='probe001'))
                with Image.open(out / 'contacts/probe001.jpg') as contact:
                    return contact.getpixel((120, 130))
            self.assertTrue(all(abs(a-b) < 3 for a,b in zip(pixel(), (12,34,56))))
            run['fixtureSha256'] = 'stale'
            self.assertEqual(pixel(), (32,32,32))
            run['fixtureSha256'] = 'fixture'
            (self.folder / 's001_conjure.tga').unlink()
            self.assertEqual(pixel(), (32,32,32))


if __name__ == '__main__':
    unittest.main()
