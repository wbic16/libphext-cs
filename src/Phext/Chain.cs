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
}
