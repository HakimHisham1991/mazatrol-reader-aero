"""Reverse-engineer PBF unit/parameter schemas from Mazatrol HTML exports."""

from __future__ import annotations

import re
from collections import defaultdict
from html import unescape
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PBF_DIR = ROOT / "SAMPLE_NC_PROGRAM" / "PBF"


def strip_html(text: str) -> str:
    return unescape(re.sub(r"<[^>]+>", "", text))


def parse_file(path: Path) -> list[str]:
    raw = path.read_text(encoding="utf-8", errors="replace")
    if "UNo." not in raw:
        raw = path.read_text(encoding="windows-1252", errors="replace")
    return [strip_html(line).rstrip() for line in raw.splitlines() if strip_html(line).strip()]


def analyze() -> dict[str, dict]:
    units: dict[str, dict] = defaultdict(
        lambda: {
            "headers": set(),
            "sub_headers": set(),
            "count": 0,
            "files": set(),
            "samples": [],
        }
    )

    for path in sorted(PBF_DIR.glob("*.html")):
        lines = parse_file(path)
        i = 0
        while i < len(lines):
            line = lines[i]
            if not line.strip().startswith("UNo."):
                i += 1
                continue

            header = re.sub(r"\s+", " ", line.strip())
            i += 1
            while i < len(lines) and not lines[i].strip():
                i += 1
            if i >= len(lines):
                break

            row = lines[i]
            match = re.match(r"\s*(\d+)\s+(\S+)", row)
            if not match:
                i += 1
                continue

            unit_name = match.group(2).strip()
            entry = units[unit_name]
            entry["headers"].add(header)
            entry["count"] += 1
            entry["files"].add(path.name)
            if len(entry["samples"]) < 2:
                entry["samples"].append((path.name, header, row[:140]))
            i += 1

            while i < len(lines):
                sub = lines[i].strip()
                if sub.startswith("UNo."):
                    break
                if sub.startswith("SNo.") or sub.startswith("FIG"):
                    entry["sub_headers"].add(re.sub(r"\s+", " ", sub))
                i += 1

    return units


def main() -> None:
    units = analyze()
    print(f"UNIT TYPES: {len(units)}")
    for name in sorted(units):
        entry = units[name]
        print(f"\n=== {name} ({entry['count']} in {len(entry['files'])} files) ===")
        for header in sorted(entry["headers"]):
            print(f"  HDR: {header[:120]}")
        for sub in sorted(entry["sub_headers"]):
            print(f"  SUB: {sub[:120]}")


if __name__ == "__main__":
    main()
