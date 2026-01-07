// ===========================================================================================================
// libphext-cs: The Fourth Root
// Phext.cs - Core operations for 11-dimensional plain hypertext
//
// Ported from: https://github.com/wbic16/libphext-rs
// Copyright: (c) 2024-2025 Will Bickford (Phext, Inc.)
// License: MIT
//
// Overview
// --------
// Phext is composable, relational text. It is composed of layers upon layers of plain text (scrolls).
// All text in a phext document is stored in subspace: a traditional buffer of utf8 text.
// Subspace enables you to orient yourself within petabyte volumes of text with dead reckoning.
//
// Coordinate Systems as Points
// ----------------------------
// A phext coordinate space is like a 3D coordinate system compressed fractally into a point.
// We make use of increasingly-larger dimension breaks to navigate this address space.
//
// Encoding (ASCII control codes repurposed as dimension breaks)
// -------------------------------------------------------------
//   Dimension  Designation  Description
//   ---------  -----------  ----------- 
//   1          CL           Column
//   2          LN           Line (0x0A)
//   3          SC           Scroll (0x17)
//   4          SN           Section (0x18)
//   5          CH           Chapter (0x19)
//   6          BK           Book (0x1A)
//   7          VM           Volume (0x1C)
//   8          CN           Collection (0x1D)
//   9          SR           Series (0x1E)
//   10         SF           Shelf (0x1F)
//   11         LB           Library (0x01)
// ===========================================================================================================

using System.Security.Cryptography;
using System.Text;

namespace Phext;

public static class PhextConstants
{
    // Phext dimension break characters (ASCII control codes)
    public const char LIBRARY_BREAK = '\x01';    // 11th dimension - replaces start of header
    public const char MORE_COWBELL = '\x07';     // i've got a fever...
    public const char LINE_BREAK = '\x0A';       // same as plain text
    public const char SCROLL_BREAK = '\x17';     // 3D Break - replaces End Transmission Block
    public const char SECTION_BREAK = '\x18';    // 4D Break - replaces Cancel Block
    public const char CHAPTER_BREAK = '\x19';    // 5D Break - replaces End of Tape
    public const char BOOK_BREAK = '\x1A';       // 6D Break - replaces Substitute
    public const char VOLUME_BREAK = '\x1C';     // 7D Break - replaces file separator
    public const char COLLECTION_BREAK = '\x1D'; // 8D Break - replaces group separator
    public const char SERIES_BREAK = '\x1E';     // 9D Break - replaces record separator
    public const char SHELF_BREAK = '\x1F';      // 10D Break - replaces unit separator

    // Address format delimiters
    public const char ADDRESS_MICRO_BREAK = '.';
    public const char ADDRESS_MACRO_BREAK = '/';
    public const char ADDRESS_MACRO_ALT = ';';

    /// <summary>
    /// Returns true if the byte is a phext dimension break (including line break)
    /// </summary>
    public static bool IsPhextBreak(byte b)
    {
        return b == LINE_BREAK ||
               b == SCROLL_BREAK ||
               b == SECTION_BREAK ||
               b == CHAPTER_BREAK ||
               b == BOOK_BREAK ||
               b == VOLUME_BREAK ||
               b == COLLECTION_BREAK ||
               b == SERIES_BREAK ||
               b == SHELF_BREAK ||
               b == LIBRARY_BREAK;
    }

    public static bool IsPhextBreak(char c) => IsPhextBreak((byte)c);
}

public class PhextEngine
{
    /// <summary>
    /// Checks if the buffer contains the MORE_COWBELL character
    /// </summary>
    public static bool CheckForCowbell(string phext)
    {
        return phext.Contains(PhextConstants.MORE_COWBELL);
    }

