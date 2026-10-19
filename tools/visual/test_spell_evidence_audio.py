import json
from pathlib import Path
import struct
import tempfile
import unittest
import wave
from spell_evidence_audio import inspect_audio, trusted_audio, interval_evidence


class AudioArtifacts(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.wav = Path(self.folder.name)/'audio.wav'
        self.metadata = self.wav.with_suffix('.json')
        with wave.open(str(self.wav), 'wb') as out:
            out.setparams((1, 2, 8000, 0, 'NONE', 'not compressed'))
            out.writeframes(struct.pack('<h', 1234)*16000)
        self.document = dict(recording=dict(path=str(self.wav), started=100., frames=16000,
            rate=8000, channels=1, pcm_peak=1234), notes=[[100.5, 'VISUAL|note|start.6'], [102., 'VISUAL|done|1']])
        self.write_metadata()

    def write_metadata(self):
        self.metadata.write_text(json.dumps(self.document), encoding='utf-8', newline='')

    def test_audio_survives_rejected_images_without_claiming_identity(self):
        audio, notes = inspect_audio(self.wav, self.metadata)
        self.assertTrue(audio['valid'])
        run = dict(audio=audio, exitCode=0, complete=False, blankShots=['blank.png'])
        evidence = interval_evidence(trusted_audio(run), 6, 8)
        self.assertEqual(evidence['startSeconds'], .5)
        self.assertEqual(evidence['endSeconds'], 2)
        self.assertEqual(evidence['status'], 'captured-unreviewed')
        self.assertNotIn('verified', evidence)

    def test_modified_metadata_invalidates_saved_evidence(self):
        audio, unused = inspect_audio(self.wav, self.metadata)
        self.document['notes'][0][0] = 101.
        self.write_metadata()
        self.assertIsNone(trusted_audio(dict(audio=audio, exitCode=0)))

    def test_mismatched_frame_count_is_rejected(self):
        self.document['recording']['frames'] = 15999
        self.write_metadata()
        audio, notes = inspect_audio(self.wav, self.metadata)
        self.assertFalse(audio['valid'])
        self.assertEqual(notes, [])

    def test_missing_or_duplicate_spell_marker_does_not_make_interval(self):
        audio, notes = inspect_audio(self.wav, self.metadata)
        self.assertIsNone(interval_evidence((audio, notes), 7, 8))
        self.assertIsNone(interval_evidence((audio, notes+notes), 6, 8))

    def test_nonzero_pcm_is_not_required_for_captured_silence(self):
        with wave.open(str(self.wav), 'wb') as out:
            out.setparams((1, 2, 8000, 0, 'NONE', 'not compressed'))
            out.writeframes(bytes(32000))
        self.document['recording']['pcm_peak'] = 0
        self.write_metadata()
        audio, unused = inspect_audio(self.wav, self.metadata)
        self.assertTrue(audio['valid'])
        self.assertEqual(audio['pcmPeak'], 0)


if __name__ == '__main__':
    unittest.main()
