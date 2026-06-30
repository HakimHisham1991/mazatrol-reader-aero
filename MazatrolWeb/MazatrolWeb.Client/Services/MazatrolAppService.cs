namespace MazatrolWeb.Client.Services;

/// <summary>Coordinates structure loading, parsing, and session updates.</summary>
public sealed class MazatrolAppService
{
    private readonly StructureLoader _structureLoader;
    private readonly MazatrolSessionState _session;
    private IReadOnlyList<UnitDefinition>? _structure;
    private MazatrolParser? _parser;

    public MazatrolAppService(StructureLoader structureLoader, MazatrolSessionState session)
    {
        _structureLoader = structureLoader;
        _session = session;
    }

    public MazatrolParser Parser => _parser
        ?? throw new InvalidOperationException("Structure not loaded yet.");

    public async Task EnsureReadyAsync(CancellationToken ct = default)
    {
        if (_parser is not null)
            return;

        _structure = await _structureLoader.LoadAsync(ct);
        _parser = new MazatrolParser(_structure);
    }

    public async Task LoadProgramAsync(byte[] data, string fileName, CancellationToken ct = default)
    {
        await EnsureReadyAsync(ct);
        var blocks = _parser!.Parse(data);
        _session.Load(data, fileName, blocks);
    }

    public IReadOnlyList<ProgramBlock> Reparse(byte[] data)
    {
        if (_parser is null)
            throw new InvalidOperationException("Structure not loaded yet.");
        return _parser.Parse(data);
    }
}
