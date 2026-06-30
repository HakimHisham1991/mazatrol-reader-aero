# Mazatrol Web — Architecture

Offline-first Blazor WebAssembly viewer and turning simulator for Mazatrol `.PBG` programs.

## Component graph

```
┌─────────────────────────────────────────────────────────────────┐
│  Pages/Viewer.razor                                             │
│  ├─ FileDropZone        ← InputFile + drag/drop                 │
│  ├─ UnitTree            ← hierarchical unit/figure list         │
│  ├─ FigureEditor        ← editable readData parameters          │
│  └─ Viewport3D          ← canvas + Three.js via IJSRuntime      │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  MazatrolSessionState (scoped)                                  │
│  • byte[] FileData, FileName, List<ProgramBlock>                │
└─────────────────────────────────────────────────────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
   MazatrolParser      ProgramEditorService   TurningProfileExtractor
   (BinaryReader)      (in-memory bytes)      → TurningSimulationInput
          │                                       │
          │                                       ▼
          │                              TurningMeshBuilder
          │                              → SimulationMeshDto (JSON)
          │                                       │
          └───────────────────────────────────────┼──────────────┐
                                                  ▼              ▼
                                          ThreeJsInterop    FileDownloadService
                                                  │
                                                  ▼
                                    wwwroot/js/interop.js
                                    wwwroot/js/three-scene.js
                                                  │
                                                  ▼
                                         Three.js + OrbitControls
```

## Interop boundary

| Direction | Payload | Purpose |
|-----------|---------|---------|
| C# → JS | `SimulationMeshDto` (JSON) | Stock dims + lathe profile points |
| C# → JS | `bool` | Wireframe toggle |
| C# → JS | trigger | Screenshot, STL export |
| JS → C# | `[JSInvokable]` | Optional selection callbacks |

Heavy parsing runs in .NET WASM. CSG/lathe mesh construction runs in Three.js to avoid marshalling complex meshes.

## Extensibility

Register unit handlers in `UnitHandlerRegistry`:

```csharp
_handlers["BAR"] = new BarUnitHandler();
_handlers["FACING"] = new FacingUnitHandler();
```

## Data files (wwwroot)

| Path | Purpose |
|------|---------|
| `data/qts200m.xml` | Unit/parameter structure definitions |
| `units/*.unit` | Insert templates (LIN, TPR, FACING) |
| `lib/three/` | Vendored Three.js r168 (offline) |

## Build & deploy

```bash
cd MazatrolWeb/MazatrolWeb.Client
dotnet run
dotnet publish -c Release -o ./publish
# Serve publish/wwwroot statically (dotnet serve, IIS, MAUI Hybrid)
```
