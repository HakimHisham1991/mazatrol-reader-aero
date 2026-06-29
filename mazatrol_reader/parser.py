"""Binary Mazatrol (.PBG) parser driven by qts200m.xml structure definitions."""

from __future__ import annotations

import logging
import struct
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Any, BinaryIO

from mazatrol_reader.config import (
    DISPLAYED_UNIT_TYPE_IDS,
    END_UNIT_TYPE_ID,
    START_UNIT_ADDRESS,
    STRUCTURE_XML,
)
from mazatrol_reader.models import (
    BarFigure,
    FacingCut,
    MaterialStock,
    ParameterDefinition,
    ParameterType,
    ParameterValue,
    PatternOption,
    ProgramBlock,
    TurningSimulationInput,
    UnitDefinition,
)

logger = logging.getLogger(__name__)


class StructureLoader:
    """Load unit/parameter definitions from qts200m.xml."""

    def __init__(self, xml_path: Path | None = None) -> None:
        self.xml_path = xml_path or STRUCTURE_XML

    def load(self) -> list[UnitDefinition]:
        if not self.xml_path.is_file():
            raise FileNotFoundError(f"Structure definition not found: {self.xml_path}")

        root = ET.parse(self.xml_path).getroot()
        definitions: list[UnitDefinition] = [
            UnitDefinition(unit_id=i, name="TBD") for i in range(257)
        ]

        for unit_elem in root:
            unit_id = int(unit_elem.get("id", "-1"))
            if unit_id < 0 or unit_id >= len(definitions):
                logger.warning("Skipping unit with invalid id: %s", unit_elem.get("id"))
                continue

            name = unit_elem.get("name", "TBD")
            params = tuple(self._parse_parameter(param) for param in unit_elem)
            is_sub_unit = name in {"SNo", "FIG"}
            definitions[unit_id] = UnitDefinition(
                unit_id=unit_id,
                name=name,
                parameters=params,
                is_sub_unit=is_sub_unit,
            )

        logger.info("Loaded %d unit definitions from %s", len(root), self.xml_path)
        return definitions

    @staticmethod
    def _parse_parameter(elem: ET.Element) -> ParameterDefinition:
        param_type = ParameterType(elem.get("type", "UNKNOWN"))
        visible_raw = elem.get("visible", "")
        visible_for = frozenset(v.strip() for v in visible_raw.split(",") if v.strip())

        pattern_options: tuple[PatternOption, ...] = ()
        if param_type == ParameterType.READ_PATTERN:
            pattern_options = tuple(
                PatternOption(
                    name=option.get("name", ""),
                    value=int(option.get("value", "0")),
                )
                for option in elem
            )

        return ParameterDefinition(
            name=elem.get("name", ""),
            offset=int(elem.get("pos", "0")),
            param_type=param_type,
            visible_for=visible_for,
            pattern_options=pattern_options,
        )


class BinaryReader:
    """Low-level binary field readers for Mazatrol program files."""

    def __init__(self, stream: BinaryIO) -> None:
        self._stream = stream

    def read_byte(self, address: int) -> int:
        self._stream.seek(address)
        return struct.unpack("B", self._stream.read(1))[0]

    def read_uint16(self, address: int) -> int:
        self._stream.seek(address)
        return struct.unpack("<H", self._stream.read(2))[0]

    def read_fixed_point_32(self, address: int) -> float:
        self._stream.seek(address)
        word = struct.unpack("<I", self._stream.read(4))[0]
        return float(word) / (2**16)

    def read_scaled_int(self, address: int) -> float:
        self._stream.seek(address)
        word = struct.unpack("<i", self._stream.read(4))[0]
        return float(word) / 10_000

    def read_text(self, address: int, length: int = 16) -> str:
        self._stream.seek(address)
        raw = self._stream.read(length)
        chars = struct.unpack("c" * length, raw)
        return b"".join(chars).decode("ascii", errors="replace").rstrip("\x00 ")

    def read_letter(self, address: int) -> str:
        value = self.read_byte(address)
        return chr(97 + value - 10)

    def read_pattern(self, address: int, options: tuple[PatternOption, ...]) -> str:
        word = self.read_byte(address)
        for option in options:
            if word == option.value:
                return option.name
        return "ERR"

    def write_scaled_int(self, address: int, value: float) -> None:
        self._stream.seek(address)
        packed = struct.pack("<I", int(float(value) * 10_000))
        self._stream.write(packed)


