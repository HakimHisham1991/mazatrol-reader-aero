using System.Globalization;

namespace MazatrolWeb.Client.Services;

/// <summary>Display rules for Mazatrol parameter values (@ vs defined zero).</summary>
public static class MazatrolParameterFormatter
{
    public const string NotApplicable = "N/A";

    /// <summary>SNo blocks store optional readData "defined" flags at byte +30.</summary>
    private static readonly IReadOnlyDictionary<int, byte> ReadDataDefinedFlagByOffset = new Dictionary<int, byte>
    {
        [48] = 0b0000_0100,
        [52] = 0b0000_0100,
        [56] = 0b0000_0001,
        [64] = 0b0000_0010,
    };

    public static bool IsPlaceholder(object? value) =>
        value is null
        || value is string s && (s.Length == 0 || s is "*" or "?" or NotApplicable);

    public static bool IsReadDataDefined(int unitAddress, int offset, int rawScaledInt, byte[] data)
    {
        if (rawScaledInt != 0)
            return true;

        if (unitAddress + 30 >= data.Length)
            return false;

        if (!ReadDataDefinedFlagByOffset.TryGetValue(offset, out var flagBit))
            return false;

        return (data[unitAddress + 30] & flagBit) != 0;
    }

    public static bool IsNumericDefined(ParameterType paramType, object value, bool rawDefined = true) =>
        paramType switch
        {
            ParameterType.NA => false,
            ParameterType.ReadData => rawDefined,
            ParameterType.ReadFullNumber2B or ParameterType.ReadFullNumber1B => Convert.ToInt64(value) != 0,
            ParameterType.WholeNumber => rawDefined,
            _ => true
        };

    public static string Format(object? value, ParameterType paramType, bool isDefined)
    {
        if (IsPlaceholder(value) || !isDefined)
            return NotApplicable;

        return value switch
        {
            float f => FormatFloat(f),
            double d => FormatFloat(d),
            _ => value?.ToString() ?? NotApplicable
        };
    }

    public static string FormatFloat(double value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);
}
