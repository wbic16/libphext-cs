// ===========================================================================================================
// libphext-cs: The Fourth Root - Binary Scroll Tests
// Ported 1:1 from libphext-rs src/test_binary.rs (0.5.0)
//
// Copyright: (c) 2024-2025 Will Bickford (Phext, Inc.)
// License: MIT
// ===========================================================================================================

using Xunit;
using Phext;

namespace Phext.Tests;

public class BinaryTests
{
    private static Coordinate Coord(string address) => Coordinate.FromString(address);

    [Fact]
    public void Test_EncodeDecodeRoundTrip()
    {
        // empty payload: header line alone
        byte[] empty = Array.Empty<byte>();
        string scroll = BinaryScroll.EncodeScroll(empty);
        Assert.Equal("phext-binary:1:base64\n", scroll);
        Assert.Equal(empty, BinaryScroll.DecodeScroll(scroll));

        // every byte value
        byte[] allBytes = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        scroll = BinaryScroll.EncodeScroll(allBytes);
        Assert.True(BinaryScroll.IsBinaryScroll(scroll));
        Assert.Equal(allBytes, BinaryScroll.DecodeScroll(scroll));

        // a plainly non-UTF-8 sequence
        byte[] foreign = { 0xFF, 0xFE, 0x80, 0x00, 0x61, 0xC3, 0x28 };
        scroll = BinaryScroll.EncodeScroll(foreign);
        Assert.Equal(foreign, BinaryScroll.DecodeScroll(scroll));

        // >= 1 MiB payload, deterministic pattern covering all byte values
        byte[] big = Enumerable.Range(0, 1024 * 1024 + 13).Select(i => (byte)(i % 256)).ToArray();
        scroll = BinaryScroll.EncodeScroll(big);
        Assert.Equal(big, BinaryScroll.DecodeScroll(scroll));
    }

    [Fact]
    public void Test_DecodeScrollRejectsNonBinary()
    {
        Assert.Null(BinaryScroll.DecodeScroll("just some text"));
        Assert.Null(BinaryScroll.DecodeScroll(""));
        // header must be a complete line: no line break, no binary scroll
        Assert.Null(BinaryScroll.DecodeScroll("phext-binary:1:base64"));
        Assert.False(BinaryScroll.IsBinaryScroll("phext-binary:1:base64"));
        // malformed base64 body: recognisably binary, undecodable
        string malformed = "phext-binary:1:base64\n!!! not base64 !!!";
        Assert.True(BinaryScroll.IsBinaryScroll(malformed));
        Assert.Null(BinaryScroll.DecodeScroll(malformed));
    }

    [Fact]
    public void Test_InsertAndFetchBinary()
    {
        var target = Coord("1.1.1/1.1.1/1.1.2");
        byte[] payload = { 0x00, 0x01, 0x02, 0xFE, 0xFF, 0x7F, 0x80 };

        string doc = PhextEngine.Insert("", Coord("1.1.1/1.1.1/1.1.1"), "plain text scroll");
        doc = BinaryScroll.InsertBinary(doc, target, payload);

        // the text scroll next door is untouched
        Assert.Equal("plain text scroll", PhextEngine.Fetch(doc, Coord("1.1.1/1.1.1/1.1.1")));
        // the binary scroll fetches back byte-identical
        Assert.Equal(payload, BinaryScroll.FetchBinary(doc, target));
        // and its stored form is the canonical binary scroll
        string stored = PhextEngine.Fetch(doc, target);
        Assert.True(BinaryScroll.IsBinaryScroll(stored));
        Assert.Equal(payload, BinaryScroll.DecodeScroll(stored));
    }

