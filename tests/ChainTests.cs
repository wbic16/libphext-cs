// ===========================================================================================================
// libphext-cs: The Fourth Root - Chain Tests
// Ported 1:1 from libphext-rs src/test_chain.rs (0.6.0)
//
// Copyright: (c) 2024-2025 Will Bickford (Phext, Inc.)
// License: MIT
// ===========================================================================================================

using Xunit;
using Phext;

namespace Phext.Tests;

public class ChainTests
{
    private const char CB = PhextConstants.CHAIN_BREAK;
    private const char CC = PhextConstants.CHAIN_CLOSE;

    /// <summary>parse + serialize must reproduce every stream byte-for-byte</summary>
    private static void Roundtrip(string stream)
    {
        var chains = PhextEngine.ParseChains(stream);
        Assert.Equal(stream, PhextEngine.SerializeChains(chains));
    }

    [Fact]
    public void ChainFreeInputIsOneUnclosedChain()
    {
        // a phext with no chain bytes: the whole stream is the single member
        // of one unclosed chain, and it serializes back unchanged
        string doc = "alpha\u0017beta\u0001gamma";
        var chains = PhextEngine.ParseChains(doc);
        Assert.Single(chains);
        Assert.Equal(new List<string> { doc }, chains[0].Members);
        Assert.False(chains[0].Closed);
        Assert.Equal(doc, PhextEngine.SerializeChains(chains));

        // single-phext behaviour on the same bytes is untouched: the chain
        // bytes are not dimension breaks, so nothing in the member moves
        Assert.Equal("beta", PhextEngine.Fetch(doc, Coordinate.FromString("1.1.1/1.1.1/1.1.2")));
        Assert.Equal("gamma", PhextEngine.Fetch(doc, Coordinate.FromString("2.1.1/1.1.1/1.1.1")));
        Roundtrip(doc);
    }

    [Fact]
    public void TwoPhextsChainedAndClosedRoundTrip()
    {
        string stream = $"edition one{CB}edition two{CC}";
        var chains = PhextEngine.ParseChains(stream);
        Assert.Single(chains);
        Assert.Equal(new List<string> { "edition one", "edition two" }, chains[0].Members);
        Assert.True(chains[0].Closed);
        Roundtrip(stream);
    }

    [Fact]
    public void UnclosedChainAtEofReportsTruncated()
    {
        var chains = PhextEngine.ParseChains("one\u0002two");
        Assert.Single(chains);
        Assert.Equal(new List<string> { "one", "two" }, chains[0].Members);
        Assert.False(chains[0].Closed); // a chain ending at EOF must report unclosed
        Roundtrip("one\u0002two");
    }

    [Fact]
    public void TwoChainsInOneStreamParseAsTwoUnits()
    {
        string stream = "a\u0002b\u0003c\u0002d\u0003";
        var chains = PhextEngine.ParseChains(stream);
        Assert.Equal(2, chains.Count);
        Assert.Equal(new List<string> { "a", "b" }, chains[0].Members);
        Assert.True(chains[0].Closed);
        Assert.Equal(new List<string> { "c", "d" }, chains[1].Members);
        Assert.True(chains[1].Closed);
        Roundtrip(stream);

        // a closed chain followed by an unclosed one stays two distinct chains
        var mixed = PhextEngine.ParseChains("a\u0003b");
        Assert.Equal(2, mixed.Count);
        Assert.Equal(new List<string> { "a" }, mixed[0].Members);
        Assert.True(mixed[0].Closed);
        Assert.Equal(new List<string> { "b" }, mixed[1].Members);
        Assert.False(mixed[1].Closed);
        Roundtrip("a\u0003b");
    }

    [Fact]
    public void ChainedMembersCarryFullPhexts()
    {
        // members are complete phexts: coordinates resolve inside each member,
        // and each member restarts at 1.1.1/1.1.1/1.1.1
        string first = "root\u0017second scroll";
        string second = PhextEngine.Insert(
            "",
            Coordinate.FromString("1.1.1/1.1.1/1.1.3"),
            "third edition");
        string stream = $"{first}\u0002{second}\u0003";
        var chains = PhextEngine.ParseChains(stream);
        Assert.Single(chains);
        Assert.Equal(2, chains[0].Members.Count);
        Assert.Equal(
            "second scroll",
            PhextEngine.Fetch(chains[0].Members[0], Coordinate.FromString("1.1.1/1.1.1/1.1.2")));
        Assert.Equal(
            "third edition",
            PhextEngine.Fetch(chains[0].Members[1], Coordinate.FromString("1.1.1/1.1.1/1.1.3")));
        Assert.Equal(
            "",
            PhextEngine.Fetch(chains[0].Members[1], Coordinate.FromString("1.1.1/1.1.1/1.1.1")));
        Roundtrip(stream);
    }

    [Fact]
    public void EditionsInSuccessionReadInOrder()
    {
        // the chain is the time axis: members arrive oldest-first and stay ordered
        string stream = "v1\u0002v2\u0002v3\u0003";
        var chains = PhextEngine.ParseChains(stream);
        Assert.Single(chains);
        Assert.Equal(new List<string> { "v1", "v2", "v3" }, chains[0].Members);
        Assert.True(chains[0].Closed);
        Roundtrip(stream);
    }

