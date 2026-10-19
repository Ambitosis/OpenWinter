"""Owned screenshot file identity and boundary checks; no real window or input."""
import hashlib
import json
import tempfile
import types
import unittest
from pathlib import Path
from unittest.mock import patch

from PIL import Image
import visual


class NativeFramebufferTests(unittest.TestCase):
    def setUp(self):
        visual.OUT.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix="native-framebuffer-test-", dir=visual.OUT)
        self.root = Path(self.temp.name).resolve()
        self.assertTrue(self.root.is_relative_to(visual.OUT.resolve()))
        self.user = self.root / "user"
        self.shots = self.root / "shots"
        self.shots.mkdir()
        self.calls = []

    def tearDown(self):
        self.temp.cleanup()

    def fake_capture(self, color, multiple=False):
        def post(hwnd, message, key, bits):
            self.calls.append((hwnd, message, key, bits))
            if message == 0x0101:
                for index in range(2 if multiple else 1):
                    Image.new("RGB", (8, 6), color).save(self.user / "screenshots" / f"native_{index}.tga")
            return 1
        return types.SimpleNamespace(user32=types.SimpleNamespace(PostMessageW=post),
                                     capture=lambda hwnd: Image.new("RGB", (8, 6), "black"))

    def test_raw_snapshot_hash_and_exact_extended_input(self):
        with patch.dict("sys.modules", {"wincap": self.fake_capture("red")}):
            visual.save_native_framebuffer(12345, self.user, self.shots, "phase")
        record = json.loads((self.shots / "native-framebuffer.jsonl").read_text())
        self.assertTrue(record["nonblack"])
        self.assertEqual(record["rawSha256"], hashlib.sha256((self.shots / "phase.tga").read_bytes()).hexdigest())
        self.assertEqual((255, 0, 0), Image.open(self.shots / "phase.png").getpixel((0, 0)))
        self.assertEqual([(12345, 0x0100, 44, 1 | (0x37 << 16) | (1 << 24)),
                          (12345, 0x0101, 44, 1 | (0x37 << 16) | (1 << 24) | (3 << 30))], self.calls)

    def test_black_cleanup_control_is_retained_and_flagged(self):
        with patch.dict("sys.modules", {"wincap": self.fake_capture("black")}):
            visual.save_native_framebuffer(12345, self.user, self.shots, "removed")
        record = json.loads((self.shots / "native-framebuffer.jsonl").read_text())
        self.assertFalse(record["nonblack"])
        self.assertTrue((self.shots / "removed.tga").exists())

    def test_ambiguous_native_files_fail_instead_of_selecting_one(self):
        with patch.dict("sys.modules", {"wincap": self.fake_capture("red", multiple=True)}):
            with self.assertRaisesRegex(RuntimeError, "ambiguous"):
                visual.save_native_framebuffer(12345, self.user, self.shots, "phase")
        self.assertFalse((self.shots / "phase.png").exists())

    def test_escaping_name_rejected_before_input(self):
        with patch.dict("sys.modules", {"wincap": self.fake_capture("red")}):
            with self.assertRaisesRegex(RuntimeError, "escaped"):
                visual.save_native_framebuffer(12345, self.user, self.shots, "../outside")
        self.assertEqual([], self.calls)


if __name__ == "__main__":
    unittest.main()