class PBGParser:
    """Parse Mazatrol binary programs into structured blocks."""

    def __init__(
        self,
        structure: list[UnitDefinition] | None = None,
        structure_path: Path | None = None,
    ) -> None:
        if structure is None:
            structure = StructureLoader(structure_path).load()
        self._structure = structure

    @property
    def structure(self) -> list[UnitDefinition]:
        return self._structure

    def parse(self, file_path: Path | str) -> list[ProgramBlock]:
        path = Path(file_path)
        if not path.is_file():
            raise FileNotFoundError(f"Program file not found: {path}")

        blocks: list[ProgramBlock] = []
        with path.open("rb") as stream:
            reader = BinaryReader(stream)
            index = 0
            unit_type_id = -1

            while unit_type_id != END_UNIT_TYPE_ID:
                unit_address = START_UNIT_ADDRESS + index * 100
                index += 1
                unit_type_id = reader.read_byte(unit_address)
                unit_number = reader.read_byte(unit_address + 2)

                definition = self._structure[unit_type_id]
                if unit_type_id not in DISPLAYED_UNIT_TYPE_IDS:
                    logger.debug(
                        "Skipping unsupported unit type id=%d name=%s at 0x%X",
                        unit_type_id,
                        definition.name,
                        unit_address,
                    )
                    continue

                block = self._parse_block(
                    reader=reader,
                    definition=definition,
                    unit_type_id=unit_type_id,
                    unit_address=unit_address,
                    unit_number=unit_number,
                )
                blocks.append(block)
                logger.debug(
                    "Parsed unit 0x%X type=%d name=%s number=%d",
                    unit_address,
                    unit_type_id,
                    definition.name,
                    unit_number,
                )

        logger.info("Parsed %d blocks from %s", len(blocks), path)
        return blocks

    def _parse_block(
        self,
        reader: BinaryReader,
        definition: UnitDefinition,
        unit_type_id: int,
        unit_address: int,
        unit_number: int,
    ) -> ProgramBlock:
        visible_pattern = ""
        ignore_next = False
        parameters: list[ParameterValue] = []

        for param_def in definition.parameters:
            value = self._read_parameter_value(
                reader=reader,
                param_def=param_def,
                unit_address=unit_address,
            )

            if param_def.param_type == ParameterType.READ_PATTERN:
                visible_pattern = str(value)

            if param_def.param_type != ParameterType.READ_PATTERN:
                if param_def.visible_for and visible_pattern not in param_def.visible_for:
                    value = "*"

            if ignore_next:
                value = ""
                ignore_next = False
            elif unit_type_id == 161 and value == "W":
                value = ""
                ignore_next = True

            parameters.append(
                ParameterValue(
                    name=param_def.name,
                    value=value,
                    file_offset=unit_address + param_def.offset,
                    param_type=param_def.param_type,
                )
            )

        return ProgramBlock(
            unit_type_id=unit_type_id,
            unit_name=definition.name,
            unit_number=unit_number,
            unit_address=unit_address,
            is_unit_header=not definition.is_sub_unit,
            parameters=parameters,
        )

    @staticmethod
    def _read_parameter_value(
        reader: BinaryReader,
        param_def: ParameterDefinition,
        unit_address: int,
    ) -> object:
        address = unit_address + param_def.offset
        param_type = param_def.param_type

        if param_type == ParameterType.NA:
            return "*"
        if param_def.offset == 0 and param_type not in {
            ParameterType.READ_PATTERN,
            ParameterType.PART_TYPE,
        }:
            return "?"
        if param_type == ParameterType.WHOLE_NUMBER:
            return reader.read_fixed_point_32(address)
        if param_type == ParameterType.READ_DATA:
            return reader.read_scaled_int(address)
        if param_type == ParameterType.TEXT:
            return reader.read_text(address)
        if param_type == ParameterType.READ_FULL_NUMBER_2B:
            return reader.read_uint16(address)
        if param_type == ParameterType.READ_FULL_NUMBER_1B:
            return reader.read_byte(address)
        if param_type == ParameterType.READ_LETTER:
            return reader.read_letter(address)
        if param_type == ParameterType.READ_PATTERN:
            return reader.read_pattern(address, param_def.pattern_options)
        if param_type == ParameterType.PART_TYPE:
            return reader.read_byte(address)

        logger.warning("Unknown parameter type %s for %s", param_type, param_def.name)
        return "ERROR"

    def to_legacy_program(self, blocks: list[ProgramBlock]) -> list[list[list[Any]]]:
        return [block.to_legacy_rows() for block in blocks]

    def write_parameter(
        self,
        file_path: Path | str,
        file_offset: int,
        param_type: ParameterType | str,
        new_value: str,
    ) -> None:
        if str(param_type) != ParameterType.READ_DATA.value:
            raise ValueError("Only readData parameters are editable")

        path = Path(file_path)
        with path.open("r+b") as stream:
            reader = BinaryReader(stream)
            reader.write_scaled_int(file_offset, float(new_value))
        logger.info("Updated parameter at offset 0x%X in %s", file_offset, path)


