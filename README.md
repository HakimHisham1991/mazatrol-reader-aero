# Mazatrol Web

View, edit, and 3D-simulate **Mazatrol** programs for Mazak CNC lathe/turn and milling machines.

**Current version:** v2.1.0 · Developed by UPECA PDC

Offline-first **.NET 10 Blazor WebAssembly** app with Three.js turning preview. No Python, conda, or server required after build.

Parses binary Mazatrol files using structure definitions in `wwwroot/data/pbg_structure.xml` (PBG/turning), `pbf_structure.xml` (PBF/Matrix milling), `pbd_structure.xml` (PBD/Matrix contour milling), and `m6m_structure.xml` (M6M/M640M milling).

---

## Features

- Parse binary Mazatrol programs (`.PBG`, `.PBF`, `.PBD`, `.M6M`, `.MZK`, `.T6M`, …)
- Import Mazatrol **HTML exports** (`.html`, `.htm`)
- Display units and figures in a program grid (green = parameter names, yellow = values)
- **Undefined vs zero** — unset fields show `N/A` (Mazatrol `@`); defined zeros show `0`
- **PBD Matrix contour** — MAT header, OFS offset row, TOOL names, FIG PTN patterns, three SNo M-code columns
- **M6M / M640M milling** — MAT + WPC-, FACE MIL, POCKET, DRILLING, CIRC MIL, TAPPING, MANU PRO, FIG/SNo companion slots
- Edit `readData` parameters with binary write-back
- Unit operations: **delete**, **duplicate**, **export**, **insert** LIN / TPR / FACING
- 3D turned-part preview from MAT stock + BAR / FACING toolpaths (optional panel; hidden by default)
- **Loading progress bar** when opening a program file
- Camera presets: ISO, Front, Side, Top; wireframe toggle; STL export

---

## Supported file extensions

`.pbg` `.pbf` `.pbd` `.pbe` `.pbm` `.mzk` `.t6m` `.m6m` `.maz` `.html` `.htm`

Sample programs (when present locally):

```
SAMPLE_NC_PROGRAM/PBF/     HTML Mazatrol exports
SAMPLE_NC_PROGRAM/PBD/     HTML + binary samples
SAMPLE_NC_PROGRAM/M6M/     M6M samples
```

---

## Project layout

```
mazatrol-reader-aero/
├── README.md
├── CHANGELOG.md
├── pbg_structure.xml            ← PBG / turning structure (sync to wwwroot/data/)
├── pbf_structure.xml
├── pbd_structure.xml
├── m6m_structure.xml
├── m6m_structure.xlsx
├── mcode.csv                    ← reference data (not wired in code yet)
├── MazatrolWeb/
│   ├── ARCHITECTURE.md
│   ├── README.md
│   ├── MazatrolWeb.slnx
│   ├── scripts/
│   │   ├── download-three.ps1
│   │   └── gen-unit-templates.ps1
│   └── MazatrolWeb.Client/
│       ├── Pages/Viewer.razor
│       ├── Components/          ← ProgramGrid, FigureEditor, Viewport3D, …
│       ├── Services/            ← MazatrolParser.cs, turning sim, JS interop
│       └── wwwroot/
│           ├── data/            ← structure XML (runtime)
│           ├── units/           ← LIN.unit, TPR.unit, FACING.unit
│           ├── js/              ← three-scene.js, interop.js
│           └── lib/three/       ← Three.js r168 (offline)
└── SAMPLE_NC_PROGRAM/
```

---

## Quick start

**Requires:** [.NET 10 SDK](https://dotnet.microsoft.com/download) only (Three.js is vendored in repo).

```powershell
cd MazatrolWeb\MazatrolWeb.Client
dotnet restore
dotnet run
```

1. Open the URL from the terminal (e.g. `http://localhost:5101`)
2. Click the **Viewer** icon in the left rail (expand the rail with the chevron if needed)
3. **Open Mazatrol program…** and select a `.PBG`, `.PBF`, `.PBD`, `.M6M`, or `.html` export
4. Click a **yellow row** in the program grid to select a unit
5. Use **Show 3D panel** for turning preview; click **Play** to simulate

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

**Offline publish:**

```powershell
cd MazatrolWeb\MazatrolWeb.Client
dotnet publish -c Release -o ./publish
dotnet tool install -g dotnet-serve    # once
dotnet serve --directory ./publish/wwwroot
```

---

## Usage — Viewer page

| Area | Action |
|------|--------|
| **Left rail** | Icon-only by default; click chevron to expand (Home / Viewer) |
| **Open file** | Progress overlay: reading → parsing → building view |
| **Program grid** | Green rows = names, yellow rows = values; click yellow row to select unit |
| **Figure editor** | Edit `readData` fields; read-only / undefined values show `N/A` |
| **3D panel** | Hidden by default; **Show 3D panel** toggle; Play = simulate; ISO/Front/Side/Top; Wireframe; STL export |
| **Layout** | Full-width program grid when 3D hidden; split view when 3D shown |

The lower panel header shows the selected unit, e.g. **`MAT 0xFC`** (material unit at file offset `0xFC`).

---

## How parsing works

1. Structure loaded from `wwwroot/data/*.xml` (unit IDs, parameter offsets, types)
2. Binary read starts at address **`0xFC`**, 100 bytes per unit slot
3. Supported unit type IDs are listed in `MazatrolConstants.cs` per file extension
4. Parameter types: `readData`, `wholeNumber`, `readPattern`, `readPbdTool`, `text`, etc.
5. Undefined values (`@` in Mazatrol HTML) → display `N/A`; zero only when the field is defined in binary
6. Simulation extracts **MAT** (stock OD/ID/length), **BAR** figures, **FACING** cuts → Three.js lathe profile

---

## 3D simulation

Turning preview uses **Three.js lathe geometry** (good visual preview). Parsing and editing work for all supported file types; 3D simulation is for turning programs with MAT + BAR/FACING toolpaths.

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Program loads (“11 blocks”) but no rows visible | Hard refresh (`Ctrl+F5`); rebuild |
| `dotnet run` file lock on `rbcswa.dswa.cache.json` | Stop other dev servers; run `restart-dev.ps1` |
| 3D panel empty | Run `MazatrolWeb\scripts\download-three.ps1` |
| Insert unit fails | Regenerate templates: `MazatrolWeb\scripts\gen-unit-templates.ps1` |
| Simulation error “no MAT unit” | Program must contain a material (MAT) unit |
| Blazor WASM preload warning in console | Harmless dev-server message; ignore |

**Blazor debug hotkey:** `Shift+Alt+D` (when app has focus)

---

## Extending

- Add unit types in structure XML, then register IDs in `MazatrolConstants.cs`
- Turning simulation: `TurningProfileExtractor`, `TurningMeshBuilder`, `three-scene.js`
- Unit handlers: `UnitHandlerRegistry` in `MazatrolWeb.Client/Services/`
- Insert templates: `MazatrolConstants.UnitTemplates` and `wwwroot/units/`

See also:

- [MazatrolWeb/ARCHITECTURE.md](MazatrolWeb/ARCHITECTURE.md) — component graph & JS interop
- [MazatrolWeb/README.md](MazatrolWeb/README.md) — quick reference
