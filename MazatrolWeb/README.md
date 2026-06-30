# Mazatrol Web

Browser-based Mazatrol viewer and turning simulator (.NET 10 Blazor WASM + Three.js).

> **Full documentation** (both apps, troubleshooting, sample files): see the [root README](../README.md).

## Quick run

```powershell
cd MazatrolWeb\MazatrolWeb.Client
dotnet restore
dotnet run
```

Open the URL shown (e.g. `http://localhost:5101`) → **Viewer** → open a `.PBG` file → **Play**.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Three.js r168 under `wwwroot/lib/three/` (included after `scripts/download-three.ps1`)

## Dev server issues

```powershell
cd MazatrolWeb\MazatrolWeb.Client
.\restart-dev.ps1
```

Stops stray `dotnet` processes, cleans `obj`/`bin`, rebuilds, and runs.

## Offline publish

```powershell
dotnet publish -c Release -o ./publish
dotnet tool install -g dotnet-serve
dotnet serve --directory ./publish/wwwroot
```

## UI layout (Viewer page)

- Collapsible **icon rail** nav (default: icons only; chevron to expand)
- **~62.5%** width: program grid + unit editor
- **~37.5%** width: Three.js 3D viewport
- Program grid: green title rows, yellow data rows

## Key files

| Path | Role |
|------|------|
| `Pages/Viewer.razor` | Main layout |
| `Components/ProgramGrid.razor` | Mazatrol program table |
| `Components/FigureEditor.razor` | Parameter edit + unit ops |
| `Components/Viewport3D.razor` | 3D canvas |
| `Services/MazatrolParser.cs` | Binary `.PBG` parser |
| `Services/TurningSimulation.cs` | Profile extraction + mesh DTO |
| `wwwroot/js/three-scene.js` | Three.js lathe rendering |
| `wwwroot/data/qts200m.xml` | Structure definitions |

## Architecture

See [ARCHITECTURE.md](ARCHITECTURE.md) for the component graph and C# ↔ JavaScript interop boundary.

## Python desktop app

The wxPython + pythonOCC version lives in [`mazatrol_reader/`](../mazatrol_reader/) at the repo root.
