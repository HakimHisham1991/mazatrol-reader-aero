namespace MazatrolWeb.Client.Services;

/// <summary>In-memory binary edits on Mazatrol program files.</summary>
public sealed class ProgramEditorService
{
    private readonly HttpClient _http;

    public ProgramEditorService(HttpClient http) => _http = http;

    public byte[] Apply(
        byte[] data,
        int unitAddress,
        UnitEditAction action,
        string unitName,
        out byte[]? exportedUnit)
    {
        exportedUnit = null;
        var unitSize = MazatrolConstants.StandardUnitSize;

        if (unitAddress < 0 || unitAddress >= data.Length)
            throw new ArgumentOutOfRangeException(nameof(unitAddress));

        var before = data.AsSpan(0, unitAddress).ToArray();
        var unitBytes = data.AsSpan(unitAddress, Math.Min(unitSize, data.Length - unitAddress)).ToArray();
        var after = data.AsSpan(unitAddress + unitSize).ToArray();

        if (unitBytes.Length < unitSize)
            throw new InvalidOperationException($"Unit at 0x{unitAddress:X} is truncated.");

        return action switch
        {
            UnitEditAction.Delete => before.Concat(after).ToArray(),
            UnitEditAction.Duplicate => before.Concat(unitBytes).Concat(unitBytes).Concat(after).ToArray(),
            UnitEditAction.Export => ExportUnit(unitBytes, out exportedUnit, before, after),
            UnitEditAction.InsertLin => InsertTemplate(before, unitBytes, after, "LIN"),
            UnitEditAction.InsertTpr => InsertTemplate(before, unitBytes, after, "TPR"),
            UnitEditAction.InsertFacing => InsertTemplate(before, unitBytes, after, "FACING"),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
    }

    private static byte[] ExportUnit(byte[] unitBytes, out byte[]? exported, byte[] before, byte[] after)
    {
        exported = unitBytes;
        return before.Concat(unitBytes).Concat(after).ToArray();
    }

    private byte[] InsertTemplate(byte[] before, byte[] unitBytes, byte[] after, string key)
    {
        var template = LoadTemplateAsync(key).GetAwaiter().GetResult();
        return before.Concat(unitBytes).Concat(template).Concat(after).ToArray();
    }

    public async Task<byte[]> LoadTemplateAsync(string key, CancellationToken ct = default)
    {
        if (!MazatrolConstants.UnitTemplates.TryGetValue(key, out var info))
            throw new KeyNotFoundException($"Unknown template: {key}");

        var bytes = await _http.GetByteArrayAsync($"units/{info.FileName}", ct);
        if (bytes.Length < info.Size)
            throw new InvalidOperationException($"Template units/{info.FileName} is too small.");

        return bytes.AsSpan(0, info.Size).ToArray();
    }
}
