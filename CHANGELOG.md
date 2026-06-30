# Changelog

All notable changes to Mazatrol Web follow [Semantic Versioning](https://semver.org/).

## [2.1.0] - 2026-06-30

### Changed
- Renamed `qts200m.xml` → `pbg_structure.xml` (root and `wwwroot/data/`) to match `pbf_structure.xml`, `pbd_structure.xml`, and `m6m_structure.xml` naming

## [2.0.0] - 2026-06-30

### Removed
- Python desktop app (`mazatrol_reader/`, wxPython, pythonOCC)
- Legacy `main.py`, `pyproject.toml`, `requirements.txt`, Python tests, and structure build scripts
- Blazor template pages (`Counter`, `Weather`) and unused `UnitTree` component
- Root `programs/` and `units/` folders (templates now ship under `wwwroot/units/`)

### Added
- **`wwwroot/units/`** — LIN, TPR, and FACING insert templates (regenerate via `MazatrolWeb/scripts/gen-unit-templates.ps1`)

### Changed
- Repository is **Mazatrol Web only** (.NET 10 Blazor WASM standalone)
- Documentation consolidated for web-only deployment
- Version policy in `.cursor/rules/revision-control.mdc` no longer tracks Python package versions

## [1.5.0] - 2026-06-29

### Added
- **M6M / M640M milling program support** — binary parser for `.M6M` files (Python + Blazor)
- **`m6m_structure.xlsx` / `m6m_structure.xml`** — reverse-engineered from 18 sample pairs in `SAMPLE_NC_PROGRAM/M6M/` (`.M6M` + `.html`)
- M6M slot resolver — 100-byte stride from `@0xFC`, type `@+0`, unit number `@+2`; companion slots for SNo/FIG
- Synthetic **WPC-** block from MAT workpiece coordinates at `@+66…+78`
- **`tools/build_m6m_structure.py`** — regenerates structure spreadsheet and XML from HTML + binary samples
- **`MazatrolM6mBinary.cs`** — C# port of M6M slot layout helpers

## [1.4.2] - 2026-06-30

### Changed
- Numeric parameter display rounded to **4 decimal places** maximum (Python + Blazor formatters)

## [1.4.1] - 2026-06-30

### Added
- **`readPbdMultiFlag`** parameter type — PBD MAT **MULTI FLAG** (`TYPE` when MULTI MODE is OFFSET)
- PBD **OFS** offset table unit (type 160) — X, Y, th, Z row under MAT header

### Changed
- PBD **MAT** structure — Matrix header columns (`MAT.`, INITIAL-Z, ATC MODE, MULTI MODE, MULTI FLAG, PITCH-X/Y) replace turning fields (OD, ID, Length, Workface, RPM)
- Material text offset corrected to **+84** for PBD MAT blocks

### Fixed
- PBD program header missing/wrong data — `18205300.PBD` and samples now show MAT + OFS rows matching Mazatrol HTML export
- **ATC MODE** `0` displays as `0` (defined zero), not `N/A`
- OFS coordinate fields display defined zero as `0`

## [1.4.0] - 2026-06-30

### Added
- **Loading progress overlay** when opening a program — staged progress bar (read file → load structure → parse → build view)
- **`readPbdTool`** parameter type — decodes PBD SNo tool names from packed bytes at offsets +9 and +13
- **`MazatrolParameterFormatter`** (C#) / `parameter_formatter.py` (Python) — central rules for `N/A` vs numeric display

### Changed
- Unified Python `__version__` in `mazatrol_reader/__init__.py` with `pyproject.toml` (1.4.0)
- **3D viewport** hidden by default; **Show / Hide 3D panel** toggle on Viewer page
- Figure editor: 80% font size; read-only parameter values shown in white
- PBD SNo structure: three **M** columns at binary offsets 24, 26, and 32 (was one column)
- `pbd_structure.xml` FIG **PTN** enums for types 192/193/194 (PT, SQR, LINE, ARC, CW, CCW)

### Fixed
- PBD SNo **TOOL** column blank — tool type is not ASCII at offset +84; mapped via `readPbdTool` (DRILL, FCE MILL, END MILL, BAL EMIL, …)
- PBD FIG **PTN** column showing `ERR` — added missing pattern enum values from HTML/binary validation
- **Undefined vs zero** — unset fields display `N/A` (like Mazatrol `@`); defined zero displays `0` (uses byte +30 flags on SNo blocks where applicable)

## [1.3.0] - 2026-06-29

### Added
- **PBD file type support** — Matrix contour milling programs use `pbd_structure.xml` (LINE LFT/CTR/IN, INDEX, DRILLING, CIRC MIL, REAMING, POCKET, milling FIG/SNo units)
- `pbd_structure.xlsx` — reverse-engineered unit/parameter schema from all 15 samples in `SAMPLE_NC_PROGRAM/PBD/`
- `tools/build_pbd_structure.py` to regenerate `pbd_structure.xlsx` and `pbd_structure.xml` from HTML + binary validation

### Changed
- Structure loader selects `pbd_structure.xml` for `.pbd` (in addition to `.pbg` → `qts200m.xml`, `.pbf` → `pbf_structure.xml`)

## [1.2.1] - 2026-06-29

### Fixed
- PBF binary parsing now uses correct Matrix unit type IDs from real `.PBF` files: POCKET=99, SNo=178, FIG=201, HEAD=19 (was incorrectly guessed as 56/181/169/13), fixing programs that showed only MAT+END

## [1.2.0] - 2026-06-29

### Added
- **PBF file type support** — Matrix milling programs use `pbf_structure.xml` (extends turning structure with POCKET, HEAD, DRILLING, milling FIG/SNo units)
- `pbf_structure.xlsx` — reverse-engineered unit/parameter schema from all 10 samples in `SAMPLE_NC_PROGRAM/PBF/`
- Mazatrol HTML export import (`.html`) for PBF sample programs when binary `.PBF` files are unavailable
- `tools/build_pbf_structure.py` to regenerate `pbf_structure.xlsx` and `pbf_structure.xml` from HTML samples

### Changed
- Structure loader selects `qts200m.xml` for `.pbg` and `pbf_structure.xml` for `.pbf`
- File picker accepts `.html` / `.htm` Mazatrol Matrix exports

## [1.1.3] - 2026-06-29

### Fixed
- Version and credit labels in `AppPageHeader` now render correctly (`v1.1.3` instead of literal `v@AppInfo.Version`)

## [1.1.2] - 2026-06-29

### Added
- Version label (`v1.1.2`) and credit line (“Developed by UPECA PDC”) below page headers in Mazatrol Web
- `AppPageHeader` component shared by Home and Viewer pages

### Changed
- Unified product version to `1.1.2` across Blazor client, Python package, and `pyproject.toml`

## [1.1.1] - 2026-06-29

### Changed
- Viewer layout: program panel ~62.5% width, 3D viewer ~37.5% (more space for program grid)
- Collapsible icon-only navigation rail (expand via chevron)

### Fixed
- Razor expression for unit address display (`MAT 0xFC` instead of literal `@Block.UnitAddress…`)
- `InputText` crash in figure editor (replaced with plain `<input>`)

## [1.1.0] - 2026-06-29

### Added
- Mazatrol Web — Blazor WASM port (.NET 10 + Three.js)
- `ProgramGrid` — green/yellow Mazatrol program table matching legacy list view
- C# binary parser, in-memory unit editing, lathe 3D simulation

## [1.0.0] - 2026-06-29

### Added
- Python 3.12 desktop port (`mazatrol_reader`) from legacy `main.py`
- wxPython UI, pythonOCC turning simulation, modular parser/editor

[2.1.0]: https://github.com/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/compare/v1.5.0...v2.0.0
[1.5.0]: https://github.com/compare/v1.4.2...v1.5.0
[1.4.2]: https://github.com/compare/v1.4.1...v1.4.2
[1.4.1]: https://github.com/compare/v1.4.0...v1.4.1
[1.4.0]: https://github.com/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/compare/v1.2.1...v1.3.0
[1.2.1]: https://github.com/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/compare/v1.1.3...v1.2.0
[1.1.3]: https://github.com/compare/v1.1.2...v1.1.3
[1.1.2]: https://github.com/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/releases/tag/v1.0.0
