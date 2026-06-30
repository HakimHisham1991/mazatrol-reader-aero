"""Parse Mazatrol Matrix HTML program exports into program blocks."""

from __future__ import annotations

import re
from html import unescape
from html.parser import HTMLParser
from pathlib import Path

from mazatrol_reader.models import ParameterType, ParameterValue, ProgramBlock

START_UNIT_ADDRESS = 0xFC
STANDARD_UNIT_SIZE = 100

UNIT_IDS: dict[str, int] = {
    "MAT": 1,
    "END": 4,
    "MANL PRG": 6,
    "M-CODE": 7,
    "HEAD": 19,
    "BAR": 48,
    "FACING": 51,
    "THREAD": 52,
    "T.GROOVE": 53,
    "T.DRILL": 54,
    "T.TAP": 55,
    "POCKET": 99,
    "DRILLING": 57,
    "LINE": 58,
    "TRANSFER": 59,
}


class _TableParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.tables: list[list[list[str]]] = []
        self._in_table = False
        self._in_row = False
        self._in_cell = False
        self._current_table: list[list[str]] = []
        self._current_row: list[str] = []
        self._cell_parts: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if tag == "table":
            self._in_table = True
            self._current_table = []
        elif tag == "tr" and self._in_table:
            self._in_row = True
            self._current_row = []
        elif tag in ("td", "th") and self._in_row:
            self._in_cell = True
            self._cell_parts = []

    def handle_endtag(self, tag: str) -> None:
        if tag in ("td", "th") and self._in_cell:
            self._in_cell = False
            self._current_row.append(unescape("".join(self._cell_parts)).strip())
        elif tag == "tr" and self._in_row:
            self._in_row = False
            if self._current_row:
                self._current_table.append(self._current_row)
        elif tag == "table" and self._in_table:
            self._in_table = False
            if self._current_table:
                self.tables.append(self._current_table)

    def handle_data(self, data: str) -> None:
        if self._in_cell:
            self._cell_parts.append(data)


def parse_html_program(text: str) -> list[ProgramBlock]:
    if "<table" in text.lower():
        return _parse_tables(text)
    return _parse_pre(text)


def parse_html_file(path: Path | str) -> list[ProgramBlock]:
    path = Path(path)
    raw = path.read_text(encoding="utf-8", errors="replace")
    return parse_html_program(raw)


def _build_parameters(columns: list[str], values: list[str], base_address: int) -> list[ParameterValue]:
    params: list[ParameterValue] = []
    for i, col in enumerate(columns):
        if i >= len(values) or not col.strip():
            continue
        raw = values[i]
        if raw in ("@", "?", ""):
            ptype = ParameterType.NA
            value: object = raw
        else:
            try:
                value = float(raw)
                ptype = ParameterType.READ_DATA
            except ValueError:
                try:
                    value = int(raw)
                    ptype = ParameterType.READ_FULL_NUMBER_2B
                except ValueError:
                    value = raw
                    ptype = ParameterType.TEXT
        params.append(
            ParameterValue(
                name=col,
                value=value,
                file_offset=base_address + 8 + i * 4,
                param_type=ptype,
            )
        )
    return params


def _header_block(index: int, unit_number: int, unit_name: str, columns: list[str], values: list[str]) -> ProgramBlock:
    address = START_UNIT_ADDRESS + index * STANDARD_UNIT_SIZE
    return ProgramBlock(
        unit_type_id=UNIT_IDS.get(unit_name, 0),
        unit_name=unit_name,
        unit_number=unit_number,
        unit_address=address,
        is_unit_header=True,
        parameters=_build_parameters(columns, values, address),
    )


