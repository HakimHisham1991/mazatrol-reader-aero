# Mazatrol Web — Blazor WebAssembly Agent Prompt (.NET 10)

You are a full-stack expert in .NET, Blazor WebAssembly, 3D web rendering, and CNC machining simulation.

**Task**: Build a complete, modern, offline-first Mazatrol viewer and simulator in .NET C# / Blazor WebAssembly from this repo.

---

## Core Requirements

- **Fully offline & self-contained**: Single deployable folder. Host via `dotnet serve`, IIS Express, or package as a desktop app. No internet required after build.
- **Tech Stack** (preferred):
  - **Blazor WebAssembly** (.NET 10) — runs C# in-browser via WASM, no JS framework needed
  - **Three.js** (r168+) via JS interop (`IJSRuntime`) for 3D rendering + OrbitControls
  - **C# binary parser** running directly in WASM (no Rust/Emscripten needed — .NET IS the WASM runtime)
  - **BabylonJS** as alternative to Three.js if preferred (better C# interop story)
  - Optional: **MAUI Blazor Hybrid** for desktop packaging (replaces Tauri/Electron)

---

## Features to Implement

1. Drag & drop or `<InputFile>` component for `.PBG` files.
2. Parse binary Mazatrol program in C# using structures from `qts200m.xml` / `pbg_structure.xlsx` — use `BinaryReader` with struct mappings.
3. Display hierarchical list of Units and Figures using Blazor component tree (similar to original app).
4. Basic editing: delete/duplicate/insert common units (`LIN`, `TPR`, `FACING`, etc.) — bound to C# model with two-way `@bind`.
5. **3D Machining Simulation** via JS interop:
   - Pass parsed geometry data from C# to Three.js scene via `IJSRuntime.InvokeVoidAsync`.
   - Generate stock cylinder; boolean subtractions for cuts.
   - Use `three-bvh-csg` or Manifold (WASM) for CSG operations, called from JS interop layer.
   - Nice lighting, OrbitControls, wireframe + solid toggle.
6. Toolpath preview (optional but nice).
7. Export options (STL, screenshot, JSON) — C# `StreamWriter` / `MemoryStream` for file download via `IJSRuntime`.

---

## Project Structure Goal

```
MazatrolWeb/
├── MazatrolWeb.Client/          ← Blazor WASM standalone project
│   ├── wwwroot/
│   │   ├── index.html
│   │   ├── js/
│   │   │   ├── three-scene.js   ← Three.js scene setup
│   │   │   └── interop.js       ← JS↔C# bridge functions
│   │   └── assets/
│   ├── Pages/
│   │   ├── Home.razor
│   │   └── Viewer.razor
│   ├── Components/
│   │   ├── UnitTree.razor
│   │   ├── FigureEditor.razor
│   │   └── Viewport3D.razor
│   ├── Services/
│   │   ├── MazatrolParser.cs    ← Binary PBG parser
│   │   ├── MazatrolModel.cs     ← Unit/Figure data models
│   │   └── ThreeJsInterop.cs    ← IJSRuntime wrapper
│   └── MazatrolWeb.Client.csproj
├── MazatrolWeb.Server/          ← Optional ASP.NET host (for dev)
└── README.md
```

---

## Constraints & Best Practices

- Heavy geometry math lives in C# (WASM) — use `System.Numerics` for vectors/matrices.
- Use `[JSInvokable]` for callbacks from Three.js back into C# (e.g., object selection events).
- Keep WASM payload small: use IL trimming (`<PublishTrimmed>true</PublishTrimmed>`). Avoid NativeAOT unless strictly needed — it increases build time significantly.
- Avoid JS interop on the hot path — batch geometry data into a single `float[]` or `byte[]` transfer. .NET 10 improves byte-array interop efficiency; prefer `byte[]` over `float[]` for large mesh payloads.
- Thorough XML doc comments on all public APIs; clear separation between parser, model, and rendering layers.
- Make it easy to extend for more unit types by registering handlers in a `Dictionary<string, IUnitHandler>`.
- Use `[ValidatableType]` + `AddValidation()` (new in .NET 10) for any form-based unit editors — AOT-safe, source-generated validation.
- Decorate persistent UI state (e.g. selected unit, camera state) with `[PersistentState]` so it survives page reloads and reconnects automatically.

---

## .NET 10-Specific Notes

- Target `net10.0` in all `.csproj` files.
- **`blazor.boot.json` is gone** — boot config is now embedded directly in `dotnet.js`. Do not reference or parse `blazor.boot.json` anywhere in startup code.
- **Asset fingerprinting**: enable import map generation in `MazatrolWeb.Client.csproj` so static JS assets get cache-busted automatically:
  ```xml
  <WriteImportMapToHtml>true</WriteImportMapToHtml>
  ```
  Then reference fingerprinted scripts in `index.html` using the `#[.{fingerprint}]` syntax:
  ```html
  <script src="_framework/blazor.webassembly#[.{fingerprint}].js"></script>
  ```
- **`<ResourcePreloader />`**: add the new built-in component in `index.html` (or `App.razor`) to preload WASM runtime and assemblies in parallel with page render — eliminates the "white flash" startup delay.
- **JS Interop enhancements**: .NET 10 supports direct JS object manipulation. Use the new APIs where appropriate:
  ```csharp
  var obj = await JS.InvokeConstructorAsync("MyThreeClass", args);
  var val = await JS.GetValueAsync<float>("threeScene.cameraFov");
  await JS.SetValueAsync("threeScene.cameraFov", 60f);
  ```
- **Hot Reload** is now enabled by default for WASM (`WasmEnableHotReload=true` in Debug). No manual setup needed during development.
- **`[PersistentState]` attribute**: replaces the verbose `PersistingComponentStateSubscription` / `PersistentComponentState` boilerplate from .NET 8/9. Use it for any component state that should survive navigation or reconnection:
  ```csharp
  [PersistentState]
  public string? SelectedUnitId { get; set; }
  ```
- **`NotFoundPage`** in `Routes.razor`: use the new attribute instead of a catch-all `<NotFound>` fragment:
  ```razor
  <Router AppAssembly="typeof(Program).Assembly" NotFoundPage="typeof(Pages.NotFound)">
  ```
- **Build-time WASM environment**: set the environment name at publish time rather than via HTTP headers:
  ```xml
  <WasmApplicationEnvironmentName>Production</WasmApplicationEnvironmentName>
  ```
- **MAUI Hybrid**: update the target framework to `net10.0-windows10.0.19041.0` (or the equivalent Android/iOS TFM). New .NET MAUI guidance in .NET 10 covers `BlazorWebView` request interception — useful for loading local `.PBG` files from the native filesystem via a custom URI scheme.
- Use `HttpClient` with base address for any asset loading in standalone WASM mode.

---

## Phased Delivery Plan

### Phase 1 — Parser + Blazor UI Scaffolding
- Blazor WASM standalone project scaffold targeting `net10.0`
- `MazatrolParser.cs`: `BinaryReader`-based `.PBG` parser using `qts200m.xml` field definitions
- `MazatrolModel.cs`: strongly-typed `PbgFile`, `Unit`, `Figure`, `Parameter` record types
- `UnitTree.razor`: collapsible tree view of parsed units/figures
- `FigureEditor.razor`: editable parameter grid per selected figure; use `[ValidatableType]` + `AddValidation()` for AOT-safe form validation
- `<InputFile>` drag-and-drop entry point in `Home.razor`
- `[PersistentState]` on selected unit / viewer state

### Phase 2 — 3D Interop + Simulation
- `ThreeJsInterop.cs`: typed `IJSRuntime` wrapper using .NET 10 enhanced interop APIs (`InvokeConstructorAsync`, `GetValueAsync`, `SetValueAsync`)
- `three-scene.js`: Three.js scene init, stock mesh, CSG pipeline
- `interop.js`: bidirectional bridge — exposes `window.threeScene.*` functions
- `Viewport3D.razor`: hosts `<canvas>` element, calls interop on geometry updates
- Wireframe / solid toggle, OrbitControls, ambient + directional lighting
- Batch mesh transfer as `byte[]` (leverages .NET 10 byte-array interop optimisation)

### Phase 3 — Export + Polish
- STL export via `MemoryStream` → `IJSRuntime` `saveAs` trigger
- JSON round-trip export/import of parsed model
- Screenshot via `canvas.toDataURL` interop call
- `<ResourcePreloader />` and fingerprinted asset references in `index.html` for fast cold-start
- MAUI Hybrid project wrapper for desktop packaging (target `net10.0`)

---

## Build & Setup Instructions

```bash
# Prerequisites
dotnet --version   # Must be 10.0+

# Create solution
dotnet new sln -n MazatrolWeb
dotnet new blazorwasm -n MazatrolWeb.Client --no-https
dotnet sln add MazatrolWeb.Client

# Set target framework in MazatrolWeb.Client.csproj
# <TargetFramework>net10.0</TargetFramework>

# Restore and run
cd MazatrolWeb.Client
dotnet restore
dotnet run

# Publish (offline-ready static output)
dotnet publish -c Release -o ./publish
# Serve from publish/wwwroot — fully static, no server required
```

### `MazatrolWeb.Client.csproj` key properties

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <PublishTrimmed>true</PublishTrimmed>
    <WriteImportMapToHtml>true</WriteImportMapToHtml>
    <WasmApplicationEnvironmentName>Production</WasmApplicationEnvironmentName>
    <!-- WasmEnableHotReload defaults to true for Debug — no entry needed -->
  </PropertyGroup>
</Project>
```

### `index.html` bootstrap (`.NET 10` pattern)

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Mazatrol Web</title>
  <!-- .NET 10: empty importmap filled at build time by WriteImportMapToHtml -->
  <script type="importmap"></script>
  <link rel="stylesheet" href="css/app.css" />
</head>
<body>
  <!-- .NET 10: ResourcePreloader for parallel WASM + assembly download -->
  <component type="typeof(Microsoft.AspNetCore.Components.WebAssembly.ResourcePreloader)"
             render-mode="WebAssemblyPrerendered" />

  <div id="app">Loading...</div>

  <!-- Fingerprinted Blazor bootstrap — cache-busted automatically -->
  <script src="_framework/blazor.webassembly#[.{fingerprint}].js"></script>

  <!-- Three.js (offline: copy to wwwroot/lib/three/) -->
  <script type="module" src="js/three-scene.js"></script>
  <script type="module" src="js/interop.js"></script>
</body>
</html>
```

> For fully offline use, download Three.js and place under `wwwroot/lib/three/` and update the import paths accordingly.

### MAUI Hybrid Packaging (Optional)

```bash
dotnet new maui-blazor -n MazatrolWeb.Maui
dotnet add MazatrolWeb.Maui reference ../MazatrolWeb.Client
# Target .NET 10
dotnet publish MazatrolWeb.Maui -f net10.0-windows10.0.19041.0 -c Release
```

In `MauiProgram.cs`, use `.NET 10` `BlazorWebView` request interception to handle local `.PBG` file access:

```csharp
builder.Services.AddMauiBlazorWebView();
// Intercept file:// requests to serve local PBG files via custom scheme
builder.Services.Configure<BlazorWebViewOptions>(opts =>
{
    opts.RequestInterceptor = async (request, next) =>
    {
        if (request.RequestUri.Scheme == "pbgfile")
            return await LoadLocalPbgAsync(request.RequestUri.LocalPath);
        return await next(request);
    };
});
```

---

## Key Architecture Decisions

| Concern | Choice | Rationale |
|---|---|---|
| WASM runtime | .NET 10 Blazor WASM | C# parser runs natively; no Rust/Emscripten needed |
| 3D rendering | Three.js via `IJSRuntime` | Mature ecosystem; CSG libs are JS-native |
| CSG operations | `three-bvh-csg` (JS) | Stays in JS layer; avoids marshalling complex meshes |
| Desktop packaging | MAUI Hybrid (.NET 10) | Native .NET; request interception for local file access |
| Data transfer | `byte[]` via interop | .NET 10 optimises byte-array boundary crossing specifically |
| State persistence | `[PersistentState]` attribute | No-boilerplate state survival across reloads / reconnects |
| Form validation | `[ValidatableType]` + `AddValidation()` | AOT-safe source-generated validation (new in .NET 10) |
| Asset caching | Fingerprinted JS via import map | Automatic cache-busting; `blazor.boot.json` no longer exists |
| Unit extensibility | `Dictionary<string, IUnitHandler>` | Open/closed — add unit types without touching core |

---

## JS↔C# Interop Reference

### C# → JS (.NET 10 enhanced APIs)

```csharp
// ThreeJsInterop.cs
public async Task UpdateGeometryAsync(byte[] meshBuffer)
    => await JS.InvokeVoidAsync("threeScene.updateGeometry", meshBuffer);

public async Task SetWireframeAsync(bool enabled)
    => await JS.InvokeVoidAsync("threeScene.setWireframe", enabled);

public async Task<string> TakeScreenshotAsync()
    => await JS.InvokeAsync<string>("threeScene.screenshot");

// .NET 10: direct property get/set without round-trip serialization
public async Task<float> GetCameraFovAsync()
    => await JS.GetValueAsync<float>("threeScene.camera.fov");

public async Task SetCameraFovAsync(float fov)
    => await JS.SetValueAsync("threeScene.camera.fov", fov);
```

### JS → C# (selection callbacks)

```javascript
// interop.js
export function onObjectSelected(objectId) {
    DotNet.invokeMethodAsync('MazatrolWeb.Client', 'OnObjectSelected', objectId);
}
```

```csharp
// any Blazor component or service
[JSInvokable]
public static void OnObjectSelected(string objectId)
{
    // Handle selection — update UI state
}
```

### State persistence (.NET 10 pattern)

```csharp
// Viewer.razor — replaces verbose PersistingComponentStateSubscription from .NET 8/9
@code {
    [PersistentState]
    public string? SelectedUnitId { get; set; }

    [PersistentState]
    public bool WireframeEnabled { get; set; }
}
```

---

**Output**: Complete project files delivered in phases as above. Start by creating `ARCHITECTURE.md` covering the full component graph and interop boundary, then implement Phase 1 (parser + UI) before proceeding to the 3D layer.
