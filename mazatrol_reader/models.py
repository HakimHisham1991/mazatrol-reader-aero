"""Data models for Mazatrol program parsing and 3D simulation."""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from typing import Any


class ParameterType(str, Enum):
    NA = "NA"
    WHOLE_NUMBER = "wholeNumber"
    READ_DATA = "readData"
    TEXT = "text"
    READ_FULL_NUMBER_2B = "readFullNumber2B"
    READ_FULL_NUMBER_1B = "readFullNumber1B"
    READ_LETTER = "readLetter"
    READ_PATTERN = "readPattern"
    PART_TYPE = "partType"
    UNKNOWN = "UNKNOWN"


@dataclass(frozen=True)
class PatternOption:
    name: str
    value: int


@dataclass(frozen=True)
class ParameterDefinition:
    name: str
    offset: int
    param_type: ParameterType
    visible_for: frozenset[str] = frozenset()
    pattern_options: tuple[PatternOption, ...] = ()


@dataclass(frozen=True)
class UnitDefinition:
    unit_id: int
    name: str
    parameters: tuple[ParameterDefinition, ...] = ()
    is_sub_unit: bool = False

    @property
    def is_figure(self) -> bool:
        return self.name == "FIG"

    @property
    def is_sequence_number(self) -> bool:
        return self.name == "SNo"


@dataclass
class ParameterValue:
    name: str
    value: Any
    file_offset: int
    param_type: ParameterType | str


@dataclass
class ProgramBlock:
    """One row/block in the Mazatrol program (unit or sub-unit)."""

    unit_type_id: int
    unit_name: str
    unit_number: int
    unit_address: int
    is_unit_header: bool
    parameters: list[ParameterValue] = field(default_factory=list)

    def get(self, name: str, default: Any = None) -> Any:
        for param in self.parameters:
            if param.name == name:
                return param.value
        return default

    def to_legacy_rows(self) -> list[list[Any]]:
        """Convert to the legacy nested list format used by the original UI."""
        rows: list[list[Any]] = []
        if self.is_unit_header:
            rows.append(["UNo", self.unit_number, self.unit_address + 2, ""])
            rows.append(["UNIT", self.unit_name, self.unit_address, ""])
        else:
            rows.append([self.unit_name, self.unit_number, self.unit_address + 2, ""])

        for param in self.parameters:
            rows.append([param.name, param.value, param.file_offset, param.param_type])

        while len(rows) < 17:
            rows.append(["", "", "", ""])
        rows.append(["addr", self.unit_address, self.unit_address, ""])
        return rows


@dataclass
class BarFigure:
    line_number: int
    start_x: float
    start_z: float
    finish_x: float
    finish_z: float
    start_corner: float
    finish_corner: float


@dataclass
class MaterialStock:
    od: float
    inner_diameter: float
    length: float
    workface: float


@dataclass
class FacingCut:
    finish_x: float
    finish_z: float


@dataclass
class TurningSimulationInput:
    stock: MaterialStock | None = None
    facing: FacingCut | None = None
    bar_figures: list[BarFigure] = field(default_factory=list)


class UnitEditAction(str, Enum):
    DELETE = "deleteUnit"
    DUPLICATE = "duplicateUnit"
    EXPORT = "exportUnit"
    INSERT_LIN = "insertUnit_LIN"
    INSERT_TPR = "insertUnit_TPR"
    INSERT_FACING = "insertUnit_FACING"
