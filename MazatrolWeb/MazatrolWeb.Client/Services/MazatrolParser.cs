using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace MazatrolWeb.Client.Services;

/// <summary>Loads unit definitions from structure XML (e.g. pbg_structure.xml).</summary>
public sealed class StructureLoader
{
    private readonly HttpClient _http;

    public StructureLoader(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<UnitDefinition>> LoadAsync(
        string? fileExtension = null,
        CancellationToken ct = default)
    {
        var path = MazatrolConstants.StructureXmlPathForExtension(fileExtension ?? ".pbg");
        var xml = await _http.GetStringAsync(path, ct);
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
                name is "SNo" or "FIG" or "OFS");
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
        if (paramType is ParameterType.ReadPattern or ParameterType.ReadPbdTool)
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
        "readPbdTool" => ParameterType.ReadPbdTool,
        "readPbdMultiFlag" => ParameterType.ReadPbdMultiFlag,
        "partType" => ParameterType.PartType,
        _ => ParameterType.Unknown
    };
}

/// <summary>Low-level binary field access for Mazatrol program bytes.</summary>
public sealed class MazatrolBinaryReader
{
    private readonly byte[] _data;

    public MazatrolBinaryReader(byte[] data) => _data = data;

    public byte[] Data => _data;

    public byte ReadByte(int address) => _data[address];

    public ushort ReadUInt16(int address) =>
        BitConverter.ToUInt16(_data, address);

    public float ReadFixedPoint32(int address) =>
        BitConverter.ToUInt32(_data, address) / (float)(1 << 16);

    public float ReadScaledInt(int address) =>
        BitConverter.ToInt32(_data, address) / 10_000f;

    public int ReadScaledIntRaw(int address) =>
        BitConverter.ToInt32(_data, address);

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

    /// <summary>PBD SNo tool type: packed (byte+9, byte+13) within the 100-byte unit block.</summary>
    public string ReadPbdTool(int unitAddress, IReadOnlyList<PatternOption> options)
    {
        var key = (ReadByte(unitAddress + 9) << 8) | ReadByte(unitAddress + 13);
        foreach (var option in options)
        {
            if (key == option.Value)
                return option.Name;
        }
        return "ERR";
    }