def _sub_block(index: int, kind: str, columns: list[str], values: list[str]) -> ProgramBlock:
    address = START_UNIT_ADDRESS + index * STANDARD_UNIT_SIZE
    unit_number = int(values[0]) if values and values[0].isdigit() else 0
    return ProgramBlock(
        unit_type_id=168 if kind == "FIG" else 180,
        unit_name=kind,
        unit_number=unit_number,
        unit_address=address,
        is_unit_header=False,
        parameters=_build_parameters(columns, values[1:] if kind == "FIG" else values, address),
    )


def _parse_tables(text: str) -> list[ProgramBlock]:
    parser = _TableParser()
    parser.feed(text)
    blocks: list[ProgramBlock] = []
    pending: ProgramBlock | None = None

    for table in parser.tables:
        if not table:
            continue
        header = table[0]
        if not header:
            continue

        if header[0].upper().startswith("UNO"):
            for row in table[1:]:
                if not row or not row[0].strip().isdigit():
                    continue
                unit_number = int(row[0])
                if header[1].upper().startswith("MAT"):
                    pending = _header_block(len(blocks), unit_number, "MAT", header[2:], row[1:])
                else:
                    pending = _header_block(len(blocks), unit_number, row[1].strip(), header[2:], row[2:])
                blocks.append(pending)
            continue

        if header[0].upper() == "FIG" and pending is not None:
            for row in table[1:]:
                blocks.append(_sub_block(len(blocks), "FIG", header[1:], row))
            continue

        if pending is not None and (header[0].upper().startswith("SNO") or (len(header) > 1 and header[1].upper().startswith("SNO"))):
            cols = header[1:] if header[0].upper().startswith("SNO") else header[2:]
            cols = [c for c in cols if c]
            for row in table[1:]:
                values = row[1:] if header[0].upper().startswith("SNO") else row[2:]
                blocks.append(_sub_block(len(blocks), "SNo", cols, values))

    return blocks


def _parse_pre(text: str) -> list[ProgramBlock]:
    plain = unescape(re.sub(r"<[^>]+>", "", text))
    lines = [line.rstrip() for line in plain.splitlines() if line.strip()]
    blocks: list[ProgramBlock] = []
    pending: ProgramBlock | None = None
    i = 0

    while i < len(lines):
        if not lines[i].startswith("UNo."):
            i += 1
            continue
        header = re.sub(r"\s+", " ", lines[i]).split()
        i += 1
        while i < len(lines) and not lines[i].strip():
            i += 1
        if i >= len(lines):
            break
        parts = lines[i].split()
        i += 1
        if not parts or not parts[0].isdigit():
            continue

        unit_number = int(parts[0])
        if header[1].upper().replace(".", "") == "MAT":
            pending = _header_block(len(blocks), unit_number, "MAT", header[2:], parts[1:])
        elif "TOOL" in header:
            pending = _header_block(len(blocks), unit_number, "MANL PRG", header[2:], parts[2:])
        elif "M1" in header:
            pending = _header_block(len(blocks), unit_number, "M-CODE", header[2:], parts[2:])
        elif "CONTI." in header:
            pending = _header_block(len(blocks), unit_number, "END", header[2:], parts[2:])
        elif "HEAD" in header:
            pending = _header_block(len(blocks), unit_number, "HEAD", header[2:], parts[2:])
        elif "DIA" in header and "DEPTH" in header:
            pending = _header_block(len(blocks), unit_number, "DRILLING", header[2:], parts[2:])
        elif "MODE" in header and "POS-C" in header:
            pending = _header_block(len(blocks), unit_number, "POCKET", header[2:], parts[2:])
        elif len(header) > 2 and header[1] == "UNIT" and header[2] == "PART":
            pending = _header_block(len(blocks), unit_number, parts[1], header[3:], parts[2:])
        elif len(parts) > 2 and parts[1] == "MANL" and parts[2] == "PRG":
            pending = _header_block(len(blocks), unit_number, "MANL PRG", header[2:], parts[3:])
        else:
            pending = _header_block(len(blocks), unit_number, parts[1], header[2:], parts[2:])
        blocks.append(pending)

    return blocks
