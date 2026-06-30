namespace MazatrolWeb.Client.Services;

/// <summary>Coordinates structure loading, parsing, and session updates.</summary>
public sealed class MazatrolAppService
{
    private readonly StructureLoader _structureLoader;
    private readonly MazatrolSessionState _session;
    private IReadOnlyList<UnitDefinition>? _structure;
    private MazatrolParser? _parser;
    private string _loadedExtension = ".pbg";

    public MazatrolAppService(StructureLoader structureLoader, MazatrolSessionState session)
    {
        _structureLoader = structureLoader;
        _session = session;
    }

    public MazatrolParser Parser => _parser
        ?? throw new InvalidOperationException("Structure not loaded yet.");

    private async Task EnsureStructureForExtensionAsync(string extension, CancellationToken ct)
    {
        if (_parser is not null && extension.Equals(_loadedExtension, StringComparison.OrdinalIgnoreCase))
            return;

        _structure = await _structureLoader.LoadAsync(extension, ct);
        _parser = new MazatrolParser(_structure);
        _loadedExtension = extension;
    }

    public async Task EnsureReadyAsync(CancellationToken ct = default)
    {
        await EnsureStructureForExtensionAsync(".pbg", ct);
    }

    public async Task LoadProgramAsync(byte[] data, string fileName, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName);
        IReadOnlyList<ProgramBlock> blocks;

        if (MazatrolConstants.HtmlExportExtensions.Contains(extension))
        {
            _session.SetLoadingProgress(40, "Decoding HTML export…");
            var html = System.Text.Encoding.UTF8.GetString(data);
            _session.SetLoadingProgress(65, "Parsing HTML export…");
            blocks = MazatrolHtmlParser.Parse(html);
        }
        else
        {
            _session.SetLoadingProgress(40, "Loading structure definition…");
            await EnsureStructureForExtensionAsync(extension, ct);
            _session.SetLoadingProgress(70, "Parsing program…");
            blocks = _parser!.Parse(data, extension);
            await Task.Yield();
        }

        _session.SetLoadingProgress(90, "Building program view…");
        _session.Load(data, fileName, blocks);
    }

    public IReadOnlyList<ProgramBlock> Reparse(byte[] data)
    {
        if (_parser is null)
            throw new InvalidOperationException("Structure not loaded yet.");
        return _parser.Parse(data, _loadedExtension);
    }
}
