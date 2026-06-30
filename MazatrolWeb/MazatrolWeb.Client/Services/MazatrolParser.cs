using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace MazatrolWeb.Client.Services;

/// <summary>Loads unit definitions from qts200m.xml.</summary>
public sealed class StructureLoader
{
    private readonly HttpClient _http;

    public StructureLoader(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<UnitDefinition>> LoadAsync(CancellationToken ct = default)
    {
        var xml = await _http.GetStringAsync(MazatrolConstants.StructureXmlPath, ct);
        var root = XDocument.Parse(xml).Root
            ?? throw new InvalidOperationException("Invalid structure XML.");

        var definitions = new UnitDefinition[257];
        for (var i = 0; i < definitions.Length; i++)
            definitions[i] = new UnitDefinition(i, "TBD", [], false);

        foreach (var unitElem in root.Elements("unit"))
        {
            var idAttr = unitElem.Attribute("id")?.Value;
            if (!int.TryParse(idAttr, out var unitId) || unitId < 0 || unitId >= definitions.Length)
                continue;

            var name = unitElem.Attribute("name")?.Value ?? "TBD";
            var parameters = unitElem.Elements("parameter")
                .Select(ParseParameter)
                .ToList();

            definitions[unitId] = new UnitDefinition(
                unitId,
                name,
                parameters,
                name is "SNo" or "FIG");
        }

        return definitions;
    }

    private static ParameterDefinition ParseParameter(XElement elem)
    {
        var typeName = elem.Attribute("type")?.Value ?? "Unknown";
        var paramType = MapParameterType(typeName);

        var visibleRaw = elem.Attribute("visible")?.Value ?? string.Empty;
        var visibleFor = visibleRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        IReadOnlyList<PatternOption> patternOptions = [];
        if (paramType == ParameterType.ReadPattern)
        {
            patternOptions = elem.Elements("enum")
                .Select(e => new PatternOption(
                    e.Attribute("name")?.Value ?? string.Empty,
                    int.Parse(e.Attribute("value")?.Value ?? "0", CultureInfo.InvariantCulture)))
                .ToList();
        }

        return new ParameterDefinition(
            elem.Attribute("name")?.Value ?? string.Empty,
            int.Parse(elem.Attribute("pos")?.Value ?? "0", CultureInfo.InvariantCulture),
            paramType,
            visibleFor,
            patternOptions);
    }

    private static ParameterType MapParameterType(string xmlType) => xmlType switch
    {
        "NA" => ParameterType.NA,
        "wholeNumber" => ParameterType.WholeNumber,
        "readData" => ParameterType.ReadData,
        "text" => ParameterType.Text,
        "readFullNumber2B" => ParameterType.ReadFullNumber2B,
        "readFullNumber1B" => ParameterType.ReadFullNumber1B,
        "readLetter" => ParameterType.ReadLetter,
        "readPattern" => ParameterType.ReadPattern,
        "partType" => ParameterType.PartType,
        _ => ParameterType.Unknown
    };
}

/// <summary>Low-level binary field access for Mazatrol program bytes.</summary>
public sealed class MazatrolBinaryReader
{
    private readonly byte[] _data;

    public MazatrolBinaryReader(byte[] data) => _data = data;

    public byte ReadByte(int address) => _data[address];

    public ushort ReadUInt16(int address) =>
        BitConverter.ToUInt16(_data, address);

    public float ReadFixedPoint32(int address) =>
        BitConverter.ToUInt32(_data, address) / (float)(1 << 16);

    public float ReadScaledInt(int address) =>
        BitConverter.ToInt32(_data, address) / 10_000f;

    public string ReadText(int address, int length = 16)
    {
        var slice = _data.AsSpan(address, Math.Min(length, _data.Length - address));
        return Encoding.ASCII.GetString(slice).TrimEnd('\0', ' ');
    }

    public char ReadLetter(int address) =>
        (char)('a' + ReadByte(address) - 10);

    public string ReadPattern(int address, IReadOnlyList<PatternOption> options)
    {
        var word = ReadByte(address);
        foreach (var option in options)
        {
            if (word == option.Value)
                return option.Name;
        }
        return "ERR";
    }