    /// <summary>
    /// Finds the start and end offsets for the given coordinate within subspace
    /// </summary>
    public static SubspaceBeacon GetSubspaceCoordinates(byte[] subspace, Coordinate target)
    {
        var walker = new Coordinate();
        var best = new Coordinate();
        int subspaceIndex = 0;
        int start = 0;
        int end = 0;
        int stage = 0;
        int max = subspace.Length;

        while (subspaceIndex < max)
        {
            byte next = subspace[subspaceIndex];
            char compare = (char)next;

            if (stage == 0)
            {
                if (walker == target)
                {
                    stage = 1;
                    start = subspaceIndex;
                    best.CopyFrom(walker);
                }
                if (walker < target)
                {
                    best.CopyFrom(walker);
                }
            }

            if (stage < 2 && walker > target)
            {
                if (stage == 0) start = subspaceIndex - 1;
                end = subspaceIndex - 1;
                stage = 2;
            }

            if (PhextConstants.IsPhextBreak(next))
            {
                if (compare == PhextConstants.SCROLL_BREAK) walker.ScrollBreak();
                if (compare == PhextConstants.SECTION_BREAK) walker.SectionBreak();
                if (compare == PhextConstants.CHAPTER_BREAK) walker.ChapterBreak();
                if (compare == PhextConstants.BOOK_BREAK) walker.BookBreak();
                if (compare == PhextConstants.VOLUME_BREAK) walker.VolumeBreak();
                if (compare == PhextConstants.COLLECTION_BREAK) walker.CollectionBreak();
                if (compare == PhextConstants.SERIES_BREAK) walker.SeriesBreak();
                if (compare == PhextConstants.SHELF_BREAK) walker.ShelfBreak();
                if (compare == PhextConstants.LIBRARY_BREAK) walker.LibraryBreak();
            }

            if (stage < 2 && walker > target)
            {
                end = subspaceIndex;
                stage = 2;
            }

            subspaceIndex++;
        }

        if (stage == 1 && walker == target)
        {
            end = max;
            stage = 2;
        }

        if (stage == 0)
        {
            start = max;
            end = max;
        }

        return new SubspaceBeacon(start, end, best);
    }

    /// <summary>
    /// Retrieves the scroll text at the given coordinate
    /// </summary>
    public static string Fetch(string phext, Coordinate target)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(phext);
        var parts = GetSubspaceCoordinates(bytes, target);

        int start = parts.Start;
        int end = parts.End;

        if (end > start)
        {
            int length = end - start;
            return Encoding.UTF8.GetString(bytes, start, length);
        }

