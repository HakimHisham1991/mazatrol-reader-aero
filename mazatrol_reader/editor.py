"""Binary editing operations for Mazatrol program units."""

from __future__ import annotations

import logging
from pathlib import Path

from mazatrol_reader.config import STANDARD_UNIT_SIZE, UNITS_DIR, UNIT_TEMPLATES
from mazatrol_reader.models import UnitEditAction

logger = logging.getLogger(__name__)


class ProgramEditor:
    """Perform unit-level binary edits on Mazatrol program files."""

    def __init__(self, unit_size: int = STANDARD_UNIT_SIZE) -> None:
        self.unit_size = unit_size

    def apply(
        self,
        file_path: Path | str,
        unit_address: int,
        action: UnitEditAction,
        unit_name: str = "UNIT",
    ) -> Path:
        path = Path(file_path)
        data = path.read_bytes()

        if unit_address < 0 or unit_address >= len(data):
            raise ValueError(f"Invalid unit address 0x{unit_address:X} for file {path}")

        before = data[:unit_address]
        unit_bytes = data[unit_address : unit_address + self.unit_size]
        after = data[unit_address + self.unit_size :]

        if len(unit_bytes) < self.unit_size:
            raise ValueError(
                f"Unit at 0x{unit_address:X} is truncated "
                f"(expected {self.unit_size} bytes, got {len(unit_bytes)})"
            )

        if action == UnitEditAction.DELETE:
            new_data = before + after
        elif action == UnitEditAction.DUPLICATE:
            new_data = before + unit_bytes + unit_bytes + after
        elif action == UnitEditAction.EXPORT:
            export_path = path.with_name(f"{path.name}_{unit_name}.unit")
            export_path.write_bytes(unit_bytes)
            logger.info("Exported unit to %s", export_path)
            return export_path
        elif action in {
            UnitEditAction.INSERT_LIN,
            UnitEditAction.INSERT_TPR,
            UnitEditAction.INSERT_FACING,
        }:
            template = self._load_template(action)
            new_data = before + unit_bytes + template + after
        else:
            raise ValueError(f"Unsupported edit action: {action}")

        path.write_bytes(new_data)
        logger.info("Applied %s at 0x%X in %s", action.value, unit_address, path)
        return path

    @staticmethod
    def _load_template(action: UnitEditAction) -> bytes:
        key = {
            UnitEditAction.INSERT_LIN: "LIN",
            UnitEditAction.INSERT_TPR: "TPR",
            UnitEditAction.INSERT_FACING: "FACING",
        }[action]
        filename, size = UNIT_TEMPLATES[key]
        template_path = UNITS_DIR / filename
        if not template_path.is_file():
            raise FileNotFoundError(
                f"Unit template not found: {template_path}. "
                "Place binary template files under the units/ directory."
            )
        data = template_path.read_bytes()
        if len(data) < size:
            raise ValueError(
                f"Template {template_path} is too small "
                f"(expected at least {size} bytes, got {len(data)})"
            )
        return data[:size]
