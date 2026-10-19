"""A failed managed build must never fall through to an old Godot client DLL."""
import subprocess
import unittest
from unittest.mock import patch

import visual


class ClientBuildGuardTests(unittest.TestCase):
    def test_failed_build_stops_before_creating_capture_or_launching_client(self):
        failure = subprocess.CompletedProcess([], 1, "compile error", "diagnostic")
        with patch.object(visual.subprocess, "run", return_value=failure) as run:
            with patch.object(visual, "godot", side_effect=AssertionError("must not launch")):
                with self.assertRaisesRegex(RuntimeError, "Client build failed.*"):
                    visual.run_openwinter({}, None, None, None, 1, "unused", (1280, 720))
        self.assertEqual(run.call_count, 1)


if __name__ == "__main__":
    unittest.main()
