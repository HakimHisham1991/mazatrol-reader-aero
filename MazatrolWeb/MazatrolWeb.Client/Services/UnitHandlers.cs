namespace MazatrolWeb.Client.Services;

/// <summary>Extensibility hook for unit-type-specific behaviour.</summary>
public interface IUnitHandler
{
    string UnitName { get; }
    void OnUnitLoaded(ProgramBlock block, TurningSimulationInput simulation);
}

public sealed class MatUnitHandler : IUnitHandler
{
    public string UnitName => "MAT";

    public void OnUnitLoaded(ProgramBlock block, TurningSimulationInput simulation)
    {
        simulation.Stock = new MaterialStock(
            TurningProfileExtractorToDouble(block.Get("OD")),
            TurningProfileExtractorToDouble(block.Get("ID")),
            TurningProfileExtractorToDouble(block.Get("Length")),
            TurningProfileExtractorToDouble(block.Get("Workface")));
    }

    private static double TurningProfileExtractorToDouble(object? v) =>
        v switch
        {
            double d => d,
            float f => f,
            int i => i,
            _ => 0
        };
}

public sealed class UnitHandlerRegistry
{
    private readonly Dictionary<string, IUnitHandler> _handlers;

    public UnitHandlerRegistry(IEnumerable<IUnitHandler> handlers) =>
        _handlers = handlers.ToDictionary(h => h.UnitName, StringComparer.Ordinal);

    public IUnitHandler? Get(string unitName) =>
        _handlers.TryGetValue(unitName, out var handler) ? handler : null;
}
