"""Check tracked Markdown's relative files and GitHub-style heading anchors offline.

Run from any directory: python tools/docs/check_links.py
Install the hash-pinned parser with: pip install --require-hashes -r tools/docs/requirements.txt
External URLs are deliberately not fetched. No game data or .NET build is needed.
"""
from html.parser import HTMLParser
from pathlib import Path
import subprocess
import sys
import unicodedata
from urllib.parse import unquote, urlsplit

from markdown_it import MarkdownIt


ROOT = Path(__file__).resolve().parents[2]
MARKDOWN = MarkdownIt("commonmark").enable(["table", "strikethrough"])


class HtmlLinks(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.anchors = set()
        self.links = []

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        for name in (("id", "name") if tag == "a" else ("id",)):
            if attrs.get(name):
                self.anchors.add(attrs[name])
        for name in ("href", "src"):
            if attrs.get(name):
                self.links.append(attrs[name])


def heading_text(tokens):
    return "".join(
        token.content if token.type in ("text", "code_inline", "image") else
        " " if token.type in ("softbreak", "hardbreak") else ""
        for token in tokens
    )


def heading_slug(text):
    # GitHub keeps letters, numbers, combining marks, underscores and hyphens.
    return "".join(
        "-" if char.isspace() else char
        for char in text.lower()
        if char.isspace() or char in "_-" or unicodedata.category(char)[0] in "LNM"
    )


def parse_document(text):
    tokens = MARKDOWN.parse(text)
    anchors = set()
    heading_ids = set()
    links = []
    for index, token in enumerate(tokens):
        line = token.map[0] + 1 if token.map else 1
        if token.type == "heading_open":
            base = heading_slug(heading_text(tokens[index + 1].children or []))
            slug = base
            suffix = 0
            while slug in heading_ids:
                suffix += 1
                slug = f"{base}-{suffix}"
            heading_ids.add(slug)
            anchors.add(slug)
        for child in [token] + (token.children or []):
            if child.type in ("link_open", "image"):
                destination = child.attrGet("href" if child.type == "link_open" else "src")
                if destination:
                    links.append((line, destination))
            elif child.type in ("html_block", "html_inline"):
                html = HtmlLinks()
                html.feed(child.content)
                anchors.update(html.anchors)
                links.extend((line, destination) for destination in html.links)
    return anchors, links


def check_paths(root, paths):
    root = root.resolve()
    documents = {}
    errors = []
    checked = 0

    def read_document(path):
        path = path.resolve()
        if not path.is_relative_to(root):
            raise ValueError("Markdown target escapes repository")
        if path not in documents:
            documents[path] = parse_document(path.read_text(encoding="utf-8-sig"))
        return documents[path]

    for source in paths:
        source = source.resolve()
        if not source.is_relative_to(root):
            errors.append(f"Markdown source escapes repository: {source.name}")
            continue
        display = source.relative_to(root).as_posix()
        try:
            _, links = read_document(source)
        except (OSError, UnicodeError) as error:
            errors.append(f"{display}: cannot read Markdown: {error}")
            continue
        for line, destination in links:
            location = f"{display}:{line}"
            try:
                url = urlsplit(destination)
                if url.scheme or url.netloc or url.path.startswith("/"):
                    continue
                checked += 1
                target = (source.parent / unquote(url.path)).resolve() if url.path else source
                if not target.is_relative_to(root):
                    errors.append(f"{location}: link escapes repository: {destination}")
                    continue
                if not target.exists():
                    errors.append(f"{location}: missing file: {destination}")
                    continue
                if url.fragment:
                    markdown_target = target / "README.md" if target.is_dir() else target
                    if markdown_target.suffix.lower() == ".md":
                        anchors, _ = read_document(markdown_target)
                        if unquote(url.fragment) not in anchors:
                            errors.append(f"{location}: missing anchor: {destination}")
            except (OSError, UnicodeError, ValueError, RuntimeError) as error:
                errors.append(f"{location}: cannot check {destination}: {error}")
    return errors, checked


def main():
    # Git selects repository Markdown, not generated output or installed packages.
    result = subprocess.run(
        ["git", "ls-files", "-z", "--", "*.md", "*.MD"], cwd=ROOT,
        check=True, capture_output=True, encoding="utf-8",
    )
    paths = [ROOT / name for name in result.stdout.split("\0") if name]
    errors, checked = check_paths(ROOT, paths)
    for error in errors:
        print(error, file=sys.stderr)
    print(f"Checked {len(paths)} Markdown files and {checked} local links; {len(errors)} errors.")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
