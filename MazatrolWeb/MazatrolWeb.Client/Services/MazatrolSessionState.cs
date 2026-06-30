namespace MazatrolWeb.Client.Services;

/// <summary>Scoped session holding the loaded program and parsed blocks.</summary>
public sealed class MazatrolSessionState
{
    public byte[]? FileData { get; private set; }
    public string? FileName { get; private set; }
    public IReadOnlyList<ProgramBlock> Blocks { get; private set; } = [];
    public string? SelectedBlockId { get; set; }
    public bool WireframeEnabled { get; set; }

    public event Action? OnChanged;

    public bool HasFile => FileData is { Length: > 0 };

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
