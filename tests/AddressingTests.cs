// ===========================================================================================================
// libphext-cs: The Fourth Root - Addressing Tests (0.6.1)
// Unit tests for the point/range address layer; the shared conformance
// fixtures live in RangeFixtureTests.cs.
//
// Copyright: (c) 2024-2025 Will Bickford (Phext, Inc.)
// License: MIT
// ===========================================================================================================

using Xunit;
using Phext;

namespace Phext.Tests;

public class AddressingTests
{
    private const char CB = PhextConstants.CHAIN_BREAK;
    private const char CC = PhextConstants.CHAIN_CLOSE;
    private const char SB = PhextConstants.SCROLL_BREAK;

    private static Chain SingleChain(string doc) => PhextEngine.ParseChains(doc)[0];

    [Fact]
    public void PointCanonicalFormOmitsUniverseOne()
    {
        var point = PhextEngine.ParsePointAddress("$1:1.1.1/1.1.1/1.1.1@24,5");
        Assert.NotNull(point);
        Assert.Equal("1.1.1/1.1.1/1.1.1@24,5", point!.ToString());

        var third = PhextEngine.ParsePointAddress("$3:1.1.1/1.1.1/1.1.1@24,5");
        Assert.Equal("$3:1.1.1/1.1.1/1.1.1@24,5", third!.ToString());
    }

    [Fact]
    public void RangeCanonicalizesReversedEndpoints()
    {
        var range = PhextEngine.ParseRangeAddress("1.1.1/1.1.1/1.1.1@24,9-1.1.1/1.1.1/1.1.1@24,5");
        Assert.NotNull(range);
        Assert.Equal("1.1.1/1.1.1/1.1.1@24,5-1.1.1/1.1.1/1.1.1@24,9", range!.ToString());
    }

    [Fact]
    public void RangeRejectsOffsetEndpoints()
    {
        Assert.Null(PhextEngine.ParseRangeAddress("1.1.1/1.1.1/1.1.1@1,1+3-1.1.1/1.1.1/1.1.1@1,5"));
        Assert.Null(PhextEngine.ParseRangeAddress("1.1.1/1.1.1/1.1.1@1,1-1.1.1/1.1.1/1.1.1@1,5+2"));
    }

    [Fact]
    public void CaretPointFetchesEmptyAndOffsetClipsAtScrollEnd()
    {
        Chain chain = SingleChain("hello");
        Assert.Equal("", chain.FetchPoint("1.1.1/1.1.1/1.1.1@1,2"));
        Assert.Equal("ell", chain.FetchPoint("1.1.1/1.1.1/1.1.1@1,2+3"));
        Assert.Equal("ello", chain.FetchPoint("1.1.1/1.1.1/1.1.1@1,2+100"));
    }

    [Fact]
    public void ColumnsAndOffsetsCountScalarsNotUtf16Units()
    {
        // "a" + U+1F600 (one scalar, two UTF-16 units) + "b": column 3 is
        // the boundary after the emoji, scalar offset 2.
        Chain chain = SingleChain("a\U0001F600b");
        var resolved = chain.ResolvePoint(PhextEngine.ParsePointAddress("1.1.1/1.1.1/1.1.1@1,3")!);
        Assert.NotNull(resolved);
        Assert.Equal(2, resolved!.Value.Offset);
        Assert.Equal("\U0001F600b", chain.FetchPoint("1.1.1/1.1.1/1.1.1@1,2+2"));
    }

    [Fact]
    public void ResolutionFailsNeverClamps()
    {
        Chain chain = SingleChain("one\ntwo");
        Assert.Null(chain.ResolvePoint(PhextEngine.ParsePointAddress("1.1.1/1.1.1/1.1.1@3,1")!));
        Assert.Null(chain.ResolvePoint(PhextEngine.ParsePointAddress("1.1.1/1.1.1/1.1.1@1,5")!));
        Assert.Null(chain.ResolvePoint(PhextEngine.ParsePointAddress("$2:1.1.1/1.1.1/1.1.1@1,1")!));
        Assert.Equal("", chain.FetchRange("1.1.1/1.1.1/1.1.1@1,1-1.1.1/1.1.1/1.1.1@9,1"));
    }

    [Fact]
    public void RangeFetchBuildsFragmentWithoutChainClose()
    {
        string doc = $"alpha{SB}beta";
        Chain chain = SingleChain(doc);
        string fragment = chain.FetchRange("1.1.1/1.1.1/1.1.1@1,2-1.1.1/1.1.1/1.1.2@1,3");
        Assert.Equal($"lpha{SB}be", fragment);
        Assert.False(fragment.Contains(CC));
    }

    [Fact]
    public void MultiverseRangeFetchJoinsWithChainBreak()
    {
        string stream = $"one{CB}two{CB}three{CC}";
        Chain chain = PhextEngine.ParseChains(stream)[0];
        string fragment = chain.FetchRange("1.1.1/1.1.1/1.1.1@1,2-$3:1.1.1/1.1.1/1.1.1@1,3");
        Assert.Equal($"ne{CB}two{CB}th", fragment);
    }
}
