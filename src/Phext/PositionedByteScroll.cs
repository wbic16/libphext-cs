// ===========================================================================================================
// libphext-cs: The Fourth Root
// PositionedByteScroll.cs - A scroll of raw bytes paired with its coordinate location
//
// Copyright: (c) 2024-2025 Will Bickford (Phext, Inc.)
// License: MIT
// ===========================================================================================================

namespace Phext;

/// <summary>
/// Represents a scroll of raw (uninterpreted) bytes positioned at a coordinate in phext space.
/// The byte-level twin of <see cref="PositionedScroll"/>: scroll bodies are carried verbatim,
/// valid UTF-8 or not. Mirrors the (Coordinate, Vec&lt;u8&gt;) pairs of libphext-rs
/// binary::phokenize_bytes.
/// </summary>
public class PositionedByteScroll : IComparable<PositionedByteScroll>, IEquatable<PositionedByteScroll>
{
    public Coordinate Coord { get; set; }
    public byte[] Bytes { get; set; }

    public PositionedByteScroll()
    {
        Coord = new Coordinate();
        Bytes = Array.Empty<byte>();
    }

    public PositionedByteScroll(Coordinate coord, byte[] bytes)
    {
        Coord = new Coordinate(coord);
        Bytes = bytes;
    }

    public PositionedByteScroll(PositionedByteScroll other)
    {
        Coord = new Coordinate(other.Coord);
        Bytes = (byte[])other.Bytes.Clone();
    }

    public int CompareTo(PositionedByteScroll? other)
    {
        if (other is null) return 1;
        return Coord.CompareTo(other.Coord);
    }

    public bool Equals(PositionedByteScroll? other)
    {
        if (other is null) return false;
        return Coord.Equals(other.Coord) && Bytes.AsSpan().SequenceEqual(other.Bytes);
    }

    public override bool Equals(object? obj) => Equals(obj as PositionedByteScroll);

    public override int GetHashCode() => HashCode.Combine(Coord, Bytes.Length);

    public static bool operator ==(PositionedByteScroll? left, PositionedByteScroll? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(PositionedByteScroll? left, PositionedByteScroll? right) => !(left == right);
}
