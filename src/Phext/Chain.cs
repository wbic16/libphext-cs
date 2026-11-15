// ===========================================================================================================
// libphext-cs: The Fourth Root
// Chain.cs - Stream-level chaining of whole phexts (0.6.0)
//
// Ported from: https://github.com/wbic16/libphext-rs (src/phext.rs, chain layer)
// Copyright: (c) 2024-2025 Will Bickford (Phext, Inc.)
// License: MIT
// ===========================================================================================================

using System.Text;

namespace Phext;

/// <summary>
/// A chain: an ordered series of complete phexts within one stream, joined by
/// CHAIN_BREAK (0x02) and optionally terminated by CHAIN_CLOSE (0x03). The chain
/// layer is a stream-level layer, one rung above the library break: the nine
/// dimension breaks structure a single phext, while the two chain bytes structure
/// a stream of phexts. Each member is a whole phext, worked with the ordinary
/// phext functions; read in succession, a chain's members are editions in
/// sequence — the chain is how a phext stream encodes time.
/// </summary>
/// <remarks>
/// <see cref="Closed"/> records how the chain ended: true when a CHAIN_CLOSE
/// terminated it, false when the stream simply ran out (EOF). A chain that ends
/// at EOF is unclosed — possibly truncated — and is never silently promoted to
/// closed. A stream with no chain bytes at all is a single unclosed chain holding
/// one member phext: every pre-0.6 phext reads this way.
/// </remarks>
public class Chain
{
    /// <summary>The member phexts of this chain, oldest first.</summary>
    public List<string> Members { get; set; } = new List<string>();

    /// <summary>True when a CHAIN_CLOSE terminated this chain; false at EOF.</summary>
    public bool Closed { get; set; }

    public Chain()
    {
    }

    public Chain(List<string> members, bool closed)
    {
        Members = members;
        Closed = closed;
    }

    /// <summary>
    /// Serializes this chain alone: its members joined by CHAIN_BREAK, with a
    /// trailing CHAIN_CLOSE when the chain is closed. See
    /// <see cref="PhextEngine.SerializeChains"/> for whole streams.
    /// </summary>
    public string Serialize()
    {
        var result = new StringBuilder();
        bool first = true;
        foreach (var member in Members)
        {
            if (!first) result.Append(PhextConstants.CHAIN_BREAK);
            first = false;
            result.Append(member);
        }
        if (Closed) result.Append(PhextConstants.CHAIN_CLOSE);
        return result.ToString();
    }

    /// <summary>
    /// Fetches a scroll by multiverse address: "$N:coord", or a bare coordinate
    /// (universe 1). Out-of-range universes, missing scrolls, and malformed
    /// universe prefixes all yield an empty string, as
    /// <see cref="PhextEngine.Fetch(string, Coordinate)"/> does for a miss —
    /// the same value as a present-but-empty scroll.
    /// </summary>
    public string Fetch(string address)
    {
        var parsed = PhextEngine.ParseUniverseAddress(address);
        if (parsed is null) return "";
        var (universe, coord) = parsed.Value;
        if (universe < 1 || universe > Members.Count) return "";
        return PhextEngine.Fetch(Members[universe - 1], coord);
    }

    /// <summary>
    /// The scroll text at a coordinate in one member phext, in the phokenize
    /// sense of presence: a coordinate is present exactly when Phokenize
    /// enumerates it.
    /// </summary>
    private static string? AddressedScrollText(string member, Coordinate target)
    {
        foreach (var ps in PhextEngine.Phokenize(member))
        {
            if (ps.Coord == target) return ps.Scroll;
        }
        return null;
    }

    /// <summary>
    /// Resolves a point address against this chain: (universe, coordinate,
    /// character offset within the scroll). A point without "@" resolves to
    /// offset 0 of its scroll. Null when the universe or scroll is absent or
    /// the position is out of range — never clamps. Offsets count Unicode
    /// scalar values.
    /// </summary>
    public (int Universe, Coordinate Coordinate, int Offset)? ResolvePoint(PointAddress point)
    {
        if (point.Universe < 1 || point.Universe > Members.Count) return null;
        string? text = AddressedScrollText(Members[point.Universe - 1], point.Coordinate);
        if (text is null) return null;
        int offset;
        if (point.Position is ScrollPosition pos)
        {
            int? resolved = ScalarText.ResolveOffset(text, pos);
            if (resolved is null) return null;
            offset = resolved.Value;
        }
        else
        {
            offset = 0;
        }
        return (point.Universe, point.Coordinate, offset);
    }

