"""Application paths and Mazatrol binary layout constants."""

from __future__ import annotations

from pathlib import Path

# Repository / package root (parent of mazatrol_reader/)
PACKAGE_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = PACKAGE_DIR.parent

STRUCTURE_XML = PROJECT_ROOT / "qts200m.xml"
PROGRAMS_DIR = PROJECT_ROOT / "programs"
UNITS_DIR = PROJECT_ROOT / "units"
ASSETS_DIR = PROJECT_ROOT / "assets"

DEFAULT_PROGRAM = PROGRAMS_DIR / "VILLA.PBG"
BACKGROUND_IMAGE = ASSETS_DIR / "eureka.bmp"

# Binary layout
START_UNIT_ADDRESS = 0xFC
STANDARD_UNIT_SIZE = 100
FACING_UNIT_SIZE = 400
END_UNIT_TYPE_ID = 4

# Unit type IDs rendered in the program list (legacy compatibility)
DISPLAYED_UNIT_TYPE_IDS: frozenset[int] = frozenset(
    {
        1,
        4,
        6,
        48,
        51,
        52,
        53,
        54,
        161,
        168,
        170,
        171,
        172,
        173,
        180,
    }
)

SUPPORTED_PROGRAM_EXTENSIONS: frozenset[str] = frozenset(
    {
        ".pbg",
        ".pbf",
        ".pbd",
        ".pbe",
        ".pbm",
        ".mzk",
        ".t6m",
        ".m6m",
        ".maz",
    }
)

UNIT_TEMPLATES: dict[str, tuple[str, int]] = {
    "LIN": ("LIN.unit", STANDARD_UNIT_SIZE),
    "TPR": ("TPR.unit", STANDARD_UNIT_SIZE),
    "FACING": ("FACING.unit", FACING_UNIT_SIZE),
}
