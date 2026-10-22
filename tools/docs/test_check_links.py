"""Offline fixtures for the documentation CI guard, including issue #11's regression."""
from pathlib import Path
import tempfile
import unittest

from check_links import check_paths


class DocumentationLinksTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)

    def write(self, name, text):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        return path

    def check(self, *paths):
        return check_paths(self.root, paths)[0]

    def test_setup_anchor_regression(self):
        self.write("README.md", "## Building from source\n")
        source = self.write("CONTRIBUTING.md", "[README](README.md#building-from-source)\n")
        self.assertEqual([], self.check(source))
        source.write_text("[README](README.md#setup)\n", encoding="utf-8")
        errors = self.check(source)
        self.assertEqual(1, len(errors))
        self.assertIn("missing anchor: README.md#setup", errors[0])

    def test_relative_paths_reference_links_images_and_encoded_names(self):
        self.write("Guide (new).md", "## Installation\n")
        self.write("image.png", "synthetic fixture")
        source = self.write("docs/README.md", "[guide][setup]\n\n"
                            "[setup]: <../Guide%20(new).md#installation>\n\n"
                            "![image](../image.png)\n")
        self.assertEqual([], self.check(source))

    def test_formatted_duplicate_and_setext_heading_anchors(self):
        source = self.write("README.md", "## Build `source` & *test*!\n\n"
                            "## Build `source` & *test*!\n\n"
                            "Setext heading\n==============\n\n"
                            "[first](#build-source--test)\n"
                            "[second](#build-source--test-1)\n"
                            "[setext](#setext-heading)\n")
        self.assertEqual([], self.check(source))

    def test_code_comments_and_external_badges_do_not_create_local_links(self):
        source = self.write("README.md", "`[example](absent.md)`\n\n"
                            "````md\n[example](absent.md)\n````\n\n"
                            "    [example](absent.md)\n\n"
                            "<!-- [example](absent.md) -->\n\n"
                            "[![CI](https://example.invalid/badge.svg)]"
                            "(https://example.invalid/build?branch=main)\n")
        self.assertEqual([], self.check(source))

    def test_missing_file_and_missing_anchor_are_both_reported(self):
        self.write("Guide.md", "# Existing\n")
        source = self.write("README.md", "[file](absent.md) [anchor](Guide.md#absent)\n")
        errors = self.check(source)
        self.assertEqual(2, len(errors))
        self.assertIn("missing file", errors[0])
        self.assertIn("missing anchor", errors[1])

    def test_parent_escape_is_rejected_without_reading_outside_repository(self):
        source = self.write("README.md", "[outside](../outside.md#secret)\n")
        errors = self.check(source)
        self.assertEqual(1, len(errors))
        self.assertIn("link escapes repository", errors[0])

    def test_explicit_html_anchors_and_links(self):
        source = self.write("README.md", '<a id="custom"></a>\n\n'
                            '<a href="#custom">valid</a>\n\n'
                            '<a href="missing.md">invalid</a>\n')
        errors = self.check(source)
        self.assertEqual(1, len(errors))
        self.assertIn("missing file", errors[0])


if __name__ == "__main__":
    unittest.main()
