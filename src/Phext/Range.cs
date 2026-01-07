// ===========================================================================================================
// libphext-cs: The Fourth Root
// Range.cs - An inclusive range across phext subspace
//
// Copyright: (c) 2024-2025 Will Bickford (Phext, Inc.)
// License: MIT
// ===========================================================================================================

namespace Phext;

/// <summary>
/// Represents an inclusive range of coordinates in phext space
/// </summary>
public class Range
{
    public Coordinate Start { get; set; }
    public Coordinate End { get; set; }

    public Range()
    {
        Start = new Coordinate();
        End = new Coordinate();
    }

    public Range(Coordinate start, Coordinate end)
    {
        Start = new Coordinate(start);
        End = new Coordinate(end);
    }

    public Range(string startCoord, string endCoord)
    {
        Start = Coordinate.FromString(startCoord);
        End = Coordinate.FromString(endCoord);
    }

    public override string ToString() => $"{Start}-{End}";
}