    /// <summary>
    /// Fetches a point address string. A bare point is a caret: it fetches
    /// the empty string. The offset form fetches the selected characters:
    /// from the point, forward <c>offset</c> characters (Unicode scalar
    /// values) within the scroll, terminating at the scroll boundary. Any
    /// parse or resolution failure fetches the empty string, as
    /// <see cref="Fetch(string)"/> does.
    /// </summary>
    public string FetchPoint(string address)
    {
        PointAddress? point = PhextEngine.ParsePointAddress(address);
        if (point is null) return "";
        if (point.Offset is not int count) return "";
        var resolved = ResolvePoint(point);
        if (resolved is null) return "";
        string text = AddressedScrollText(Members[point.Universe - 1], point.Coordinate) ?? "";
        int start = resolved.Value.Offset;
        int end = Math.Min(start + count, ScalarText.Count(text));
        return ScalarText.Slice(text, start, end);
    }

    /// <summary>
    /// Resolves a range against this chain into per-scroll segments:
    /// (universe, coordinate, segment text) in ascending order. Middle
    /// scrolls contribute their whole text; the endpoint scrolls contribute
    /// from/to their endpoint boundaries (an endpoint without "@"
    /// contributes its whole scroll). Null when either endpoint fails to
    /// resolve — an out-of-range endpoint is an error, never a clamp.
    /// </summary>
    public List<(int Universe, Coordinate Coordinate, string Text)>? ResolveRange(RangeAddress range)
    {
        PointAddress start = range.Start;
        PointAddress end = range.End;
        if (start.Universe < 1 || end.Universe > Members.Count || start.Universe > end.Universe) return null;

        // both endpoint scrolls must be present, and both endpoint positions must resolve
        var startCheck = ResolvePoint(new PointAddress(start.Universe, start.Coordinate, start.Position, null));
        if (startCheck is null) return null;
        var endCheck = ResolvePoint(new PointAddress(end.Universe, end.Coordinate, end.Position, null));
        if (endCheck is null) return null;

        var segments = new List<(int Universe, Coordinate Coordinate, string Text)>();
        for (int universe = start.Universe; universe <= end.Universe; universe++)
        {
            string member = Members[universe - 1];
            var scrolls = PhextEngine.Phokenize(member);
            scrolls.Sort();
            foreach (var ps in scrolls)
            {
                Coordinate coord = ps.Coord;
                if (universe == start.Universe && coord < start.Coordinate) continue;
                if (universe == end.Universe && coord > end.Coordinate) continue;
                int lo = 0;
                int hi = ScalarText.Count(ps.Scroll);
                if (universe == start.Universe && coord == start.Coordinate)
                {
                    if (start.Position is ScrollPosition spos)
                    {
                        int? resolvedLo = ScalarText.ResolveOffset(ps.Scroll, spos);
                        if (resolvedLo is null) return null;
                        lo = resolvedLo.Value;
                    }
                }
                if (universe == end.Universe && coord == end.Coordinate)
                {
                    if (end.Position is ScrollPosition epos)
                    {
                        int? resolvedHi = ScalarText.ResolveOffset(ps.Scroll, epos);
                        if (resolvedHi is null) return null;
                        hi = resolvedHi.Value;
                    }
                }
                if (lo > hi) return null;
                segments.Add((universe, coord, ScalarText.Slice(ps.Scroll, lo, hi)));
            }
        }
        return segments;
    }

    /// <summary>
    /// Fetches a range address string: the spanned text with phext structure
    /// bytes preserved — the covered segments re-assembled at their
    /// coordinates into a phext (chain) fragment (scroll breaks between
    /// covered scrolls inside a universe, CHAIN_BREAK between universes,
    /// never a trailing CHAIN_CLOSE: a fragment is an excerpt, not a
    /// terminated chain), so the result round-trips as a phext fragment.
    /// Empty segments contribute nothing. Any parse or resolution failure
    /// fetches the empty string, as <see cref="Fetch(string)"/> does.
    /// </summary>
    public string FetchRange(string address)
    {
        RangeAddress? range = PhextEngine.ParseRangeAddress(address);
        if (range is null) return "";
        var segments = ResolveRange(range);
        if (segments is null) return "";
        var members = new List<string>();
        for (int universe = range.Start.Universe; universe <= range.End.Universe; universe++)
        {
            string member = "";
            foreach (var seg in segments)
            {
                if (seg.Universe != universe) continue;
                if (seg.Text.Length == 0) continue;
                member = PhextEngine.Insert(member, seg.Coordinate, seg.Text);
            }
            members.Add(member);
        }
        return new Chain(members, false).Serialize();
    }
}
