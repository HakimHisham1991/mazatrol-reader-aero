"""Application paths and Mazatrol binary layout constants."""

from __future__ import annotations

from pathlib import Path

# Repository / package root (parent of mazatrol_reader/)
PACKAGE_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = PACKAGE_DIR.parent

STRUCTURE_XML = PROJECT_ROOT / "qts200m.xml"
PBF_STRUCTURE_XML = PROJECT_ROOT / "pbf_structure.xml"
PBD_STRUCTURE_XML = PROJECT_ROOT / "pbd_structure.xml"
M6M_STRUCTURE_XML = PROJECT_ROOT / "m6m_structure.xml"
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

PBF_DISPLAYED_UNIT_TYPE_IDS: frozenset[int] = frozenset(
    {
        1,
        4,
        6,
        7,
        19,
        32,
        48,
        51,
        52,
        53,
        54,
        55,
        64,
        99,
        161,
        168,
        170,
        172,
        174,
        176,
        177,
        178,
        180,
        185,
        201,
        202,
    }
)

PBD_DISPLAYED_UNIT_TYPE_IDS: frozenset[int] = frozenset(
    {
        1,
        2,
        4,
        5,
        6,
        12,
        32,
        35,
        38,
        64,
        66,
        68,
        99,
        160,
        161,
        176,
        177,
        178,
        192,
        193,
        194,
    }
)

M6M_DISPLAYED_UNIT_TYPE_IDS: frozenset[int] = frozenset(
    {
        1,
        2,
        4,
        5,
        6,
        32,
        38,
        55,
        66,
        67,
        96,
        99,
        161,
        176,
        177,
        178,
        192,
        193,
        194,
    }
)


def structure_xml_for_extension(extension: str) -> Path:
    ext = extension.lower()
    if ext == ".pbf":
        return PBF_STRUCTURE_XML
    if ext == ".pbd":
        return PBD_STRUCTURE_XML
    if ext == ".m6m":
        return M6M_STRUCTURE_XML
    return STRUCTURE_XML


def displayed_unit_ids_for_extension(extension: str) -> frozenset[int]:
    ext = extension.lower()
    if ext == ".pbf":
        return PBF_DISPLAYED_UNIT_TYPE_IDS
    if ext == ".pbd":
        return PBD_DISPLAYED_UNIT_TYPE_IDS
    if ext == ".m6m":
        return M6M_DISPLAYED_UNIT_TYPE_IDS
    return DISPLAYED_UNIT_TYPE_IDS

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
        ".html",
        ".htm",
    }
)

HTML_EXPORT_EXTENSIONS: frozenset[str] = frozenset({".html", ".htm"})

UNIT_TEMPLATES: dict[str, tuple[str, int]] = {
    "LIN": ("LIN.unit", STANDARD_UNIT_SIZE),
    "TPR": ("TPR.unit", STANDARD_UNIT_SIZE),
    "FACING": ("FACING.unit", FACING_UNIT_SIZE),
}
