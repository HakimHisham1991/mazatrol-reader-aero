using System.Globalization;

namespace MazatrolWeb.Client.Services;

/// <summary>Extracts stock and bar turning data from parsed program blocks.</summary>
public static class TurningProfileExtractor
{
    public static TurningSimulationInput Extract(IReadOnlyList<ProgramBlock> blocks)
    {
        var result = new TurningSimulationInput();
        var unitContext = string.Empty;
        var barStartZ = 0.0;
        var figureIndex = 0;

        foreach (var block in blocks)
        {
            if (block.IsUnitHeader)
            {
                unitContext = block.UnitName;
                switch (unitContext)
                {
                    case "MAT":
                        result.Stock = new MaterialStock(
                            ToDouble(block.Get("OD")),
                            ToDouble(block.Get("ID")),
                            ToDouble(block.Get("Length")),
                            ToDouble(block.Get("Workface")));
                        break;
                    case "FACING":
                        barStartZ = ToDouble(block.Get("FIN-Z"));
                        break;
                    case "BAR":
                        barStartZ = ToDouble(block.Get("CPT-Z"));
                        break;
                }
                continue;
            }

            if (block.UnitName != "FIG")
                continue;

            if (unitContext == "FACING")
            {
                result.Facing = new FacingCut(
                    ToDouble(block.Get("SPT-X")),
                    ToDouble(block.Get("SPT-Z")));
                continue;
            }

            if (unitContext != "BAR")
                continue;

            figureIndex++;
            var finishX = ToDouble(block.Get("FPT-X"));
            var finishZ = ToDouble(block.Get("FPT-Z"));
            var startCorner = ToDouble(block.Get("S-CNR"));
            var finishCorner = ToDouble(block.Get("F-CNR/$"));

            var startXRaw = block.Get("SPT-X");
            var startZRaw = block.Get("SPT-Z");

            double startX, startZ;
            if (startXRaw?.ToString() == "*")
            {
                startX = finishX;
                startZ = barStartZ;
            }
            else
            {
                startX = ToDouble(startXRaw);
                startZ = startZRaw?.ToString() == "*" ? barStartZ : ToDouble(startZRaw);
            }

            result.BarFigures.Add(new BarFigure(
                figureIndex, startX, startZ, finishX, finishZ, startCorner, finishCorner));

            barStartZ = finishZ;
        }

        return result;
    }

    private static double ToDouble(object? value)
    {
        return value switch
        {
            null => 0,
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var p) => p,
            _ => Convert.ToDouble(value, CultureInfo.InvariantCulture)
        };
    }
}

/// <summary>Builds a lathe profile DTO for Three.js from simulation input.</summary>
public static class TurningMeshBuilder
{
    public static SimulationMeshDto Build(TurningSimulationInput input)
    {
        if (input.Stock is null)
            throw new InvalidOperationException("Program has no MAT unit.");

        var stock = input.Stock;
        var profile = new List<ProfilePoint>();

        // Face side (Z=0): stock OD, then facing cut if present
        var faceRadius = stock.Od / 2;
        if (input.Facing is { FinishX: > 0 } facing)
            faceRadius = Math.Min(faceRadius, facing.FinishX / 2);

        profile.Add(new ProfilePoint(faceRadius, 0));
        profile.Add(new ProfilePoint(faceRadius, stock.Workface));

        // Walk BAR figures along Z axis
        var currentZ = stock.Workface;
        var currentRadius = faceRadius;

        foreach (var fig in input.BarFigures)
        {
            var segStartZ = currentZ + (fig.StartZ - (profile.Count > 1 ? 0 : 0));
            // Map figure Z coordinates relative to workface
            var z0 = stock.Workface + fig.StartZ;
            var z1 = stock.Workface + fig.FinishZ;
            var r0 = fig.StartX / 2;
            var r1 = fig.FinishX / 2;

            if (profile.Count == 0 || Math.Abs(profile[^1].AxialZ - z0) > 0.001)
                profile.Add(new ProfilePoint(currentRadius, z0));

            profile.Add(new ProfilePoint(r0, z0));
            profile.Add(new ProfilePoint(r1, z1));
            currentRadius = r1;
            currentZ = z1;
        }

        // Close at stock length
        var totalLength = stock.Length + stock.Workface;
        if (profile.Count == 0 || Math.Abs(profile[^1].AxialZ - totalLength) > 0.001)
            profile.Add(new ProfilePoint(currentRadius, totalLength));

        profile.Add(new ProfilePoint(stock.Od / 2, totalLength));

        return new SimulationMeshDto
        {
            StockOd = stock.Od,
            StockId = stock.InnerDiameter,
            StockLength = stock.Length,
            Workface = stock.Workface,
            Facing = input.Facing,
            Profile = profile
        };
    }
}