    /// <summary>PBD MAT MULTI FLAG: TYPE when MULTI MODE byte +9 is OFFSET (3).</summary>
    public (string Value, bool IsDefined) ReadPbdMultiFlag(int unitAddress)
    {
        var mode = ReadByte(unitAddress + 9);
        return mode == 3 ? ("TYPE", true) : ("*", false);
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

    public IReadOnlyList<ProgramBlock> Parse(byte[] data, string? fileExtension = null)
    {
        var extension = fileExtension ?? ".pbg";
        if (extension.Equals(".m6m", StringComparison.OrdinalIgnoreCase))
            return ParseM6m(data);

        var reader = new MazatrolBinaryReader(data);
        var blocks = new List<ProgramBlock>();
        var index = 0;
        var unitTypeId = -1;
        var displayedIds = MazatrolConstants.DisplayedUnitTypeIdsForExtension(extension);

        while (unitTypeId != MazatrolConstants.EndUnitTypeId)
        {
            var unitAddress = MazatrolConstants.StartUnitAddress + index * 100;
            index++;

            if (unitAddress >= data.Length)
                break;

            unitTypeId = reader.ReadByte(unitAddress);
            var unitNumber = reader.ReadByte(unitAddress + 2);
            var definition = _structure[unitTypeId];

            if (!displayedIds.Contains(unitTypeId))
                continue;

            blocks.Add(ParseBlock(reader, definition, unitTypeId, unitAddress, unitNumber));
        }

        return blocks;
    }

    private IReadOnlyList<ProgramBlock> ParseM6m(byte[] data)
    {
        var reader = new MazatrolBinaryReader(data);
        var blocks = new List<ProgramBlock>();
        var displayedIds = MazatrolConstants.M6mDisplayedUnitTypeIds;
        var index = 0;
        var expectSno = false;
        var headerUno = -1;
        var matAddress = MazatrolConstants.StartUnitAddress;

        while (true)
        {
            var unitAddress = MazatrolConstants.StartUnitAddress + index * 100;
            var slotIndex = index;
            index++;

            var rawType = reader.ReadByte(unitAddress);
            var rawNum = reader.ReadByte(unitAddress + 2);

            if (MazatrolM6mBinary.ShouldStop(rawType, rawNum, unitAddress, data.Length))
                break;

            var (structureId, nextExpectSno) = MazatrolM6mBinary.ResolveStructureId(
                slotIndex, rawType, rawNum, expectSno);
            expectSno = nextExpectSno;

            if (structureId < 0 || !displayedIds.Contains(structureId))
                continue;

            var definition = _structure[structureId];
            int unitNumber;
            if (MazatrolM6mBinary.IsHeaderUnit(structureId))
            {
                headerUno += 1;
                unitNumber = headerUno;
            }
            else
            {
                unitNumber = rawNum;
            }

            if (slotIndex == 0)
                matAddress = unitAddress;

            blocks.Add(ParseBlock(reader, definition, structureId, unitAddress, unitNumber));

            if (structureId == 1 && MazatrolM6mBinary.HasWpcCoords(data, matAddress))
            {
                headerUno += 1;
                var wpcDef = _structure[2];
                blocks.Add(ParseBlock(reader, wpcDef, 2, matAddress, headerUno));
            }
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
            var (value, isDefined) = ReadParameterValue(reader, paramDef, unitAddress, unitTypeId);

            if (paramDef.ParamType == ParameterType.ReadPattern)
                visiblePattern = value?.ToString() ?? string.Empty;

            if (paramDef.ParamType != ParameterType.ReadPattern)
            {
                if (paramDef.VisibleFor.Count > 0 && !paramDef.VisibleFor.Contains(visiblePattern))
                {
                    value = "*";
                    isDefined = false;
                }
            }

            if (ignoreNext)
            {
                value = string.Empty;
                isDefined = false;
                ignoreNext = false;
            }
            else if (unitTypeId == 161 && value?.ToString() == "W")
            {
                value = string.Empty;
                isDefined = false;
                ignoreNext = true;
            }

            parameters.Add(new ParameterValue
            {
                Name = paramDef.Name,
                Value = value!,
                FileOffset = unitAddress + paramDef.Offset,
                ParamType = paramDef.ParamType,
                IsDefined = isDefined
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

    private static (object Value, bool IsDefined) ReadParameterValue(
        MazatrolBinaryReader reader,
        ParameterDefinition paramDef,
        int unitAddress,
        int unitTypeId)
    {
        var address = unitAddress + paramDef.Offset;

        switch (paramDef.ParamType)
        {
            case ParameterType.NA:
                return ("*", false);

            case ParameterType.WholeNumber:
            {
                var raw = BitConverter.ToUInt32(reader.Data, address);
                return (reader.ReadFixedPoint32(address), raw != 0);
            }

            case ParameterType.ReadData:
            {
                var raw = reader.ReadScaledIntRaw(address);
                var isDefined = MazatrolParameterFormatter.IsReadDataDefined(
                    unitAddress, paramDef.Offset, raw, reader.Data);
                if (unitTypeId == 160)
                    isDefined = true;
                return (reader.ReadScaledInt(address), isDefined);
            }

            case ParameterType.Text:
                return (reader.ReadText(address), true);

            case ParameterType.ReadFullNumber2B:
            {
                var raw = reader.ReadUInt16(address);
                var isDefined = raw != 0 || paramDef.Name == "ATC MODE";
                return (raw, isDefined);
            }

            case ParameterType.ReadFullNumber1B:
            {
                var raw = reader.ReadByte(address);
                return (raw, raw != 0);
            }

            case ParameterType.ReadLetter:
                return (reader.ReadLetter(address).ToString(), true);

            case ParameterType.ReadPattern:
                return (reader.ReadPattern(address, paramDef.PatternOptions), true);

            case ParameterType.ReadPbdTool:
                return (reader.ReadPbdTool(unitAddress, paramDef.PatternOptions), true);

            case ParameterType.ReadPbdMultiFlag:
                return reader.ReadPbdMultiFlag(unitAddress);

            case ParameterType.PartType:
            {
                var raw = reader.ReadByte(address);
                return (raw, raw != 0);
            }

            default:
                if (paramDef.Offset == 0)
                    return ("?", false);
                return ("ERROR", false);
        }
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