    public void WriteScaledInt(int address, float value)
    {
        var packed = (uint)(value * 10_000f);
        BitConverter.TryWriteBytes(_data.AsSpan(address, 4), packed);
    }
}

/// <summary>Parses Mazatrol binary programs using structure definitions.</summary>
public sealed class MazatrolParser
{
    private readonly IReadOnlyList<UnitDefinition> _structure;

    public MazatrolParser(IReadOnlyList<UnitDefinition> structure) => _structure = structure;

    public IReadOnlyList<ProgramBlock> Parse(byte[] data)
    {
        var reader = new MazatrolBinaryReader(data);
        var blocks = new List<ProgramBlock>();
        var index = 0;
        var unitTypeId = -1;

        while (unitTypeId != MazatrolConstants.EndUnitTypeId)
        {
            var unitAddress = MazatrolConstants.StartUnitAddress + index * 100;
            index++;

            if (unitAddress >= data.Length)
                break;

            unitTypeId = reader.ReadByte(unitAddress);
            var unitNumber = reader.ReadByte(unitAddress + 2);
            var definition = _structure[unitTypeId];

            if (!MazatrolConstants.DisplayedUnitTypeIds.Contains(unitTypeId))
                continue;

            blocks.Add(ParseBlock(reader, definition, unitTypeId, unitAddress, unitNumber));
        }

        return blocks;
    }

    private ProgramBlock ParseBlock(
        MazatrolBinaryReader reader,
        UnitDefinition definition,
        int unitTypeId,
        int unitAddress,
        int unitNumber)
    {
        var visiblePattern = string.Empty;
        var ignoreNext = false;
        var parameters = new List<ParameterValue>();

        foreach (var paramDef in definition.Parameters)
        {
            var value = ReadParameterValue(reader, paramDef, unitAddress);

            if (paramDef.ParamType == ParameterType.ReadPattern)
                visiblePattern = value?.ToString() ?? string.Empty;

            if (paramDef.ParamType != ParameterType.ReadPattern)
            {
                if (paramDef.VisibleFor.Count > 0 && !paramDef.VisibleFor.Contains(visiblePattern))
                    value = "*";
            }

            if (ignoreNext)
            {
                value = string.Empty;
                ignoreNext = false;
            }
            else if (unitTypeId == 161 && value?.ToString() == "W")
            {
                value = string.Empty;
                ignoreNext = true;
            }

            parameters.Add(new ParameterValue
            {
                Name = paramDef.Name,
                Value = value!,
                FileOffset = unitAddress + paramDef.Offset,
                ParamType = paramDef.ParamType
            });
        }

        return new ProgramBlock
        {
            UnitTypeId = unitTypeId,
            UnitName = definition.Name,
            UnitNumber = unitNumber,
            UnitAddress = unitAddress,
            IsUnitHeader = !definition.IsSubUnit,
            Parameters = parameters
        };
    }

    private static object ReadParameterValue(
        MazatrolBinaryReader reader,
        ParameterDefinition paramDef,
        int unitAddress)
    {
        var address = unitAddress + paramDef.Offset;

        return paramDef.ParamType switch
        {
            ParameterType.NA => "*",
            ParameterType.WholeNumber => reader.ReadFixedPoint32(address),
            ParameterType.ReadData => reader.ReadScaledInt(address),
            ParameterType.Text => reader.ReadText(address),
            ParameterType.ReadFullNumber2B => reader.ReadUInt16(address),
            ParameterType.ReadFullNumber1B => reader.ReadByte(address),
            ParameterType.ReadLetter => reader.ReadLetter(address).ToString(),
            ParameterType.ReadPattern => reader.ReadPattern(address, paramDef.PatternOptions),
            ParameterType.PartType => reader.ReadByte(address),
            _ when paramDef.Offset == 0 => "?",
            _ => "ERROR"
        };
    }

    public void WriteParameter(byte[] data, int fileOffset, ParameterType paramType, string newValue)
    {
        if (paramType != ParameterType.ReadData)
            throw new InvalidOperationException("Only readData parameters are editable.");

        if (!float.TryParse(newValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            throw new FormatException($"Invalid numeric value: {newValue}");

        new MazatrolBinaryReader(data).WriteScaledInt(fileOffset, parsed);
    }
}
