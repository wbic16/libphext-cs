// ===========================================================================================================
// libphext-cs: The Fourth Root - Linear-time phokenize tests
// Ported from libphext-rs src/test_phokenize.rs
//
// Copyright: (c) 2024-2025 Will Bickford (Phext, Inc.)
// License: MIT
// ===========================================================================================================

using System.Diagnostics;
using System.Text;
using Xunit;
using Phext;

namespace Phext.Tests;

public class PhokenizeTests : Constants
{
    /// <summary>
    /// The earlier Phokenize: NextScroll in a loop. Quadratic, but the definition of the expected output.
    /// </summary>
    private static List<PositionedScroll> ReferencePhokenize(string phext)
    {
        var result = new List<PositionedScroll>();
        var coord = new Coordinate();
        string temp = phext;

        while (true)
        {
            var (item, nextCoord, remaining) = PhextEngine.NextScroll(temp, coord);
            result.Add(item);
            coord = nextCoord;
            temp = remaining;
            if (string.IsNullOrEmpty(temp)) break;
        }

        return result;
    }

    /// <summary>
    /// The earlier GetSubspaceCoordinates, which compared the walker with the target on every byte.
    /// </summary>
    private static SubspaceBeacon ReferenceSubspaceCoordinates(byte[] subspace, Coordinate target)
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
                if (compare == SB) walker.ScrollBreak();
                if (compare == SN) walker.SectionBreak();
                if (compare == CH) walker.ChapterBreak();
                if (compare == BK) walker.BookBreak();
                if (compare == VM) walker.VolumeBreak();
                if (compare == CN) walker.CollectionBreak();
                if (compare == SR) walker.SeriesBreak();
                if (compare == SF) walker.ShelfBreak();
                if (compare == LB) walker.LibraryBreak();
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

    private static List<(string, string)> Pairs(List<PositionedScroll> tokens) =>
        tokens.Select(token => (token.Coord.ToString(), token.Scroll)).ToList();

    private static readonly char[] Breaks = { SB, SN, CH, BK, VM, CN, SR, SF, LB };

    private static IEnumerable<string> EdgeCases()
    {
        yield return "";
        yield return "one scroll\nwith two lines";
        yield return "a" + SB + "b" + SN + "c" + CH + "d" + BK + "e" + VM + "f" + CN + "g" + SR + "h" + SF + "i" + LB + "j";
        yield return "" + SB + SB + "lead" + SB + SB + SB + "gap" + LB + LB + SB + "deep" + SB;
        yield return "trailing one" + SB;
        yield return "trailing two" + SB + SB;
        yield return "" + LB + SF + SR + CN + VM + BK + CH + SN + SB;
        yield return "" + SB;
        yield return "ünïcödé ✓" + SF + "shelf ☃" + BK + "boek ü" + SB + "🜁";
        yield return "\n\n" + SB + "\n";
        yield return "\r\n" + SB + "\u0007cowbell" + SB + "\0nul";
        // Lone surrogates become U+FFFD when encoded as UTF-8, in both implementations
        yield return "\uD83D" + SB + "\uDE01" + SB + "x\uD83D";
        // More breaks than a dimension holds: the coordinate saturates at the maximum
        yield return "first" + new string(SB, 1005) + "last" + new string(LB, 1001) + "beyond";
    }

    /// <summary>
    /// Small deterministic generator, so a failure always reproduces.
    /// </summary>
    private sealed class Lcg
    {
        private ulong _state;
        public Lcg(ulong seed) { _state = seed; }
        public ulong Next()
        {
            _state = _state * 6364136223846793005UL + 1442695040888963407UL;
            return _state >> 33;
        }
    }

    private static string RandomDoc(Lcg rng)
    {
        string[] pieces = { "a", "bc", "ü", "☃", "🜁", "\n", " ", "text" };
        var doc = new StringBuilder();
        ulong count = rng.Next() % 60;
        for (ulong i = 0; i < count; i++)
        {
            if (rng.Next() % 3 == 0)
            {
                ulong run = 1 + rng.Next() % 3;
                for (ulong r = 0; r < run; r++)
                {
                    doc.Append(Breaks[rng.Next() % 9]);
                }
            }
            else
            {
                doc.Append(pieces[rng.Next() % (ulong)pieces.Length]);
            }
        }
        return doc.ToString();
    }

