namespace MazatrolWeb.Client.Services;

/// <summary>Scoped session holding the loaded program and parsed blocks.</summary>
public sealed class MazatrolSessionState
{
    public byte[]? FileData { get; private set; }
    public string? FileName { get; private set; }
    public IReadOnlyList<ProgramBlock> Blocks { get; private set; } = [];
    public string? SelectedBlockId { get; set; }
    public bool WireframeEnabled { get; set; }
    public bool IsLoading { get; private set; }
    public string LoadingMessage { get; private set; } = string.Empty;
    public int LoadingPercent { get; private set; }

    public event Action? OnChanged;

    public bool HasFile => FileData is { Length: > 0 };

    public void BeginLoading(string message)
    {
        IsLoading = true;
        LoadingMessage = message;
        LoadingPercent = 0;
        Notify();
    }

    public void SetLoadingProgress(int percent, string? message = null)
    {
        LoadingPercent = Math.Clamp(percent, 0, 100);
        if (message is not null)
            LoadingMessage = message;
        if (!IsLoading)
            IsLoading = true;
        Notify();
    }

    public void EndLoading()
    {
        IsLoading = false;
        LoadingMessage = string.Empty;
        LoadingPercent = 0;
        Notify();
    }

    public void Load(byte[] data, string fileName, IReadOnlyList<ProgramBlock> blocks)
    {
        FileData = data;
        FileName = fileName;
        Blocks = blocks;
        SelectedBlockId ??= blocks.FirstOrDefault()?.Id;
        Notify();
    }

    public void UpdateData(byte[] data, IReadOnlyList<ProgramBlock> blocks)
    {
        FileData = data;
        Blocks = blocks;
        Notify();
    }

    public ProgramBlock? SelectedBlock =>
        Blocks.FirstOrDefault(b => b.Id == SelectedBlockId);

    public void SelectBlock(string? blockId)
    {
        SelectedBlockId = blockId;
        Notify();
    }

    private void Notify() => OnChanged?.Invoke();
}