    [Fact]
    public void Test_FetchBinaryFallbacks()
    {
        string doc = PhextEngine.Insert("", Coord("1.1.1/1.1.1/1.1.1"), "hello");
        // a plain text scroll yields its own UTF-8 bytes
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes("hello"), BinaryScroll.FetchBinary(doc, Coord("1.1.1/1.1.1/1.1.1")));
        // an unoccupied coordinate yields no bytes (mirrors fetch returning "")
        Assert.Equal(Array.Empty<byte>(), BinaryScroll.FetchBinary(doc, Coord("9.9.9/9.9.9/9.9.9")));
        // a malformed binary scroll yields its own bytes verbatim, not an exception
        doc = PhextEngine.Insert("", Coord("1.1.1/1.1.1/1.1.1"), "phext-binary:1:base64\n!!!");
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes("phext-binary:1:base64\n!!!"), BinaryScroll.FetchBinary(doc, Coord("1.1.1/1.1.1/1.1.1")));
    }

    [Fact]
    public void Test_UpdateRuleNonUtf8IsBase64Verbatim()
    {
        var target = Coord("1.1.1/1.1.1/1.1.1");
        // payload deliberately includes dimension-break bytes (0x17, 0x1C):
        // armoured as base64, they round-trip as content, not structure
        byte[] payload = { 0xFF, 0x17, 0x1C, 0x80, 0x00 };
        string doc = BinaryScroll.ReplaceBytes("", target, payload);

        string stored = PhextEngine.Fetch(doc, target);
        Assert.True(BinaryScroll.IsBinaryScroll(stored), "non-UTF-8 update must be stored as a binary scroll");
        Assert.Equal(payload, BinaryScroll.FetchBinary(doc, target));

        // same rule through the insert entry point
        doc = BinaryScroll.InsertBytes("", target, payload);
        Assert.Equal(payload, BinaryScroll.FetchBinary(doc, target));
    }

    [Fact]
    public void Test_UpdateRuleUtf8StaysText()
    {
        var target = Coord("1.1.1/1.1.1/1.1.1");
        string doc = BinaryScroll.ReplaceBytes("", target, System.Text.Encoding.UTF8.GetBytes("ordinary text ✓"));
        Assert.Equal("ordinary text ✓", PhextEngine.Fetch(doc, target));
        Assert.False(BinaryScroll.IsBinaryScroll(PhextEngine.Fetch(doc, target)));
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes("ordinary text ✓"), BinaryScroll.FetchBinary(doc, target));
    }

    [Fact]
    public void Test_IngestForeignByteStream()
    {
        // a phext byte stream whose second scroll is not valid UTF-8
        var stream = new List<byte>();
        stream.AddRange(System.Text.Encoding.UTF8.GetBytes("hello"));
        stream.Add((byte)PhextConstants.SCROLL_BREAK);
        stream.AddRange(new byte[] { 0xFF, 0xFE, 0x41, 0x80 });

        string doc = BinaryScroll.Ingest(stream.ToArray());

        // text scroll passes through verbatim
        Assert.Equal("hello", PhextEngine.Fetch(doc, Coord("1.1.1/1.1.1/1.1.1")));
        // foreign scroll: stored as a binary scroll, reads back as the original bytes
        var foreignAt = Coord("1.1.1/1.1.1/1.1.2");
        Assert.True(BinaryScroll.IsBinaryScroll(PhextEngine.Fetch(doc, foreignAt)));
        Assert.Equal(new byte[] { 0xFF, 0xFE, 0x41, 0x80 }, BinaryScroll.FetchBinary(doc, foreignAt));
    }

    [Fact]
    public void Test_IngestCanonicalPhextIsIdentity()
    {
        string doc = PhextEngine.Insert("", Coord("1.1.1/1.1.1/1.1.1"), "first");
        doc = PhextEngine.Insert(doc, Coord("1.1.1/1.1.1/1.1.2"), "second ✓ scroll");
        doc = PhextEngine.Insert(doc, Coord("2.1.1/1.3.1/1.1.1"), "far away");
        Assert.Equal(doc, BinaryScroll.Ingest(System.Text.Encoding.UTF8.GetBytes(doc)));
    }

    [Fact]
    public void Test_IngestEmptyAndBreakOnlyStreams()
    {
        // no exception, no content
        string doc = BinaryScroll.Ingest(Array.Empty<byte>());
        Assert.Equal("", PhextEngine.Fetch(doc, Coord("1.1.1/1.1.1/1.1.1")));
        doc = BinaryScroll.Ingest(new byte[] { (byte)PhextConstants.SCROLL_BREAK, (byte)PhextConstants.SECTION_BREAK });
        Assert.Equal("", PhextEngine.Fetch(doc, Coord("1.1.1/1.1.1/1.1.1")));
    }

    [Fact]
    public void Test_PhokenizeBytesMatchesPhokenize()
    {
        string doc = "alpha\u0017beta\u0018gamma";
        var textParts = PhextEngine.Phokenize(doc);
        var byteParts = BinaryScroll.PhokenizeBytes(System.Text.Encoding.UTF8.GetBytes(doc));
        Assert.Equal(textParts.Count, byteParts.Count);
        for (int i = 0; i < textParts.Count; i++)
        {
            Assert.Equal(textParts[i].Coord, byteParts[i].Coord);
            Assert.Equal(System.Text.Encoding.UTF8.GetBytes(textParts[i].Scroll), byteParts[i].Bytes);
        }
    }
}
