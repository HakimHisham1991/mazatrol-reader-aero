using Microsoft.JSInterop;

namespace MazatrolWeb.Client.Services;

/// <summary>Typed wrapper around Three.js interop functions.</summary>
public sealed class ThreeJsInterop
{
    private readonly IJSRuntime _js;

    public ThreeJsInterop(IJSRuntime js) => _js = js;

    public ValueTask InitializeAsync(string canvasId) =>
        _js.InvokeVoidAsync("mazatrolThree.init", canvasId);

    public ValueTask UpdateSimulationAsync(SimulationMeshDto mesh) =>
        _js.InvokeVoidAsync("mazatrolThree.updateSimulation", mesh);

    public ValueTask SetWireframeAsync(bool enabled) =>
        _js.InvokeVoidAsync("mazatrolThree.setWireframe", enabled);

    public ValueTask ViewIsoAsync() =>
        _js.InvokeVoidAsync("mazatrolThree.viewIso");

    public ValueTask ViewFrontAsync() =>
        _js.InvokeVoidAsync("mazatrolThree.viewFront");

    public ValueTask ViewSideAsync() =>
        _js.InvokeVoidAsync("mazatrolThree.viewSide");

    public ValueTask ViewTopAsync() =>
        _js.InvokeVoidAsync("mazatrolThree.viewTop");

    public async Task<string> TakeScreenshotAsync() =>
        await _js.InvokeAsync<string>("mazatrolThree.screenshot");

    public ValueTask ExportStlAsync() =>
        _js.InvokeVoidAsync("mazatrolThree.exportStl");

    public ValueTask ResizeAsync() =>
        _js.InvokeVoidAsync("mazatrolThree.resize", "mazatrol-viewport");
}

/// <summary>Triggers browser file downloads from WASM.</summary>
public sealed class FileDownloadService
{
    private readonly IJSRuntime _js;

    public FileDownloadService(IJSRuntime js) => _js = js;

    public ValueTask DownloadBytesAsync(string fileName, byte[] data, string contentType = "application/octet-stream") =>
        _js.InvokeVoidAsync("mazatrolFiles.downloadBytes", fileName, data, contentType);

    public ValueTask DownloadTextAsync(string fileName, string content, string contentType = "application/json") =>
        _js.InvokeVoidAsync("mazatrolFiles.downloadText", fileName, content, contentType);
}
