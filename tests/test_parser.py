"""Parser tests that do not require wxPython or pythonOCC."""

from __future__ import annotations

import struct
from pathlib import Path

import pytest

from mazatrol_reader.models import ParameterType
from mazatrol_reader.parser import BinaryReader, PBGParser, StructureLoader


@pytest.fixture
def structure_xml(tmp_path: Path) -> Path:
    xml = tmp_path / "structure.xml"
    xml.write_text(
        """<?xml version="1.0"?>
        <data>
          <unit id="1" name="MAT">
            <parameter name="OD" pos="64" type="readData"/>
            <parameter name="Length" pos="72" type="readData"/>
          </unit>
          <unit id="4" name="END">
            <parameter name="REPEAT" pos="0" type="readFullNumber2B"/>
          </unit>
        </data>
        """,
        encoding="utf-8",
    )
    return xml


def test_structure_loader(structure_xml: Path) -> None:
    definitions = StructureLoader(structure_xml).load()
    assert definitions[1].name == "MAT"
    assert definitions[1].parameters[0].param_type == ParameterType.READ_DATA


def test_binary_reader_scaled_int(tmp_path: Path) -> None:
    path = tmp_path / "data.bin"
    path.write_bytes(struct.pack("<i", 250_000))  # 25.0
    with path.open("rb") as stream:
        reader = BinaryReader(stream)
        assert reader.read_scaled_int(0) == 25.0


def test_read_data_undefined_zero_is_na() -> None:
    from mazatrol_reader.parameter_formatter import (
        NOT_APPLICABLE,
        format_display_value,
        is_read_data_defined_with_flags,
    )

    assert not is_read_data_defined_with_flags(52, 0, 0)
    assert format_display_value(0.0, ParameterType.READ_DATA, False) == NOT_APPLICABLE
    assert format_display_value(0.0, ParameterType.READ_DATA, True) == "0"
    assert is_read_data_defined_with_flags(56, 0, 0b0000_0001)
    assert format_display_value(0, ParameterType.READ_FULL_NUMBER_2B, False) == NOT_APPLICABLE


def test_parser_end_unit(tmp_path: Path, structure_xml: Path) -> None:
    program = tmp_path / "test.pbg"
    payload = bytearray(0x200)
    payload[0xFC] = 4  # END unit type id
    program.write_bytes(payload)

    blocks = PBGParser(structure_path=structure_xml).parse(program)
    assert len(blocks) == 1
    assert blocks[0].unit_name == "END"
