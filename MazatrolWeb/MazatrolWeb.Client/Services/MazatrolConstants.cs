namespace MazatrolWeb.Client.Services;

/// <summary>Mazatrol binary layout and supported unit type identifiers.</summary>
public static class MazatrolConstants
{
    public const int StartUnitAddress = 0xFC;
    public const int StandardUnitSize = 100;
    public const int FacingUnitSize = 400;
    public const int EndUnitTypeId = 4;

    public const string PbgStructureXmlPath = "data/pbg_structure.xml";
    public const string PbfStructureXmlPath = "data/pbf_structure.xml";
    public const string PbdStructureXmlPath = "data/pbd_structure.xml";
    public const string M6mStructureXmlPath = "data/m6m_structure.xml";

    public static string StructureXmlPathForExtension(string extension)
    {
        if (extension.Equals(".pbf", StringComparison.OrdinalIgnoreCase))
            return PbfStructureXmlPath;
        if (extension.Equals(".pbd", StringComparison.OrdinalIgnoreCase))
            return PbdStructureXmlPath;
        if (extension.Equals(".m6m", StringComparison.OrdinalIgnoreCase))
            return M6mStructureXmlPath;
        return PbgStructureXmlPath;
    }

    public static readonly HashSet<int> PbgDisplayedUnitTypeIds =
    [
        1, 4, 6, 48, 51, 52, 53, 54, 161, 168, 170, 171, 172, 173, 180
    ];

    public static readonly HashSet<int> PbfDisplayedUnitTypeIds =
    [
        1, 4, 6, 7, 19, 32, 48, 51, 52, 53, 54, 55, 64, 99,
        161, 168, 170, 172, 174, 176, 177, 178, 180, 185, 201, 202
    ];

    public static readonly HashSet<int> PbdDisplayedUnitTypeIds =
    [
        1, 2, 4, 5, 6, 12, 32, 35, 38, 64, 66, 68, 99, 160,
        161, 176, 177, 178, 192, 193, 194
    ];

    public static readonly HashSet<int> M6mDisplayedUnitTypeIds =
    [
        1, 2, 4, 5, 6, 32, 38, 55, 66, 67, 96, 99,
        161, 176, 177, 178, 192, 193, 194
    ];

    public static HashSet<int> DisplayedUnitTypeIdsForExtension(string extension)
    {
        if (extension.Equals(".pbf", StringComparison.OrdinalIgnoreCase))
            return PbfDisplayedUnitTypeIds;
        if (extension.Equals(".pbd", StringComparison.OrdinalIgnoreCase))
            return PbdDisplayedUnitTypeIds;
        if (extension.Equals(".m6m", StringComparison.OrdinalIgnoreCase))
            return M6mDisplayedUnitTypeIds;
        return PbgDisplayedUnitTypeIds;
    }

    /// <summary>Legacy alias — PBG turning program unit filter.</summary>
    public static readonly HashSet<int> DisplayedUnitTypeIds = PbgDisplayedUnitTypeIds;

    public static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pbg", ".pbf", ".pbd", ".pbe", ".pbm", ".mzk", ".t6m", ".m6m", ".maz", ".html"
        };

    public static readonly HashSet<string> HtmlExportExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".html", ".htm" };

    public static readonly IReadOnlyDictionary<string, (string FileName, int Size)> UnitTemplates =
        new Dictionary<string, (string, int)>(StringComparer.OrdinalIgnoreCase)
        {
            ["LIN"] = ("LIN.unit", StandardUnitSize),
            ["TPR"] = ("TPR.unit", StandardUnitSize),
            ["FACING"] = ("FACING.unit", FacingUnitSize),
        };
}
