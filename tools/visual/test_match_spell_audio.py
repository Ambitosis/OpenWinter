"""Synthetic tests; contain no installed audio or other game assets."""
import unittest
import numpy as np
from match_spell_audio import RATE, identify, identify_candidates
from unittest.mock import patch


class Matching(unittest.TestCase):
    def setUp(self):
        rng = np.random.default_rng(481)
        self.reference = rng.normal(0, .1, RATE)
        self.noise = rng.normal(0, .01, RATE*3)

    def test_gain_mix_and_offset(self):
        capture = self.noise.copy()
        start = RATE//2
        capture[start:start+RATE] += self.reference*.4
        result = identify(capture, self.reference)
        self.assertEqual(result["status"], "identified")
        self.assertAlmostEqual(result["offset"], start/RATE, places=3)

    def test_unrelated_waveform_is_unresolved(self):
        self.assertEqual(identify(self.noise, self.reference)["status"], "unresolved")

    def test_silent_recording_is_unresolved(self):
        self.assertEqual(identify(self.noise*0, self.reference)["status"], "unresolved")

    def test_explicit_prefix_recovers_clip_truncated_before_loudest_anchors(self):
        reference = self.reference.copy()
        reference[RATE//2:] *= 20
        capture = self.noise.copy()
        capture[:RATE//2] += reference[:RATE//2]
        result = identify_candidates(capture,reference,[.5])
        self.assertEqual(result["attempts"][0]["status"],"unresolved")
        self.assertEqual(result["status"],"identified")
        self.assertEqual(result["referencePrefixSeconds"],.5)
        self.assertEqual(result["referenceDuration"],1)
        self.assertEqual(len(result["attempts"]),2)

    def test_native_rate_keeps_exact_waveform_alignment(self):
        rng = np.random.default_rng(814)
        reference = rng.normal(0,.1,44100)
        capture = np.zeros(88200)
        capture[1789:1789+len(reference)] = reference
        with patch("match_spell_audio.RATE",44100):
            result = identify(capture,reference)
        self.assertEqual(result["status"],"identified")
        self.assertTrue(all(v>.99999 for v in result["anchorScores"]))
        self.assertAlmostEqual(result["offset"],1789/44100,places=4)

    def test_silent_reference_is_explicit(self):
        self.assertEqual(identify(self.noise, self.reference*0)["status"], "silent-reference")


if __name__ == "__main__":
    unittest.main()
