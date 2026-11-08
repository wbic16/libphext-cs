// ===========================================================================================================
// libphext-cs: The Fourth Root
// BinaryScroll.cs - Canonical binary scrolls: arbitrary bytes stored inside phext
//
// Ported from: libphext-rs src/binary.rs (0.5.0)
// Copyright: (c) 2024-2025 Will Bickford (Phext, Inc.)
// License: MIT
//
// Phext scrolls are text (valid UTF-8). Arbitrary binary data rides as text via base64.
// This file defines the one canonical binary scroll format, so that every phext
// implementation (Rust, Python, Node, C#, C, ...) stores and recognises binary payloads
// byte-for-byte identically:
//
//     phext-binary:1:base64
//     <base64 body>
//
// Format specification (version 1):
// * Line 1 is exactly `phext-binary:1:base64`, followed by a single line break (0x0A).
// * The body is RFC 4648 base64: standard alphabet (A-Z, a-z, 0-9, +, /), padding (=)
//   required, no line wrapping, no whitespace.
// * An empty payload is the header line alone (header + line break + "").
// * A scroll whose content begins with the header line is a binary scroll by definition.
//   The format claims that namespace: text that matches it exactly is read as a binary
//   scroll. A prefix-matching scroll whose body is not strict base64 is a *malformed*
//   binary scroll.
//
// The ingestion rule (canonical behaviour, all ports):
// > When non-UTF-8 data is observed in an incoming scroll update, we encode it with
// > base64 verbatim.
//
// Byte-oriented entry points (InsertBytes, ReplaceBytes, Ingest) never reject and never
// throw on non-UTF-8 scroll data. Valid UTF-8 is stored as ordinary text; anything else
// is stored as a binary scroll holding the base64 of the exact observed bytes - verbatim,
// with no normalisation - so the matching fetch returns the original bytes exactly.
// Within a phext byte stream, the nine dimension-break bytes are always structural;
// binary payloads that may contain them enter through the update functions, armoured
// as base64.
// ===========================================================================================================

using System.Text;

namespace Phext;

public static class BinaryScroll
{
    /// <summary>The first (and only header) line of a binary scroll, without its line break.</summary>
    public const string Header = "phext-binary:1:base64";

    /// <summary>Header line plus its terminating line break: the prefix of every binary scroll.</summary>
    private const string Prefix = Header + "\n";

    // Strict UTF-8: throws on invalid byte sequences instead of substituting U+FFFD.
    // The ingestion rule turns on exactly this distinction.
    private static readonly Encoding StrictUtf8 =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// True when <paramref name="scroll"/> carries the binary scroll header. Decoding
    /// success is a separate question - see <see cref="DecodeScroll"/>.
    /// </summary>
    public static bool IsBinaryScroll(string scroll)
    {
        return scroll.StartsWith(Prefix, StringComparison.Ordinal);
    }

    /// <summary>Encodes raw bytes as a binary scroll (header line + base64 body).</summary>
    public static string EncodeScroll(byte[] payload)
    {
        return Prefix + Convert.ToBase64String(payload);
    }

    /// <summary>
    /// Decodes a binary scroll back to its raw bytes. Returns null when the scroll is not
    /// a binary scroll, or when its body is not strict canonical base64. Strictness mirrors
    /// the Rust base64 STANDARD engine: whitespace, missing/extra padding, non-alphabet
    /// characters, and non-zero trailing bits are all rejected (Convert.FromBase64String
    /// alone tolerates whitespace, so the decode is verified by re-encoding).
    /// </summary>
    public static byte[]? DecodeScroll(string scroll)
    {
        if (!scroll.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        string body = scroll.Substring(Prefix.Length);

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(body);
        }
        catch (FormatException)
        {
            return null;
        }

        if (!string.Equals(Convert.ToBase64String(bytes), body, StringComparison.Ordinal))
        {
            return null;
        }

        return bytes;
    }

    /// <summary>
    /// Stores <paramref name="payload"/> at <paramref name="location"/> as a binary scroll,
    /// unconditionally: the caller has declared this scroll binary, even if the bytes
    /// happen to be valid UTF-8.
    /// </summary>
    public static string InsertBinary(string phext, Coordinate location, byte[] payload)
    {
        return PhextEngine.Insert(phext, location, EncodeScroll(payload));
    }

    /// <summary>
    /// Replaces the scroll at <paramref name="location"/> with <paramref name="payload"/>,
    /// stored as a binary scroll (see <see cref="InsertBinary"/>).
    /// </summary>
    public static string ReplaceBinary(string phext, Coordinate location, byte[] payload)
    {
        return PhextEngine.Replace(phext, location, EncodeScroll(payload));
    }

    /// <summary>
    /// Retrieves the bytes held at <paramref name="target"/>. A well-formed binary scroll
    /// decodes to its payload. Any other scroll - plain text, a malformed binary scroll,
    /// or no scroll at all - yields the scroll's own UTF-8 bytes (empty when the coordinate
    /// is unoccupied), mirroring how <see cref="PhextEngine.Fetch"/> returns "" there.
    /// This function is total: it never throws and never loses scroll content.
    /// </summary>
    public static byte[] FetchBinary(string phext, Coordinate target)
    {
        string scroll = PhextEngine.Fetch(phext, target);
        return DecodeScroll(scroll) ?? Encoding.UTF8.GetBytes(scroll);
    }

