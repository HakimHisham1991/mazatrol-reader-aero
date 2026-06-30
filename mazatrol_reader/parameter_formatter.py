"""Format Mazatrol parameter values for display (@ vs defined zero)."""

from __future__ import annotations

from typing import Any

from mazatrol_reader.models import ParameterType

NOT_APPLICABLE = "N/A"

# SNo blocks: optional readData fields mark "defined" in byte +30.
_READ_DATA_DEFINED_FLAG_BY_OFFSET: dict[int, int] = {
    48: 0b0000_0100,
    52: 0b0000_0100,
    56: 0b0000_0001,
    64: 0b0000_0010,
}


def is_placeholder(value: Any) -> bool:
    if value is None:
        return True
    if isinstance(value, str):
        return value in {"", "*", "?", NOT_APPLICABLE}
    return False


def is_read_data_defined(unit_address: int, offset: int, raw_scaled_int: int, data: bytes) -> bool:
    if raw_scaled_int != 0:
        return True
    flag_bit = _READ_DATA_DEFINED_FLAG_BY_OFFSET.get(offset)
    if flag_bit is None:
        return False
    if unit_address + 30 >= len(data):
        return False
    return (data[unit_address + 30] & flag_bit) != 0


def is_read_data_defined_with_flags(offset: int, raw_scaled_int: int, flags_byte: int) -> bool:
    if raw_scaled_int != 0:
        return True
    flag_bit = _READ_DATA_DEFINED_FLAG_BY_OFFSET.get(offset)
    if flag_bit is None:
        return False
    return (flags_byte & flag_bit) != 0


def format_float(value: float) -> str:
    text = f"{value:.4f}".rstrip("0").rstrip(".")
    return text if text else "0"


def format_display_value(value: Any, param_type: ParameterType | str, is_defined: bool) -> str:
    if is_placeholder(value) or not is_defined:
        return NOT_APPLICABLE
    if isinstance(value, float):
        return format_float(value)
    return str(value)
