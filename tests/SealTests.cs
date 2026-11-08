// ===========================================================================================================
// libphext-cs: The Fourth Root - XZ Seal Tests
// Ported 1:1 from libphext-rs src/test_seal.rs (0.5.0)
//
// Copyright: (c) 2024-2025 Will Bickford (Phext, Inc.)
// License: MIT
// ===========================================================================================================

using Xunit;
using Phext;
using Phext.Seal;

namespace Phext.Tests;

public class SealTests
{
    [Fact]
    public void Test_SealRoundTripRealPhext()
    {
        // a real, multi-scroll phext (stands in for the rs crate's unit-test corpus;
        // real full-size files are covered by the cross-implementation interop harness)
        string corpus = "alpha\u0017beta\u0018gamma ✓ delta\u0019" +
                        "phext-binary:1:base64\nAAEC/f7/fw==\u001A" +
                        "a far scroll with ordinary text\nand a second line";
        byte[] sealedBytes = PhextSeal.Seal(corpus);
        Assert.True(sealedBytes.Length > 0);
        // an xz stream starts with the xz magic bytes
        Assert.Equal(new byte[] { 0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00 }, sealedBytes.Take(6).ToArray());
        string restored = PhextSeal.UnsealString(sealedBytes);
        Assert.Equal(corpus, restored);
    }

    [Fact]
    public void Test_SealRoundTripEmptyAndSmall()
    {
        byte[] sealedEmpty = PhextSeal.Seal("");
        Assert.Equal("", PhextSeal.UnsealString(sealedEmpty));

        byte[] sealedBytes = PhextSeal.Seal("hello phext");
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes("hello phext"), PhextSeal.Unseal(sealedBytes));
    }

    [Fact]
    public void Test_SealCarryingBinaryScrolls()
    {
        // a phext holding base64 binary scrolls (the canonical literal form)
        // seals and unseals with every scroll intact
        string doc = "cover text\u0017phext-binary:1:base64\nAAEC/f7/fw==\u0017after";
        byte[] sealedBytes = PhextSeal.Seal(doc);
        string restored = PhextSeal.UnsealString(sealedBytes);
        Assert.Equal(doc, restored);
    }

    [Fact]
    public void Test_SealIsDeterministic()
    {
        string doc = "the same phext, sealed twice\u0017the same phext, sealed twice";
        byte[] first = PhextSeal.Seal(doc);
        byte[] second = PhextSeal.Seal(doc);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Test_UnsealRejectsNonXzData()
    {
        Assert.Throws<InvalidDataException>(() => PhextSeal.Unseal(System.Text.Encoding.UTF8.GetBytes("this is not an xz stream")));
        Assert.Throws<InvalidDataException>(() => PhextSeal.Unseal(Array.Empty<byte>()));
        Assert.Throws<InvalidDataException>(() => PhextSeal.UnsealString(System.Text.Encoding.UTF8.GetBytes("phext, but unsealed")));
    }
}
