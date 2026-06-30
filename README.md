# Mazatrol Reader / Mazatrol Web

View, edit, and 3D-simulate **Mazatrol** programs for Mazak CNC lathe/turn machines.

**Current version:** v1.5.0 · Developed by UPECA PDC

This repository contains two applications ported from the legacy Python 2 `main.py`:

| Application | Stack | Best for |
|-------------|-------|----------|
| **[Mazatrol Web](#mazatrol-web-blazor-wasm)** | .NET 10 Blazor WASM + Three.js | Browser, offline deploy, no conda |
| **[Mazatrol Reader (desktop)](#mazatrol-reader-python-desktop)** | Python 3.12 + wxPython + pythonOCC | Full OpenCascade boolean simulation |

Both parse binary Mazatrol files using structure definitions in `qts200m.xml` (PBG/turning), `pbf_structure.xml` (PBF/Matrix milling), `pbd_structure.xml` (PBD/Matrix contour milling), and `m6m_structure.xml` (M6M/M640M milling).

---

## Features

- Parse binary Mazatrol programs (`.PBG`, `.PBF`, `.PBD`, `.M6M`, `.MZK`, `.T6M`, …)
- Display units and figures in a program grid (green = parameter names, yellow = values)
- **Undefined vs zero** — unset fields show `N/A` (Mazatrol `@`); defined zeros show `0`
- **PBD Matrix contour** — MAT header (INITIAL-Z, ATC/MULTI modes, PITCH), OFS offset row, TOOL names, FIG PTN patterns, three SNo M-code columns
- **M6M / M640M milling** — MAT + WPC-, FACE MIL, POCKET, DRILLING, CIRC MIL, TAPPING, MANU PRO, FIG/SNo companion slots
- Edit `readData` parameters with binary write-back
- Unit operations: **delete**, **duplicate**, **export**, **insert** LIN / TPR / FACING
- 3D turned-part preview from MAT stock + BAR / FACING toolpaths (optional panel; hidden by default)
- **Loading progress bar** when opening a program file
- Camera presets: ISO, Front, Side, Top; wireframe toggle; STL export (Web)

---

## Supported file extensions

`.pbg` `.pbf` `.pbd` `.pbe` `.pbm` `.mzk` `.t6m` `.m6m` `.maz`

Sample programs (when present locally):

```
SAMPLE_NC_PROGRAM/PBG/     e.g. AXIS28X140.PBG, CONUS.PBG, VILLA.PBG
SAMPLE_NC_PROGRAM/M6M/     e.g. 19011400.M6M (+ matching .html exports)
programs/                  optional copy location for desktop app
```

---

## Project layout

```
mazatrol-reader-aero/
├── README.md                    ← this file
├── qts200m.xml                  ← unit/parameter structure (required)
├── m6m_structure.xlsx           ← M6M structure workbook (reverse-engineered)
├── m6m_structure.xml            ← M6M / M640M milling structure (required for .M6M)
├── mcode.csv                    ← reference data (not wired in code yet)
├── main.py                      ← legacy Python launcher
├── pyproject.toml               ← Python package config
├── requirements.txt
│
├── mazatrol_reader/             ← Python desktop app
│   ├── parser.py                ← binary parser + XML loader
│   ├── editor.py                ← unit-level binary edits
│   ├── visualization.py         ← pythonOCC turning simulation
│   ├── gui.py                   ← wxPython UI
│   └── __main__.py              ← entry point: mazatrol-reader
│
├── MazatrolWeb/                 ← Blazor WebAssembly app
│   ├── ARCHITECTURE.md          ← component / interop diagram
│   ├── README.md                ← Web-specific notes
│   ├── MazatrolWeb.slnx
│   ├── scripts/download-three.ps1
│   └── MazatrolWeb.Client/
│       ├── Pages/Viewer.razor   ← main UI
│       ├── Components/          ← ProgramGrid, FigureEditor, Viewport3D, LoadingOverlay, …
│       ├── Services/            ← MazatrolParser.cs, turning sim, JS interop
│       └── wwwroot/
│           ├── data/qts200m.xml
│           ├── data/m6m_structure.xml
│           ├── js/              ← three-scene.js, interop.js
│           └── lib/three/       ← Three.js r168 (offline)
│
├── programs/                    ← place .PBG files here (desktop)
├── units/                       ← LIN.unit, TPR.unit, FACING.unit templates
├── assets/                      ← optional eureka.bmp background
└── SAMPLE_NC_PROGRAM/           ← sample NC / Mazatrol exports
```

---

## Quick start — which app should I run?

### Mazatrol Web (recommended)

**Requires:** [.NET 10 SDK](https://dotnet.microsoft.com/download) only (Three.js is vendored in repo).

```powershell
cd MazatrolWeb\MazatrolWeb.Client
dotnet restore
dotnet run
```

1. Open the URL from the terminal (e.g. `http://localhost:5101`)
2. Click the **Viewer** icon in the left rail (expand the rail with the chevron if needed)
3. **Open Mazatrol program…** and select a `.PBG`, `.PBF`, or `.PBD` file (progress overlay while loading)
4. Click a **yellow row** in the program grid to select a unit
5. Use **Show 3D panel** if you want the turning preview; click **Play** to simulate

**If Three.js is missing** (blank 3D panel):

```powershell
cd MazatrolWeb
.\scripts\download-three.ps1
```

**If build fails with “file is being used by another process”:**

```powershell
cd MazatrolWeb\MazatrolWeb.Client
.\restart-dev.ps1
```

Or manually: stop all `dotnet run` instances, delete `obj` and `bin`, then `dotnet run` again.

**Offline publish:**

```powershell
cd MazatrolWeb\MazatrolWeb.Client
dotnet publish -c Release -o ./publish
dotnet tool install -g dotnet-serve    # once
dotnet serve --directory ./publish/wwwroot
```

---

### Mazatrol Reader (Python desktop)

**Requires:** Python 3.12+, wxPython, pythonocc-core (conda on Windows).

#### Full install with 3D (Windows)

```powershell
conda create -n mazatrol python=3.12 wxpython pythonocc-core=7.8.1 -c conda-forge
conda activate mazatrol
cd c:\Users\Public\Documents\mazatrol-reader-aero\mazatrol-reader-aero
pip install -e .
```

#### GUI only (no 3D)

```powershell
cd c:\Users\Public\Documents\mazatrol-reader-aero\mazatrol-reader-aero
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install wxPython
pip install -e .
```

#### Run

```powershell
mazatrol-reader
# or
python -m mazatrol_reader
# or
python main.py
```

Place `.PBG` files in `programs/` or open via the file combo / drag-and-drop.

---

## Usage guide

### Mazatrol Web — Viewer page

| Area | Action |
|------|--------|
| **Left rail** | Icon-only by default; click chevron to expand (Home / Viewer) |
| **Open file** | Progress overlay: reading → parsing → building view |
| **Program grid** | Green rows = names, yellow rows = values; click yellow row to select unit |
| **Figure editor** | Edit `readData` fields; read-only / undefined values show `N/A` |
| **3D panel** | Hidden by default; **Show 3D panel** toggle; Play = simulate; ISO/Front/Side/Top; Wireframe; STL export |
| **Layout** | Full-width program grid when 3D hidden; split view when 3D shown |

The lower panel header shows the selected unit, e.g. **`MAT 0xFC`** (material unit at file offset `0xFC`). That is correct when a MAT row is selected.

### Mazatrol Reader — desktop

1. Open a program from the combo box or drag-and-drop a `.PBG` onto the window
2. Double-click yellow `readData` cells to edit
3. Right-click a unit row → delete / duplicate / export / insert LIN·TPR·FACING
4. **Play** → OpenCascade 3D simulation (boolean cuts from BAR figures)
5. Camera buttons: ISO, Front, Side, Up

---

## Data files you may need locally

These are **not always committed** to the repo; add them for full functionality:

| Path | Purpose |
|------|---------|
| `qts200m.xml` | Unit structure definitions (included) |
| `SAMPLE_NC_PROGRAM/PBG/*.PBG` | Sample Mazatrol binaries |
| `programs/*.PBG` | Desktop app default load path |
| `units/LIN.unit` | 100-byte insert template |
| `units/TPR.unit` | 100-byte insert template |
| `units/FACING.unit` | 400-byte insert template |
| `assets/eureka.bmp` | Optional 3D background (desktop) |

Insert-unit operations fail with a clear error if `units/*.unit` templates are missing.

---

## How parsing works

1. Structure loaded from `qts200m.xml` (unit IDs, parameter offsets, types)
2. Binary read starts at address **`0xFC`**, 100 bytes per unit slot
3. Supported unit type IDs are listed in `DISPLAYED_UNIT_TYPE_IDS` (Python: `config.py`, C#: `MazatrolConstants.cs`)
4. Parameter types: `readData`, `wholeNumber`, `readPattern`, `readPbdTool`, `text`, etc.
5. Undefined values (`@` in Mazatrol HTML) → display `N/A`; zero only when the field is defined in binary
6. Simulation extracts **MAT** (stock OD/ID/length), **BAR** figures, **FACING** cuts → 3D profile

---

## 3D simulation notes

| | Mazatrol Web | Mazatrol Reader |
|---|--------------|-----------------|
| Engine | Three.js lathe geometry | pythonOCC boolean CSG |
| Accuracy | Good preview | Closer to original OCC logic |
| Requires | Browser + Three.js | conda `pythonocc-core` |

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Program loads (“11 blocks”) but no rows visible | Hard refresh (`Ctrl+F5`); ensure latest build with `ProgramGrid` |
| Literal text `0x@Block.UnitAddress…` | Fixed in current code — refresh/rebuild |
| `dotnet run` file lock on `rbcswa.dswa.cache.json` | Stop other dev servers; run `restart-dev.ps1` |
| 3D panel empty | Run `MazatrolWeb\scripts\download-three.ps1` |
| Insert unit fails | Add `units/LIN.unit`, `TPR.unit`, `FACING.unit` |
| Simulation error “no MAT unit” | Program must contain a material (MAT) unit |
| `pythonOCC not installed` | Use conda: `conda install -c conda-forge pythonocc-core` |
| Blazor WASM preload warning in console | Harmless dev-server message; ignore |

**Blazor debug hotkey:** `Shift+Alt+D` (when app has focus)

---

## Extending

- Add unit types in `qts200m.xml`, then register IDs in `DISPLAYED_UNIT_TYPE_IDS`
- Python simulation: `TurningProfileExtractor`, `TurningSimulator` in `mazatrol_reader/`
- Web simulation: `TurningProfileExtractor`, `TurningMeshBuilder`, `three-scene.js`
- Web unit handlers: `UnitHandlerRegistry` in `MazatrolWeb.Client/Services/`
- Insert templates: `UNIT_TEMPLATES` / `config.UNIT_TEMPLATES`

See also:

- [MazatrolWeb/ARCHITECTURE.md](MazatrolWeb/ARCHITECTURE.md) — Blazor component graph & JS interop
- [MazatrolWeb/README.md](MazatrolWeb/README.md) — Web-only quick reference

---

## Breaking changes from legacy `main.py`

| Legacy | Modern |
|--------|--------|
| Python 2 monolith | Python 3.12 package + Blazor WASM port |
| `from OCC.BRepPrimAPI import *` | `from OCC.Core.BRepPrimAPI import …` |
| Global `display`, `prgLineAction` | Scoped services / session state |
| `wx.PySimpleApp()` | `wx.App(False)` |
| Single list control UI | Web: ProgramGrid + FigureEditor + Viewport3D |