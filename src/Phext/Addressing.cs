// ===========================================================================================================
// libphext-cs: The Fourth Root
// Addressing.cs - Line/column addressing types (0.6.1)
//
// Ported from: https://github.com/wbic16/libphext-rs (src/phext.rs, line/column
// addressing section, v0.6.1; range syntax ratified by Will 2026-11-15 —
// RANGE-SYNTAX-PROPOSAL.md in the ports program workspace).
// Copyright: (c) 2024-2025 Will Bickford (Phext, Inc.)
// License: MIT
// ===========================================================================================================

using System.Text;

namespace Phext;

/// <summary>
/// A caret boundary inside one scroll: 1-based line and column. Column 1 is
/// the boundary before the line's first character; a line of n characters has
/// valid columns 1..n+1. Columns count Unicode scalar values.
/// </summary>
public struct ScrollPosition : IEquatable<ScrollPosition>, IComparable<ScrollPosition>
{
    public int Line { get; set; }
    public int Column { get; set; }

    public ScrollPosition(int line, int column)
    {
        Line = line;
        Column = column;
    }

    public int CompareTo(ScrollPosition other) =>
        (Line, Column).CompareTo((other.Line, other.Column));

    public bool Equals(ScrollPosition other) => Line == other.Line && Column == other.Column;
    public override bool Equals(object? obj) => obj is ScrollPosition other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Line, Column);
    public static bool operator ==(ScrollPosition left, ScrollPosition right) => left.Equals(right);
    public static bool operator !=(ScrollPosition left, ScrollPosition right) => !left.Equals(right);
    public override string ToString() => $"{Line},{Column}";
}

/// <summary>
/// One parsed point address: universe (1-based; 1 when the "$N:" prefix is
/// absent), the 9D coordinate, an optional caret position, and an optional
/// forward offset in characters (Unicode scalar values).
/// </summary>
public class PointAddress : IEquatable<PointAddress>
{
    public int Universe { get; set; }
    public Coordinate Coordinate { get; set; }
    public ScrollPosition? Position { get; set; }
    public int? Offset { get; set; }

    public PointAddress(int universe, Coordinate coordinate, ScrollPosition? position, int? offset)
    {
        Universe = universe;
        Coordinate = new Coordinate(coordinate);
        Position = position;
        Offset = offset;
    }

    /// <summary>
    /// Canonical form: "$1:" is never written (universe 1 is bare), integers
    /// are in minimal decimal form, and there is no whitespace.
    /// </summary>
    public override string ToString()
    {
        var result = new StringBuilder();
        if (Universe > 1) result.Append($"${Universe}:");
        result.Append(Coordinate.ToString());
        if (Position is ScrollPosition pos)
        {
            result.Append($"@{pos.Line},{pos.Column}");
            if (Offset is int offset) result.Append($"+{offset}");
        }
        return result.ToString();
    }

    public bool Equals(PointAddress? other) =>
        other is not null && Universe == other.Universe && Coordinate == other.Coordinate &&
        Position == other.Position && Offset == other.Offset;

    public override bool Equals(object? obj) => Equals(obj as PointAddress);
    public override int GetHashCode() => HashCode.Combine(Universe, Coordinate, Position, Offset);
}

/// <summary>
/// A parsed range: two point endpoints, normalized to ascending order at
/// parse time (direction is discarded, per the ratified spec).
/// </summary>
public class RangeAddress : IEquatable<RangeAddress>
{
    public PointAddress Start { get; set; }
    public PointAddress End { get; set; }

    public RangeAddress(PointAddress start, PointAddress end)
    {
        Start = start;
        End = end;
    }

    public override string ToString() => $"{Start}-{End}";

    public bool Equals(RangeAddress? other) =>
        other is not null && Start.Equals(other.Start) && End.Equals(other.End);

    public override bool Equals(object? obj) => Equals(obj as RangeAddress);
    public override int GetHashCode() => HashCode.Combine(Start, End);
}

/// <summary>
/// Text helpers that count Unicode scalar values (runes), never UTF-16 code
/// units and never bytes — the unit the addressing layer is specified in.
/// C# strings are UTF-16, so every count and slice here walks runes.
/// </summary>
internal static class ScalarText
{
    /// <summary>The number of Unicode scalar values in the text.</summary>
    public static int Count(string text)
    {
        int count = 0;
        foreach (Rune _ in text.EnumerateRunes()) count++;
        return count;
    }

    /// <summary>
    /// Resolves a caret position inside one scroll to a character (scalar)
    /// offset. Null when the line or column is past the scroll's extent —
    /// never clamps. Lines split on 0x0A, which stays inside the scroll.
    /// </summary>
    public static int? ResolveOffset(string text, ScrollPosition pos)
    {
        if (pos.Line < 1 || pos.Column < 1) return null;
        int line = 1;
        int column = 1;
        int offset = 0;
        foreach (Rune ch in text.EnumerateRunes())
        {
            if (line == pos.Line && column == pos.Column) return offset;
            if (ch.Value == '\n') { line += 1; column = 1; }
            else { column += 1; }
            offset += 1;
        }
        if (line == pos.Line && column == pos.Column) return offset;
        return null;
    }

    /// <summary>
    /// The substring between scalar offsets [start, end). Callers guarantee
    /// 0 &lt;= start &lt;= end &lt;= Count(text).
    /// </summary>
    public static string Slice(string text, int start, int end)
    {
        if (start >= end) return "";
        var result = new StringBuilder();
        int index = 0;
        foreach (Rune ch in text.EnumerateRunes())
        {
            if (index >= end) break;
            if (index >= start) result.Append(ch.ToString());
            index += 1;
        }
        return result.ToString();
    }
}
