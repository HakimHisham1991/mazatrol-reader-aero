"""M6M (M640M) binary layout helpers."""

from __future__ import annotations

import struct
from pathlib import Path

M6M_MAT_NUM = 251
M6M_SNO_NUM = 6
M6M_FIG_RECT_NUM = 62
M6M_MANU_SNO_NUMS = frozenset({1, 8})

PARENT_UNIT_IDS = frozenset({96, 99, 66, 67, 32, 38, 55, 6})

HEADER_STRUCTURE_IDS = frozenset({1, 2, 4, 5, 6, 32, 38, 55, 66, 67, 96, 99})


def m6m_file_has_wpc_html(path: Path) -> bool:
    html = path.with_suffix(".html")
    if not html.is_file():
        return False
    raw = html.read_text(encoding="windows-1252", errors="replace")
    return "ADD. WPC" in raw or "ADD WPC" in raw


def m6m_has_wpc_coords(data: bytes, mat_address: int) -> bool:
    if mat_address + 80 > len(data):
        return False
    for offset in (66, 70, 74, 78):
        if struct.unpack_from("<i", data, mat_address + offset)[0] != 0:
            return True
    return False


def resolve_m6m_structure_id(
    slot_index: int,
    raw_type: int,
    unit_num: int,
    *,
    expect_sno: bool,
) -> tuple[int, bool]:
    """Return (structure_unit_id, next_expect_sno)."""
    if slot_index == 0 and raw_type == 0:
        return 1, False

    if raw_type == 96:
        return 96, True
    if raw_type == 99:
        return 99, True
    if raw_type == 66:
        return 66, True
    if raw_type == 67:
        return 67, True
    if raw_type == 32:
        return 32, True
    if raw_type == 38:
        return 38, True
    if raw_type == 55:
        return 55, True
    if raw_type == 6:
        return 6, True
    if raw_type == 5:
        return 5, False
    if raw_type == 4:
        return 4, False

    if raw_type == 0:
        if unit_num in M6M_MANU_SNO_NUMS:
            return 161, False
        if unit_num == M6M_FIG_RECT_NUM:
            return 193, False
        if unit_num == M6M_SNO_NUM:
            if expect_sno:
                return 177, False
            return 194, False

    return -1, expect_sno


def m6m_should_stop(raw_type: int, unit_num: int, unit_address: int, file_size: int) -> bool:
    if unit_address + 100 > file_size:
        return True
    return raw_type == 0 and unit_num == 0