class TurningProfileExtractor:
    """Extract stock and bar turning data from parsed program blocks."""

    @staticmethod
    def extract(blocks: list[ProgramBlock]) -> TurningSimulationInput:
        result = TurningSimulationInput()
        unit_context = ""
        bar_start_x = 0.0
        bar_start_z = 0.0
        figure_index = 0

        for block in blocks:
            if block.is_unit_header:
                unit_context = block.unit_name
                if unit_context == "MAT":
                    result.stock = MaterialStock(
                        od=float(block.get("OD", 0)),
                        inner_diameter=float(block.get("ID", 0)),
                        length=float(block.get("Length", 0)),
                        workface=float(block.get("Workface", 0)),
                    )
                elif unit_context == "FACING":
                    bar_start_x = float(block.get("PART", 0) or 0)
                    bar_start_z = float(block.get("FIN-Z", 0) or 0)
                elif unit_context == "BAR":
                    bar_start_x = float(block.get("CPT-X", 0) or 0)
                    bar_start_z = float(block.get("CPT-Z", 0) or 0)
                continue

            if block.unit_name != "FIG":
                continue

            if unit_context == "FACING":
                result.facing = FacingCut(
                    finish_x=float(block.get("SPT-X", 0)),
                    finish_z=float(block.get("SPT-Z", 0)),
                )
                continue

            if unit_context != "BAR":
                continue

            figure_index += 1
            finish_x = float(block.get("FPT-X", 0))
            finish_z = float(block.get("FPT-Z", 0))
            start_corner = float(block.get("S-CNR", 0) or 0)
            finish_corner = float(block.get("F-CNR/$", 0) or 0)

            start_x_raw = block.get("SPT-X", "*")
            start_z_raw = block.get("SPT-Z", "*")

            if start_x_raw == "*":
                start_x = finish_x
                start_z = bar_start_z
            else:
                start_x = float(start_x_raw)
                start_z = float(start_z_raw) if start_z_raw != "*" else bar_start_z

            result.bar_figures.append(
                BarFigure(
                    line_number=figure_index,
                    start_x=start_x,
                    start_z=start_z,
                    finish_x=finish_x,
                    finish_z=finish_z,
                    start_corner=start_corner,
                    finish_corner=finish_corner,
                )
            )
            bar_start_x = finish_x
            bar_start_z = finish_z

        return result