    private static IEnumerable<string> RandomDocs(ulong seed, int count)
    {
        var rng = new Lcg(seed);
        for (int i = 0; i < count; i++)
        {
            yield return RandomDoc(rng);
        }
    }

    private static string Show(string doc) =>
        string.Concat(doc.Select(c => c < ' ' ? $"\\x{(int)c:X2}" : c.ToString()));

    [Fact]
    public void Test_PhokenizeMatchesTheNextScrollLoop()
    {
        foreach (var doc in EdgeCases().Concat(RandomDocs(0x5eed, 5_000)))
        {
            var expected = Pairs(ReferencePhokenize(doc));
            var actual = Pairs(PhextEngine.Phokenize(doc));
            Assert.True(expected.SequenceEqual(actual), $"phokenize differs for \"{Show(doc)}\"");
        }
    }

    [Fact]
    public void Test_SubspaceCoordinatesMatchThePerByteScan()
    {
        var rng = new Lcg(0xbeac0);
        foreach (var doc in EdgeCases().Concat(RandomDocs(0xc0de, 2_000)))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(doc);
            var targets = ReferencePhokenize(doc).Select(token => token.Coord).ToList();
            for (int i = 0; i < 4; i++)
            {
                targets.Add(new Coordinate(
                    1 + (int)(rng.Next() % 3), 1 + (int)(rng.Next() % 3), 1 + (int)(rng.Next() % 3),
                    1 + (int)(rng.Next() % 3), 1 + (int)(rng.Next() % 3), 1 + (int)(rng.Next() % 3),
                    1 + (int)(rng.Next() % 3), 1 + (int)(rng.Next() % 3), 1 + (int)(rng.Next() % 4)));
            }

            foreach (var target in targets)
            {
                var expected = ReferenceSubspaceCoordinates(bytes, target);
                var actual = PhextEngine.GetSubspaceCoordinates(bytes, target);
                string context = $"{target} in \"{Show(doc)}\"";
                Assert.True(expected.Start == actual.Start, $"start differs for {context}");
                Assert.True(expected.End == actual.End, $"end differs for {context}");
                Assert.True(expected.Best == actual.Best, $"best differs for {context}");
            }
        }
    }

    [Fact]
    public void Test_ExplodeAndImplodeRoundtripTheSameScrolls()
    {
        static List<(string, string)> Filled(Dictionary<Coordinate, string> map) =>
            map.Where(kv => kv.Value.Length > 0)
               .Select(kv => (kv.Key.ToString(), kv.Value))
               .OrderBy(pair => pair.Item1, StringComparer.Ordinal)
               .ThenBy(pair => pair.Item2, StringComparer.Ordinal)
               .ToList();

        foreach (var doc in EdgeCases().Concat(RandomDocs(0xfab1e, 2_000)))
        {
            var map = PhextEngine.Explode(doc);
            var rebuilt = PhextEngine.Implode(map);
            var again = PhextEngine.Explode(rebuilt);
            Assert.True(Filled(map).SequenceEqual(Filled(again)), $"roundtrip differs for \"{Show(doc)}\"");
        }
    }

    [Fact]
    public void Test_ALargePhextExplodesInLinearTime()
    {
        var doc = new StringBuilder();
        for (int index = 0; index < 50_000; index++)
        {
            if (index > 0)
            {
                // 40 scrolls a book, 40 books a volume: every coordinate stays in range
                doc.Append(index % 1600 == 0 ? VM : index % 40 == 0 ? BK : SB);
            }
            doc.Append("kind: message\nauthor: bench\n---\nsome words about the topic here");
        }
        string phext = doc.ToString();

        var watch = Stopwatch.StartNew();
        var map = PhextEngine.Explode(phext);
        var rebuilt = PhextEngine.Implode(map);
        var toc = PhextEngine.Textmap(phext);
        var last = PhextEngine.Fetch(phext, map.Keys.Max()!);
        watch.Stop();

        Assert.Equal(50_000, map.Count);
        Assert.Equal(phext, rebuilt);
        Assert.Equal(50_000, toc.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal("kind: message\nauthor: bench\n---\nsome words about the topic here", last);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"took {watch.Elapsed}");
    }
}
