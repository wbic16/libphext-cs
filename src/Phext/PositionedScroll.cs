// ===========================================================================================================
// libphext-cs: The Fourth Root
// PositionedScroll.cs - A scroll of text paired with its coordinate location
//
// Copyright: (c) 2024-2025 Will Bickford (Phext, Inc.)
// License: MIT
// ===========================================================================================================

namespace Phext;

/// <summary>
/// Represents a scroll of text positioned at a specific coordinate in phext space
/// </summary>
public class PositionedScroll : IComparable<PositionedScroll>, IEquatable<PositionedScroll>
{
    public Coordinate Coord { get; set; }
    public string Scroll { get; set; }

    public PositionedScroll()
    {
        Coord = new Coordinate();
        Scroll = "";
    }

    public PositionedScroll(Coordinate coord, string scroll)
    {
        Coord = new Coordinate(coord);
        Scroll = scroll;
    }

    public PositionedScroll(PositionedScroll other)
    {
        Coord = new Coordinate(other.Coord);
        Scroll = other.Scroll;
    }

    public int CompareTo(PositionedScroll? other)
    {
        if (other is null) return 1;
        return Coord.CompareTo(other.Coord);
    }

    public bool Equals(PositionedScroll? other)
    {
        if (other is null) return false;
        return Coord.Equals(other.Coord) && Scroll == other.Scroll;
    }

    public override bool Equals(object? obj) => Equals(obj as PositionedScroll);

    public override int GetHashCode() => HashCode.Combine(Coord, Scroll);

    public override string ToString()
    {
        var preview = Scroll.Length > 4 ? Scroll[..4] : Scroll;
        return $"{Coord}: {preview}";
    }

    public static bool operator ==(PositionedScroll? left, PositionedScroll? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(PositionedScroll? left, PositionedScroll? right) => !(left == right);
}