        return "";
    }

    /// <summary>
    /// Replaces the scroll at the given coordinate with new content
    /// </summary>
    public static string Replace(string phext, Coordinate location, string scroll)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(phext);
        var parts = GetSubspaceCoordinates(bytes, location);
        int start = parts.Start;
        int end = parts.End;
        var subspaceCoordinate = new Coordinate(parts.Best);

        var fixup = new List<byte>();

        while (subspaceCoordinate.Library < location.Library)
        {
            fixup.Add((byte)PhextConstants.LIBRARY_BREAK);
            subspaceCoordinate.LibraryBreak();
        }
        while (subspaceCoordinate.Shelf < location.Shelf)
        {
            fixup.Add((byte)PhextConstants.SHELF_BREAK);
            subspaceCoordinate.ShelfBreak();
        }
        while (subspaceCoordinate.Series < location.Series)
        {
            fixup.Add((byte)PhextConstants.SERIES_BREAK);
            subspaceCoordinate.SeriesBreak();
        }
        while (subspaceCoordinate.Collection < location.Collection)
        {
            fixup.Add((byte)PhextConstants.COLLECTION_BREAK);
            subspaceCoordinate.CollectionBreak();
        }
        while (subspaceCoordinate.Volume < location.Volume)
        {
            fixup.Add((byte)PhextConstants.VOLUME_BREAK);
            subspaceCoordinate.VolumeBreak();
        }
        while (subspaceCoordinate.Book < location.Book)
        {
            fixup.Add((byte)PhextConstants.BOOK_BREAK);
            subspaceCoordinate.BookBreak();
        }
        while (subspaceCoordinate.Chapter < location.Chapter)
        {
            fixup.Add((byte)PhextConstants.CHAPTER_BREAK);
            subspaceCoordinate.ChapterBreak();
        }
        while (subspaceCoordinate.Section < location.Section)
        {
            fixup.Add((byte)PhextConstants.SECTION_BREAK);
            subspaceCoordinate.SectionBreak();
        }
        while (subspaceCoordinate.Scroll < location.Scroll)
        {
            fixup.Add((byte)PhextConstants.SCROLL_BREAK);
            subspaceCoordinate.ScrollBreak();
        }

        byte[] scrollBytes = Encoding.UTF8.GetBytes(scroll);
        int max = bytes.Length;
        if (end > max) end = max;

        var result = new List<byte>();
        result.AddRange(bytes.Take(start));
        result.AddRange(fixup);
        result.AddRange(scrollBytes);
        result.AddRange(bytes.Skip(end));

        return Encoding.UTF8.GetString(result.ToArray());
    }

    /// <summary>
    /// Inserts content at the end of the scroll at the given coordinate
    /// </summary>
    public static string Insert(string phext, Coordinate location, string scroll)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(phext);
        var parts = GetSubspaceCoordinates(bytes, location);
        int end = parts.End;
        var subspaceCoordinate = new Coordinate(parts.Best);

        var fixup = new List<byte>();

        while (subspaceCoordinate.Library < location.Library)
        {
            fixup.Add((byte)PhextConstants.LIBRARY_BREAK);
            subspaceCoordinate.LibraryBreak();
        }
        while (subspaceCoordinate.Shelf < location.Shelf)
        {
            fixup.Add((byte)PhextConstants.SHELF_BREAK);
            subspaceCoordinate.ShelfBreak();
        }
        while (subspaceCoordinate.Series < location.Series)
        {
            fixup.Add((byte)PhextConstants.SERIES_BREAK);
            subspaceCoordinate.SeriesBreak();
        }
        while (subspaceCoordinate.Collection < location.Collection)
        {
            fixup.Add((byte)PhextConstants.COLLECTION_BREAK);
            subspaceCoordinate.CollectionBreak();
        }
        while (subspaceCoordinate.Volume < location.Volume)
        {
            fixup.Add((byte)PhextConstants.VOLUME_BREAK);
            subspaceCoordinate.VolumeBreak();
        }
        while (subspaceCoordinate.Book < location.Book)
        {
            fixup.Add((byte)PhextConstants.BOOK_BREAK);
            subspaceCoordinate.BookBreak();
        }
        while (subspaceCoordinate.Chapter < location.Chapter)
        {
            fixup.Add((byte)PhextConstants.CHAPTER_BREAK);
            subspaceCoordinate.ChapterBreak();
        }
        while (subspaceCoordinate.Section < location.Section)
        {
            fixup.Add((byte)PhextConstants.SECTION_BREAK);
            subspaceCoordinate.SectionBreak();
        }
        while (subspaceCoordinate.Scroll < location.Scroll)
        {
            fixup.Add((byte)PhextConstants.SCROLL_BREAK);
            subspaceCoordinate.ScrollBreak();
        }

        byte[] scrollBytes = Encoding.UTF8.GetBytes(scroll);

        var result = new List<byte>();
        result.AddRange(bytes.Take(end));
        result.AddRange(fixup);
        result.AddRange(scrollBytes);
        result.AddRange(bytes.Skip(end));

        return Encoding.UTF8.GetString(result.ToArray());
    }

    /// <summary>
    /// Removes the scroll at the given coordinate
    /// </summary>
    public static string Remove(string phext, Coordinate location)
    {
        string phase1 = Replace(phext, location, "");
        return Normalize(phase1);
    }

    /// <summary>
    /// Replaces content within a range of coordinates
    /// </summary>
    public static string RangeReplace(string phext, Range location, string scroll)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(phext);
        var partsStart = GetSubspaceCoordinates(bytes, location.Start);
        var partsEnd = GetSubspaceCoordinates(bytes, location.End);

        int start = partsStart.Start;
        int end = partsEnd.End;
        int max = bytes.Length;
        if (end > max) end = max;

        byte[] scrollBytes = Encoding.UTF8.GetBytes(scroll);

        var result = new List<byte>();
        result.AddRange(bytes.Take(start));
        result.AddRange(scrollBytes);
        result.AddRange(bytes.Skip(end));

        return Encoding.UTF8.GetString(result.ToArray());
    }

    /// <summary>
    /// Retrieves the next scroll from the given string, starting from the specified coordinate
    /// </summary>
    public static (PositionedScroll scroll, Coordinate nextStart, string remaining) NextScroll(string phext, Coordinate start)
    {
        var location = new Coordinate(start);
        byte[] p = Encoding.UTF8.GetBytes(phext);
        var output = new List<byte>();
        var remaining = new List<byte>();
        int pi = 0;
        var begin = new Coordinate(start);
        int pmax = p.Length;

        while (pi < pmax)
        {
            char test = (char)p[pi];
            bool dimensionBreak = false;

            if (test == PhextConstants.SCROLL_BREAK) { location.ScrollBreak(); dimensionBreak = true; }
            if (test == PhextConstants.SECTION_BREAK) { location.SectionBreak(); dimensionBreak = true; }
            if (test == PhextConstants.CHAPTER_BREAK) { location.ChapterBreak(); dimensionBreak = true; }
            if (test == PhextConstants.BOOK_BREAK) { location.BookBreak(); dimensionBreak = true; }
            if (test == PhextConstants.VOLUME_BREAK) { location.VolumeBreak(); dimensionBreak = true; }
            if (test == PhextConstants.COLLECTION_BREAK) { location.CollectionBreak(); dimensionBreak = true; }
            if (test == PhextConstants.SERIES_BREAK) { location.SeriesBreak(); dimensionBreak = true; }
            if (test == PhextConstants.SHELF_BREAK) { location.ShelfBreak(); dimensionBreak = true; }
            if (test == PhextConstants.LIBRARY_BREAK) { location.LibraryBreak(); dimensionBreak = true; }

            if (dimensionBreak)
            {
                if (output.Count > 0)
                {
                    pi++;
                    break;
                }
            }
            else
            {
                begin.CopyFrom(location);
                output.Add(p[pi]);
            }
            pi++;
        }

        while (pi < p.Length)
        {
            remaining.Add(p[pi]);
            pi++;
        }

        var outScroll = new PositionedScroll(begin, Encoding.UTF8.GetString(output.ToArray()));
        return (outScroll, location, Encoding.UTF8.GetString(remaining.ToArray()));
    }

    /// <summary>
    /// Transforms a packed phext buffer into a list of positioned scrolls (phokens)
    /// </summary>
    public static List<PositionedScroll> Phokenize(string phext)
    {
        var result = new List<PositionedScroll>();
        var coord = new Coordinate();
        string temp = phext;

        while (true)
        {
            var (item, nextCoord, remaining) = NextScroll(temp, coord);
            result.Add(item);
            coord = nextCoord;
            temp = remaining;
            if (string.IsNullOrEmpty(temp)) break;
        }

        return result;
    }

    /// <summary>
    /// Serializes a list of positioned scrolls back into a phext buffer
    /// </summary>
    public static string Dephokenize(List<PositionedScroll> tokens)
    {
        var result = new StringBuilder();
        var coord = new Coordinate();

        foreach (var ps in tokens)
        {
            if (!string.IsNullOrEmpty(ps.Scroll))
            {
                result.Append(AppendScroll(ps, coord));
            }
            coord = new Coordinate(ps.Coord);
        }

        return result.ToString();
    }

    /// <summary>
    /// Emits the delimiters and text required to append a positioned scroll
    /// </summary>
    public static string AppendScroll(PositionedScroll token, Coordinate coord)
    {
        var output = new StringBuilder();
        var current = new Coordinate(coord);

        while (current < token.Coord)
        {
            if (current.Library < token.Coord.Library) { output.Append(PhextConstants.LIBRARY_BREAK); current.LibraryBreak(); continue; }
            if (current.Shelf < token.Coord.Shelf) { output.Append(PhextConstants.SHELF_BREAK); current.ShelfBreak(); continue; }
            if (current.Series < token.Coord.Series) { output.Append(PhextConstants.SERIES_BREAK); current.SeriesBreak(); continue; }
            if (current.Collection < token.Coord.Collection) { output.Append(PhextConstants.COLLECTION_BREAK); current.CollectionBreak(); continue; }
            if (current.Volume < token.Coord.Volume) { output.Append(PhextConstants.VOLUME_BREAK); current.VolumeBreak(); continue; }
            if (current.Book < token.Coord.Book) { output.Append(PhextConstants.BOOK_BREAK); current.BookBreak(); continue; }
            if (current.Chapter < token.Coord.Chapter) { output.Append(PhextConstants.CHAPTER_BREAK); current.ChapterBreak(); continue; }
            if (current.Section < token.Coord.Section) { output.Append(PhextConstants.SECTION_BREAK); current.SectionBreak(); continue; }
            if (current.Scroll < token.Coord.Scroll) { output.Append(PhextConstants.SCROLL_BREAK); current.ScrollBreak(); continue; }
        }

        output.Append(token.Scroll);
        return output.ToString();
    }

    /// <summary>
    /// Explodes a phext into a dictionary of coordinates to scroll content
    /// </summary>
    public static Dictionary<Coordinate, string> Explode(string phext)
    {
        var parts = Phokenize(phext);
        var hash = new Dictionary<Coordinate, string>();
        foreach (var row in parts)
        {
            hash[new Coordinate(row.Coord)] = row.Scroll;
        }
        return hash;
    }

    /// <summary>
    /// Implodes a dictionary of coordinates back into a phext buffer
    /// </summary>
    public static string Implode(Dictionary<Coordinate, string> map)
    {
        var vec = map.Select(kv => new PositionedScroll(kv.Key, kv.Value)).ToList();
        vec.Sort();
        return Dephokenize(vec);
    }

    /// <summary>
    /// Zipper-merges two phext documents
    /// </summary>
    public static string Merge(string left, string right)
    {
        var tl = Phokenize(left);
        var tr = Phokenize(right);
        int tli = 0, tri = 0;
        int maxtl = tl.Count, maxtr = tr.Count;
        var result = new StringBuilder();
        var coord = new Coordinate();

        while (true)
        {
            bool haveLeft = tli < maxtl;
            bool haveRight = tri < maxtr;

            bool pickLeft = haveLeft && (!haveRight || tl[tli].Coord <= tr[tri].Coord);
            bool pickRight = haveRight && (!haveLeft || tr[tri].Coord <= tl[tli].Coord);

            if (pickLeft)
            {
                result.Append(AppendScroll(new PositionedScroll(tl[tli]), coord));
                coord = new Coordinate(tl[tli].Coord);
                tli++;
            }
            if (pickRight)
            {
                result.Append(AppendScroll(new PositionedScroll(tr[tri]), coord));
                coord = new Coordinate(tr[tri].Coord);
                tri++;
            }

            if (!pickLeft && !pickRight) break;
        }

        return result.ToString();
    }

    /// <summary>
    /// Subtracts scrolls from left where there is also content at those coordinates in right
    /// </summary>
    public static string Subtract(string left, string right)
    {
        var pl = Phokenize(left);
        var pr = Phokenize(right);
        var result = new StringBuilder();
        int pri = 0;
        int max = pr.Count;
        var coord = new Coordinate();

        foreach (var token in pl)
        {
            bool doAppend = pri == max;

            if (pri < max)
            {
                var compare = pr[pri];
                if (token.Coord < compare.Coord)
                {
                    doAppend = true;
                }
                else if (token.Coord == compare.Coord)
                {
                    pri++;
                }
            }

            if (doAppend)
            {
                result.Append(AppendScroll(new PositionedScroll(token), coord));
                coord = new Coordinate(token.Coord);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Normalizes a phext by removing empty scrolls
    /// </summary>
    public static string Normalize(string phext)
    {
        var arr = Phokenize(phext);
        return Dephokenize(arr);
    }

    /// <summary>
    /// Expands all dimension breaks by one level (line->scroll, scroll->section, etc.)
    /// </summary>
    public static string Expand(string phext)
    {
        var buffer = Encoding.UTF8.GetBytes(phext);
        for (int p = 0; p < buffer.Length; p++)
        {
            if (buffer[p] == PhextConstants.LINE_BREAK) buffer[p] = (byte)PhextConstants.SCROLL_BREAK;
            else if (buffer[p] == PhextConstants.SCROLL_BREAK) buffer[p] = (byte)PhextConstants.SECTION_BREAK;
            else if (buffer[p] == PhextConstants.SECTION_BREAK) buffer[p] = (byte)PhextConstants.CHAPTER_BREAK;
            else if (buffer[p] == PhextConstants.CHAPTER_BREAK) buffer[p] = (byte)PhextConstants.BOOK_BREAK;
            else if (buffer[p] == PhextConstants.BOOK_BREAK) buffer[p] = (byte)PhextConstants.VOLUME_BREAK;
            else if (buffer[p] == PhextConstants.VOLUME_BREAK) buffer[p] = (byte)PhextConstants.COLLECTION_BREAK;
            else if (buffer[p] == PhextConstants.COLLECTION_BREAK) buffer[p] = (byte)PhextConstants.SERIES_BREAK;
            else if (buffer[p] == PhextConstants.SERIES_BREAK) buffer[p] = (byte)PhextConstants.SHELF_BREAK;
            else if (buffer[p] == PhextConstants.SHELF_BREAK) buffer[p] = (byte)PhextConstants.LIBRARY_BREAK;
        }
        return Encoding.UTF8.GetString(buffer);
    }

    /// <summary>
    /// Contracts all dimension breaks by one level (library->shelf, shelf->series, etc.)
    /// </summary>
    public static string Contract(string phext)
    {
        var buffer = Encoding.UTF8.GetBytes(phext);
        for (int p = 0; p < buffer.Length; p++)
        {
            if (buffer[p] == PhextConstants.LIBRARY_BREAK) buffer[p] = (byte)PhextConstants.SHELF_BREAK;
            else if (buffer[p] == PhextConstants.SHELF_BREAK) buffer[p] = (byte)PhextConstants.SERIES_BREAK;
            else if (buffer[p] == PhextConstants.SERIES_BREAK) buffer[p] = (byte)PhextConstants.COLLECTION_BREAK;
            else if (buffer[p] == PhextConstants.COLLECTION_BREAK) buffer[p] = (byte)PhextConstants.VOLUME_BREAK;
            else if (buffer[p] == PhextConstants.VOLUME_BREAK) buffer[p] = (byte)PhextConstants.BOOK_BREAK;
            else if (buffer[p] == PhextConstants.BOOK_BREAK) buffer[p] = (byte)PhextConstants.CHAPTER_BREAK;
            else if (buffer[p] == PhextConstants.CHAPTER_BREAK) buffer[p] = (byte)PhextConstants.SECTION_BREAK;
            else if (buffer[p] == PhextConstants.SECTION_BREAK) buffer[p] = (byte)PhextConstants.SCROLL_BREAK;
            else if (buffer[p] == PhextConstants.SCROLL_BREAK) buffer[p] = (byte)PhextConstants.LINE_BREAK;
        }
        return Encoding.UTF8.GetString(buffer);
    }

    /// <summary>
    /// Creates a one-line summary of a scroll (max 32 chars)
    /// </summary>
    public static string CreateSummary(string phext)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(phext);
        int limit = Math.Min(32, buffer.Length);
        var summary = new List<byte>();

        for (int i = 0; i < limit; i++)
        {
            if (PhextConstants.IsPhextBreak(buffer[i])) break;
            summary.Add(buffer[i]);
        }

        if (summary.Count < buffer.Length)
        {
            summary.Add((byte)'.');
            summary.Add((byte)'.');
            summary.Add((byte)'.');
        }

        return Encoding.UTF8.GetString(summary.ToArray());
    }

    /// <summary>
    /// Creates an HTML navigation map of the phext
    /// </summary>
    public static string Navmap(string urlbase, string phext)
    {
        var phokens = Phokenize(phext);
        var result = new StringBuilder();

        if (phokens.Count > 0)
        {
            result.AppendLine("<ul>");
        }

        foreach (var phoken in phokens)
        {
            result.AppendLine($"<li><a href=\"{urlbase}{phoken.Coord.ToUrlEncoded()}\">{phoken.Coord} {CreateSummary(phoken.Scroll)}</a></li>");
        }

        if (phokens.Count > 0)
        {
            result.AppendLine("</ul>");
        }

        return result.ToString();
    }

    /// <summary>
    /// Creates a text-based navigation map of the phext
    /// </summary>
    public static string Textmap(string phext)
    {
        var phokens = Phokenize(phext);
        var result = new StringBuilder();

        foreach (var phoken in phokens)
        {
            result.AppendLine($"* {phoken.Coord}: {CreateSummary(phoken.Scroll)}");
        }

        return result.ToString();
    }

    /// <summary>
    /// Computes an XXH3-128 style checksum (using MD5 as stand-in, can be replaced with xxHash)
    /// Note: For true compatibility, integrate System.IO.Hashing.XxHash128
    /// </summary>
    public static string Checksum(string phext)
    {
        // Using MD5 as placeholder - for production, use System.IO.Hashing.XxHash128
        byte[] buffer = Encoding.UTF8.GetBytes(phext);
        using var md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(buffer);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Computes a hierarchical manifest of checksums for each scroll
    /// </summary>
    public static string Manifest(string phext)
    {
        var phokens = Phokenize(phext);
        for (int i = 0; i < phokens.Count; i++)
        {
            phokens[i].Scroll = Checksum(phokens[i].Scroll);
        }
        return Dephokenize(phokens);
    }

    /// <summary>
    /// Computes a soundex-style phonetic encoding for a scroll
    /// </summary>
    private static string SoundexInternal(string text)
    {
        const string letter1 = "bpfv";
        const string letter2 = "cskgjqxz";
        const string letter3 = "dt";
        const string letter4 = "l";
        const string letter5 = "mn";
        const string letter6 = "r";

        int value = 1;
        foreach (char c in text.ToLowerInvariant())
        {
            if (letter1.Contains(c)) { value += 1; continue; }
            if (letter2.Contains(c)) { value += 2; continue; }
            if (letter3.Contains(c)) { value += 3; continue; }
            if (letter4.Contains(c)) { value += 4; continue; }
            if (letter5.Contains(c)) { value += 5; continue; }
            if (letter6.Contains(c)) { value += 6; continue; }
        }

        return (value % 99).ToString();
    }

    /// <summary>
    /// Computes a hierarchical soundex of the phext
    /// </summary>
    public static string SoundexV1(string phext)
    {
        var phokens = Phokenize(phext);
        for (int i = 0; i < phokens.Count; i++)
        {
            phokens[i].Scroll = SoundexInternal(phokens[i].Scroll);
        }
        return Dephokenize(phokens);
    }

    /// <summary>
    /// Computes the offsets of each scroll in the phext
    /// </summary>
    private static List<PositionedScroll> IndexPhokens(string phext)
    {
        var phokens = Phokenize(phext);
        int offset = 0;
        var coord = new Coordinate();
        var output = new List<PositionedScroll>();

        for (int i = 0; i < phokens.Count; i++)
        {
            var reference = phokens[i].Coord;
            while (coord.Library < reference.Library) { coord.LibraryBreak(); offset++; }
            while (coord.Shelf < reference.Shelf) { coord.ShelfBreak(); offset++; }
            while (coord.Series < reference.Series) { coord.SeriesBreak(); offset++; }
            while (coord.Collection < reference.Collection) { coord.CollectionBreak(); offset++; }
            while (coord.Volume < reference.Volume) { coord.VolumeBreak(); offset++; }
            while (coord.Book < reference.Book) { coord.BookBreak(); offset++; }
            while (coord.Chapter < reference.Chapter) { coord.ChapterBreak(); offset++; }
            while (coord.Section < reference.Section) { coord.SectionBreak(); offset++; }
            while (coord.Scroll < reference.Scroll) { coord.ScrollBreak(); offset++; }

            output.Add(new PositionedScroll(new Coordinate(coord), offset.ToString()));
            offset += phokens[i].Scroll.Length;
        }

        return output;
    }

    /// <summary>
    /// Computes a hierarchical map of scroll offsets in phext format
    /// </summary>
    public static string Index(string phext)
    {
        var output = IndexPhokens(phext);
        return Dephokenize(output);
    }

    /// <summary>
    /// Computes the byte offset of a specific scroll coordinate
    /// </summary>
    public static int Offset(string phext, Coordinate coord)
    {
        var output = IndexPhokens(phext);

        var best = new Coordinate();
        bool matched = false;
        var fetchCoord = new Coordinate(coord);

        foreach (var phoken in output)
        {
            if (phoken.Coord <= coord)
            {
                best.CopyFrom(phoken.Coord);
            }
            if (phoken.Coord == coord)
            {
                matched = true;
            }
        }

        if (!matched)
        {
            fetchCoord.CopyFrom(best);
        }

        string index = Dephokenize(output);
        string offsetStr = Fetch(index, fetchCoord);
        return int.TryParse(offsetStr, out int result) ? result : 0;
    }
}