    [Fact]
    public void EmptyAndBreakOnlyStreamsRoundTrip()
    {
        // the empty stream is one unclosed chain holding one empty phext
        var chains = PhextEngine.ParseChains("");
        Assert.Single(chains);
        Assert.Equal(new List<string> { "" }, chains[0].Members);
        Assert.False(chains[0].Closed);
        Roundtrip("");

        // a lone close terminates a chain holding one empty phext
        chains = PhextEngine.ParseChains("\u0003");
        Assert.Single(chains);
        Assert.Equal(new List<string> { "" }, chains[0].Members);
        Assert.True(chains[0].Closed);
        Roundtrip("\u0003");

        // leading and trailing breaks preserve their empty members
        Assert.Equal(
            new List<string> { "a", "" },
            PhextEngine.ParseChains("a\u0002")[0].Members);
        Assert.Equal(
            new List<string> { "", "b" },
            PhextEngine.ParseChains("\u0002b")[0].Members);
        Assert.Equal(3, PhextEngine.ParseChains("\u0002\u0002")[0].Members.Count);
        Roundtrip("a\u0002");
        Roundtrip("\u0002b");
        Roundtrip("\u0002\u0002");
        Roundtrip("\u0002");

        // back-to-back closes are two chains, the second holding an empty phext
        chains = PhextEngine.ParseChains("a\u0003\u0003");
        Assert.Equal(2, chains.Count);
        Assert.True(chains[0].Closed && chains[1].Closed);
        Assert.Equal(new List<string> { "" }, chains[1].Members);
        Roundtrip("a\u0003\u0003");
    }

    [Fact]
    public void SerializeFromConstructedChains()
    {
        var chain = new Chain();
        Assert.False(chain.Closed);
        Assert.Equal("", chain.Serialize());
        chain.Members.Add("v1");
        chain.Members.Add("v2");
        Assert.Equal("v1\u0002v2", chain.Serialize());
        chain.Closed = true;
        Assert.Equal("v1\u0002v2\u0003", chain.Serialize());
        Assert.Equal("v1\u0002v2\u0003", PhextEngine.SerializeChains(new List<Chain> { chain }));
    }

    [Fact]
    public void CanonShapedDocumentsParseUnchanged()
    {
        // the regression fixture stands in for the canon phexts (CYOA,
        // incipit.phext, emise.phext): verified to contain no 0x02 or 0x03
        // bytes, so each reads as one unclosed chain holding one member,
        // byte-identical in and out
        string path = Path.Combine(AppContext.BaseDirectory, "regression-1-helios.phext");
        byte[] docBytes = File.ReadAllBytes(path);
        string doc = System.Text.Encoding.UTF8.GetString(docBytes);
        Assert.DoesNotContain((byte)PhextConstants.CHAIN_BREAK, docBytes);
        Assert.DoesNotContain((byte)PhextConstants.CHAIN_CLOSE, docBytes);
        var chains = PhextEngine.ParseChains(doc);
        Assert.Single(chains);
        Assert.Equal(new List<string> { doc }, chains[0].Members);
        Assert.False(chains[0].Closed);
        Assert.Equal(doc, PhextEngine.SerializeChains(chains));
    }

    [Fact]
    public void ChainLayerDoesNotTouchMemberStructure()
    {
        // splitting happens at the stream level only: a member's own scrolls
        // and breaks are exactly what the single-phext functions see
        var chains = PhextEngine.ParseChains("x\u0017y\u0002z\u0017w\u0003");
        Assert.Equal(
            new List<string> { "x\u0017y", "z\u0017w" },
            chains[0].Members);
        Assert.Equal(2, PhextEngine.Phokenize(chains[0].Members[1]).Count);
        // and a chain byte inside what a 0.5 reader saw as one document is
        // now the stream layer's: phokenize alone no longer spans members
        Roundtrip("x\u0017y\u0002z\u0017w\u0003");
    }

    [Fact]
    public void MultiverseAddressing()
    {
        var chain = new Chain(new List<string> { "U1 home", "U2 home" }, true);
        Assert.Equal("U1 home", chain.Fetch("1.1.1/1.1.1/1.1.1"));
        Assert.Equal("U1 home", chain.Fetch("$1:1.1.1/1.1.1/1.1.1"));
        Assert.Equal("U2 home", chain.Fetch("$2:1.1.1/1.1.1/1.1.1"));
        Assert.Equal("", chain.Fetch("$3:1.1.1/1.1.1/1.1.1"));
        Assert.Null(PhextEngine.ParseUniverseAddress("$0:1.1.1/1.1.1/1.1.1"));
        var parsed = PhextEngine.ParseUniverseAddress("$12:2.1.1/1.1.1/1.1.1");
        Assert.NotNull(parsed);
        Assert.Equal(12, parsed.Value.Universe);
    }

    [Fact]
    public void AdjacentClosesAreTwoEmptyChains()
    {
        var chains = PhextEngine.ParseChains("\u0003\u0003");
        Assert.Equal(2, chains.Count);
        Assert.All(chains, c => Assert.True(c.Closed));
        Assert.Equal("\u0003\u0003", PhextEngine.SerializeChains(chains));
    }
}
