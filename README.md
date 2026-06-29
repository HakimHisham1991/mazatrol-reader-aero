# Mazatrol Reader

View, edit, and 3D-simulate Mazatrol programs for Mazak CNC lathe/turn machines.

This is a modern Python 3.12+ port of the legacy monolithic `main.py`, refactored into a maintainable package with typed parsing, wxPython UI, and OpenCascade turning simulation.

## Features

- Parse binary Mazatrol programs (`.PBG`, `.PBF`, `.MZK`, …) using `qts200m.xml`
- Display units and parameters in an editable list view
- Edit `readData` parameters in-place (binary write-back)
- Right-click unit operations: delete, duplicate, export, insert LIN/TPR/FACING
- Drag & drop program files onto the window
- 3D turned-part simulation (stock cylinder + boolean cuts for BAR/FACING)

## Project layout

```
mazatrol_reader/          # Application package
  config.py               # Paths and constants
  models.py               # Dataclasses / enums
  parser.py               # XML structure + binary parser
  editor.py               # Unit-level binary edits
  visualization.py        # pythonOCC turning simulation
  gui.py                  # wxPython UI
  __main__.py             # CLI entry point
main.py                   # Thin launcher (backward compatible)
qts200m.xml               # Unit/parameter structure definitions
programs/                 # Sample .PBG files (not included in repo)
units/                    # Insert templates: LIN.unit, TPR.unit, FACING.unit
assets/eureka.bmp         # Optional 3D viewer background
pyproject.toml
requirements.txt
```

## Requirements

| Component | Package | Notes |
|-----------|---------|-------|
| Python | 3.12+ | Required |
| GUI | wxPython 4.2+ | `pip install wxPython` |
| 3D | pythonocc-core 7.8+ | **Use conda-forge on Windows** |

> **Windows note:** `pythonocc-core` is difficult to install via pip on Windows. Use Miniconda/Anaconda:
>
> ```bash
> conda create -n mazatrol python=3.12
> conda activate mazatrol
> conda install -c conda-forge pythonocc-core=7.8.1 wxpython
> pip install -e .
> ```

## Installation

### Option A — uv (recommended for development)

```bash
cd mazatrol-reader-aero
uv venv --python 3.12
uv pip install -e .
# Add OCC separately via conda if needed
```

### Option B — pip

```bash
python -m venv .venv
.venv\Scripts\activate        # Windows
pip install -e .
pip install wxPython
```

### Option C — conda (full stack including 3D)

```bash
conda create -n mazatrol python=3.12 wxpython pythonocc-core=7.8.1 -c conda-forge
conda activate mazatrol
pip install -e .
```

## Running

```bash
# Installed entry point
mazatrol-reader

# Or module
python -m mazatrol_reader

# Legacy launcher
python main.py
```

Place Mazatrol program files in `programs/` or open/drag-drop any supported file.

Click **Play** to run the turning simulation (requires MAT + BAR/FACING units).

## Usage

1. **Open** a program via the combo box, **Open…** button, or drag & drop.
2. **Double-click** yellow `readData` cells to edit values.
3. **Right-click** a unit row for delete / duplicate / export / insert.
4. **Play** builds the 3D part from material stock and BAR figure cuts.
5. Use **ISO / Front / Side / Up** to change the camera.

## Breaking changes from legacy `main.py`

| Legacy | Modern |
|--------|--------|
| Python 2 syntax | Python 3.12+ only |
| Monolithic `main.py` | Package `mazatrol_reader.*` |
| Global `display`, `prgLineAction` | Instance-based GUI callbacks |
| `from OCC.BRepPrimAPI import *` | `from OCC.Core.BRepPrimAPI import …` |
| `wx.PySimpleApp()` | `wx.App(False)` |
| Hard-coded `programs/VILLA.PBG` | Auto-detect first file in `programs/` |
| Absolute pixel layout | `SplitterWindow` + sizers |
| `print` debugging | `logging` module |

## Missing / not yet in repository

The following referenced assets are **not committed** to this repo and must be supplied locally:

- `programs/*.PBG` — sample Mazatrol binaries
- `units/LIN.unit`, `units/TPR.unit`, `units/FACING.unit` — insert templates
- `assets/eureka.bmp` — optional viewer background
- `pbg_structure.xlsx`, `mcode.csv` — referenced in docs but unused by current code

Insert-unit operations fail with a clear error if template files are missing.

## Extending

- Add unit types in `qts200m.xml`, then include their IDs in `config.DISPLAYED_UNIT_TYPE_IDS`
- Add simulation support in `TurningProfileExtractor` and `TurningSimulator`
- New insert templates: extend `config.UNIT_TEMPLATES`

## License

See [LICENSE](LICENSE).
