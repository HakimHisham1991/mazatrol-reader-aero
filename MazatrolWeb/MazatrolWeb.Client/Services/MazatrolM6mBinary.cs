using System.Buffers.Binary;

namespace MazatrolWeb.Client.Services;

/// <summary>M6M / M640M binary slot layout (type @+0, unit number @+2).</summary>
public static class MazatrolM6mBinary
{
    private static readonly HashSet<int> ManuSnoNums = [1, 8];
    private const int SnoNum = 6;
    private const int FigRectNum = 62;

    public static bool HasWpcCoords(ReadOnlySpan<byte> data, int matAddress)
    {
        if (matAddress + 80 > data.Length)
            return false;

        foreach (var offset in new[] { 66, 70, 74, 78 })
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(data.Slice(matAddress + offset, 4)) != 0)
                return true;
        }

        return false;
    }

    public static bool ShouldStop(int rawType, int unitNum, int unitAddress, int fileSize) =>
        unitAddress + MazatrolConstants.StandardUnitSize > fileSize
        || (rawType == 0 && unitNum == 0);

    public static (int StructureId, bool ExpectSno) ResolveStructureId(
        int slotIndex,
        int rawType,
        int unitNum,
        bool expectSno)
    {
        if (slotIndex == 0 && rawType == 0)
            return (1, false);

        if (rawType is 96 or 99 or 66 or 67 or 32 or 38 or 55 or 6)
            return (rawType, true);

        if (rawType == 5)
            return (5, false);

        if (rawType == 4)
            return (4, false);

        if (rawType == 0)
        {
            if (ManuSnoNums.Contains(unitNum))
                return (161, false);

            if (unitNum == FigRectNum)
                return (193, false);

            if (unitNum == SnoNum)
                return expectSno ? (177, false) : (194, false);
        }

        return (-1, expectSno);
    }

    public static bool IsHeaderUnit(int structureId) =>
        structureId is 1 or 2 or 4 or 5 or 6 or 32 or 38 or 55 or 66 or 67 or 96 or 99;
}