    /// <summary>
    /// The stored form of an incoming scroll update, per the ingestion rule:
    /// valid UTF-8 stays text; anything else becomes a binary scroll holding the
    /// base64 of the exact observed bytes, verbatim.
    /// </summary>
    private static string StoredForm(byte[] payload)
    {
        try
        {
            return StrictUtf8.GetString(payload);
        }
        catch (DecoderFallbackException)
        {
            return EncodeScroll(payload);
        }
    }

    /// <summary>
    /// Byte-oriented insert: stores <paramref name="payload"/> at <paramref name="location"/>
    /// under the ingestion rule (UTF-8 as text, non-UTF-8 as a base64-verbatim binary scroll).
    /// </summary>
    public static string InsertBytes(string phext, Coordinate location, byte[] payload)
    {
        return PhextEngine.Insert(phext, location, StoredForm(payload));
    }

    /// <summary>
    /// Byte-oriented replace: stores <paramref name="payload"/> at <paramref name="location"/>
    /// under the ingestion rule (UTF-8 as text, non-UTF-8 as a base64-verbatim binary scroll).
    /// </summary>
    public static string ReplaceBytes(string phext, Coordinate location, byte[] payload)
    {
        return PhextEngine.Replace(phext, location, StoredForm(payload));
    }

    private static bool IsStructuralBreak(byte b)
    {
        return b == (byte)PhextConstants.SCROLL_BREAK ||
               b == (byte)PhextConstants.SECTION_BREAK ||
               b == (byte)PhextConstants.CHAPTER_BREAK ||
               b == (byte)PhextConstants.BOOK_BREAK ||
               b == (byte)PhextConstants.VOLUME_BREAK ||
               b == (byte)PhextConstants.COLLECTION_BREAK ||
               b == (byte)PhextConstants.SERIES_BREAK ||
               b == (byte)PhextConstants.SHELF_BREAK ||
               b == (byte)PhextConstants.LIBRARY_BREAK;
    }

    private static void ApplyBreak(Coordinate location, byte b)
    {
        if (b == (byte)PhextConstants.SCROLL_BREAK) location.ScrollBreak();
        if (b == (byte)PhextConstants.SECTION_BREAK) location.SectionBreak();
        if (b == (byte)PhextConstants.CHAPTER_BREAK) location.ChapterBreak();
        if (b == (byte)PhextConstants.BOOK_BREAK) location.BookBreak();
        if (b == (byte)PhextConstants.VOLUME_BREAK) location.VolumeBreak();
        if (b == (byte)PhextConstants.COLLECTION_BREAK) location.CollectionBreak();
        if (b == (byte)PhextConstants.SERIES_BREAK) location.SeriesBreak();
        if (b == (byte)PhextConstants.SHELF_BREAK) location.ShelfBreak();
        if (b == (byte)PhextConstants.LIBRARY_BREAK) location.LibraryBreak();
    }

    /// <summary>
    /// The byte-level twin of <see cref="PhextEngine.Phokenize"/>: walks a raw byte stream
    /// and returns each scroll's coordinate and raw bytes, with identical segmentation.
    /// Dimension-break bytes are ASCII, so byte offsets are exact; scroll bodies are
    /// returned uninterpreted, valid UTF-8 or not.
    /// </summary>
    public static List<PositionedByteScroll> PhokenizeBytes(byte[] input)
    {
        var result = new List<PositionedByteScroll>();
        int max = input.Length;
        var coord = new Coordinate();
        int pi = 0;

        while (true)
        {
            var begin = new Coordinate(coord);
            int textStart = -1;
            int textEnd = max;

            while (pi < max)
            {
                byte b = input[pi];
                if (IsStructuralBreak(b))
                {
                    ApplyBreak(coord, b);
                    if (textStart >= 0)
                    {
                        textEnd = pi;
                        pi++;
                        break;
                    }
                }
                else if (textStart < 0)
                {
                    textStart = pi;
                    begin.CopyFrom(coord);
                }
                pi++;
            }

            byte[] scroll = textStart >= 0
                ? input.AsSpan(textStart, textEnd - textStart).ToArray()
                : Array.Empty<byte>();
            result.Add(new PositionedByteScroll(begin, scroll));
            if (pi >= max) break;
        }

        return result;
    }

    /// <summary>
    /// Ingests an externally supplied phext byte stream. Every scroll is observed as raw
    /// bytes: valid UTF-8 scrolls pass through verbatim, and any scroll whose bytes are
    /// not valid UTF-8 is stored as a binary scroll holding the base64 of the exact
    /// observed bytes (the ingestion rule), so <see cref="FetchBinary"/> at its coordinate
    /// returns the original bytes exactly. Never rejects, never throws. The result is the
    /// canonical serialization (the same normal form as Implode of Explode).
    /// </summary>
    public static string Ingest(byte[] bytes)
    {
        var parts = PhokenizeBytes(bytes);
        var map = new Dictionary<Coordinate, string>();
        foreach (var part in parts)
        {
            string text;
            try
            {
                text = StrictUtf8.GetString(part.Bytes);
            }
            catch (DecoderFallbackException)
            {
                text = EncodeScroll(part.Bytes);
            }
            map[new Coordinate(part.Coord)] = text;
        }
        return PhextEngine.Implode(map);
    }
}
