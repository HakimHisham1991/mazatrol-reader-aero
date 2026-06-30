# Changelog

All notable changes to Mazatrol Reader / Mazatrol Web follow [Semantic Versioning](https://semver.org/).

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

[1.1.3]: https://github.com/compare/v1.1.2...v1.1.3
[1.1.2]: https://github.com/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/releases/tag/v1.0.0
