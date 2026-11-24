// ===========================================================================================================
// libphext-cs: The Fourth Root - Store Tests (v0.7.0)
// Equivalence, thrash/growth, and genericity tests for DynamicPhext and
// TransitivePhext, ported from libphext-py's tests/test_stores.py.
// ===========================================================================================================

using Xunit;
using Phext;

namespace Phext.Tests;

public class StoreTests
{
    private static Coordinate C(int chapter, int section, int scroll) =>
        new Coordinate(1, 1, 1, 1, 1, 1, chapter, section, scroll);

    private static readonly string[] Texts =
    {
        "alpha scroll text\n", "beta\nsecond line\n", "gamma gamma gamma",
        "delta!", "epsilon scroll body text", "zeta\n", "eta theta iota",
        "kappa lambda mu nu xi"
    };

    private static List<Coordinate> Pool()
    {
        var pool = new List<Coordinate>();
        for (int ch = 1; ch <= 2; ch++)
            for (int sn = 1; sn <= 6; sn++)
                for (int sc = 1; sc <= 10; sc++)
                    pool.Add(C(ch, sn, sc));
        return pool;
    }

    // Seeded random edit-sequence equivalence vs the original string path,
    // asserting byte-identical serialization AFTER EVERY operation.
    private static void RunEquivalence(Action<Coordinate, string> insert,
                                       Action<Coordinate, string> replace,
                                       Action<Coordinate> remove,
                                       Func<Coordinate, string> fetch,
                                       Func<string> serialize)
    {
        var rng = new Random(20261124);
        var pool = Pool();
        string buffer = "";
        for (int op = 0; op < 400; op++)
        {
            var coord = pool[rng.Next(pool.Count)];
            string text = Texts[rng.Next(Texts.Length)];
            switch (rng.Next(4))
            {
                case 0:
                    buffer = PhextEngine.Insert(buffer, coord, text);
                    insert(coord, text);
                    break;
                case 1:
                    buffer = PhextEngine.Replace(buffer, coord, text);
                    replace(coord, text);
                    break;
                case 2:
                    buffer = PhextEngine.Remove(buffer, coord);
                    remove(coord);
                    break;
                default:
                    Assert.Equal(PhextEngine.Fetch(buffer, coord), fetch(coord));
                    break;
            }
            Assert.Equal(buffer, serialize());
        }
        foreach (var coord in pool)
            Assert.Equal(PhextEngine.Fetch(buffer, coord), fetch(coord));
    }

    [Fact]
    public void Dynamic_Equivalence_SeededRandomOps()
    {
        var store = new DynamicPhext();
        RunEquivalence(store.Insert, store.Replace, store.Remove, store.Fetch, store.Serialize);
    }

    [Fact]
    public void Transitive_Equivalence_SeededRandomOps()
    {
        var store = new TransitivePhext();
        RunEquivalence(store.Insert, store.Replace, store.Remove, store.Fetch, store.Serialize);
    }

    [Fact]
    public void Stores_LoadAndSerialize_RoundTrip()
    {
        string buffer = "";
        buffer = PhextEngine.Insert(buffer, C(1, 1, 1), "first scroll");
        buffer = PhextEngine.Insert(buffer, C(1, 3, 2), "later scroll\nwith lines");
        buffer = PhextEngine.Insert(buffer, C(2, 1, 1), "chapter two");
        Assert.Equal(buffer, new DynamicPhext(buffer).Serialize());
        Assert.Equal(buffer, new TransitivePhext(buffer).Serialize());
        Assert.Equal("", new DynamicPhext().Serialize());
        Assert.Equal("", new TransitivePhext().Serialize());
    }

    [Fact]
    public void Stores_InsertConcatenates_ReplaceOverwrites_FetchAbsentEmpty()
    {
        foreach (var (insert, replace, fetch) in Stores())
        {
            var coord = C(1, 2, 3);
            Assert.Equal("", fetch(coord));
            insert(coord, "hello ");
            insert(coord, "world");
            Assert.Equal("hello world", fetch(coord));
            replace(coord, "overwritten");
            Assert.Equal("overwritten", fetch(coord));
        }
    }

    private static IEnumerable<(Action<Coordinate, string>, Action<Coordinate, string>, Func<Coordinate, string>)> Stores()
    {
        var d = new DynamicPhext();
        yield return (d.Insert, d.Replace, d.Fetch);
        var t = new TransitivePhext();
        yield return (t.Insert, t.Replace, t.Fetch);
    }

    [Fact]
    public void Dynamic_Growth_AmortizedCopies()
    {
        var store = new DynamicPhext();
        for (int i = 0; i < 1000; i++) store.Append(C(1, 1, i + 1), Texts[i % Texts.Length]);
        Assert.Equal(1000, store.Count);
        Assert.Equal(10, store.ResizeCount);          // capacities 1,2,4,...,1024
        Assert.Equal(1023, store.ElementsCopied);     // 1+2+...+512 < 2N
        Assert.Equal(1024, store.Capacity);
    }

    [Fact]
    public void Dynamic_NoShrinkAtHalfFull_ShrinkAtQuarterFull()
    {
        var store = new DynamicPhext();
        for (int i = 0; i < 64; i++) store.Append(C(1, 1, i + 1), "x");
        Assert.Equal(64, store.Capacity);
        for (int i = 64; i > 32; i--) store.Remove(C(1, 1, i));
        Assert.Equal(32, store.Count);
        Assert.Equal(64, store.Capacity);             // half full: NO shrink
        for (int i = 32; i > 16; i--) store.Remove(C(1, 1, i));
        Assert.Equal(16, store.Count);
        Assert.Equal(32, store.Capacity);             // quarter full: halved
    }

    [Fact]
    public void Dynamic_Thrash_QuarterFullVsHalfFull()
    {
        // At a 64-scroll boundary over 50 alternating append/remove pairs,
        // the quarter-full store copies 64 elements total (one grow); the
        // half-full variant copies 6,400 (a grow + a shrink per pair).
        var quarter = new DynamicPhext();
        var half = new DynamicPhext("", shrinkDivisor: 2);
        foreach (var store in new[] { quarter, half })
        {
            for (int i = 0; i < 64; i++) store.Append(C(1, 1, i + 1), "x");
            store.ResetStats();
            for (int p = 0; p < 50; p++)
            {
                store.Append(C(1, 1, 65), "tail");
                store.Remove(C(1, 1, 65));
            }
        }
        Assert.Equal(64, quarter.ElementsCopied);
        Assert.Equal(6400, half.ElementsCopied);
    }

    [Fact]
    public void Dynamic_AppendRequiresTailOrder()
    {
        var store = new DynamicPhext();
        store.Append(C(1, 1, 5), "five");
        Assert.Throws<ArgumentException>(() => store.Append(C(1, 1, 5), "again"));
        Assert.Throws<ArgumentException>(() => store.Append(C(1, 1, 3), "backwards"));
    }

    [Fact]
    public void Dynamic_ShrinkFloorReachesCapacityOne()
    {
        var store = new DynamicPhext();
        for (int i = 0; i < 8; i++) store.Append(C(1, 1, i + 1), "x");
        for (int i = 8; i > 0; i--) store.Remove(C(1, 1, i));
        Assert.Equal(0, store.Count);
        Assert.Equal(1, store.Capacity);
    }

    [Fact]
    public void BranchingMap_GenericDepths()
    {
        var m3 = new BranchingMap<string>(3);
        Assert.True(m3.Put(new[] { 2, 1, 1 }, "a"));
        Assert.True(m3.Put(new[] { 1, 9, 9 }, "b"));
        Assert.False(m3.Put(new[] { 2, 1, 1 }, "a2"));
        Assert.Equal(2, m3.Count);
        Assert.True(m3.TryFetch(new[] { 2, 1, 1 }, out var v));
        Assert.Equal("a2", v);
        Assert.False(m3.TryFetch(new[] { 9, 9, 9 }, out _));
        var keys = m3.Items().Select(it => string.Join(",", it.Key)).ToList();
        Assert.Equal(new[] { "1,9,9", "2,1,1" }, keys); // sorted at every level
        Assert.True(m3.Remove(new[] { 2, 1, 1 }));
        Assert.False(m3.Remove(new[] { 2, 1, 1 }));
        Assert.Equal(1, m3.Count);
        Assert.Throws<ArgumentException>(() => m3.Put(new[] { 1, 2 }, "bad"));

        var m1 = new BranchingMap<int>(1);
        Assert.True(m1.Put(new[] { 7 }, 42));
        Assert.True(m1.TryFetch(new[] { 7 }, out var n));
        Assert.Equal(42, n);
    }

    [Fact]
    public void Transitive_DepthDerivedFromCoordinate()
    {
        var store = new TransitivePhext();
        Assert.Equal(CoordinateKey.Of(new Coordinate()).Length, store.Depth);
        Assert.Equal(9, store.Depth);
    }
    // Regression: pre-existing string-path defect found by the equivalence
    // gate above. GetSubspaceCoordinates left start=0 when the walker crossed
    // the target on a break byte, so Replace at an absent coordinate in a gap
    // dropped the buffer's leading break (shifting later scrolls one
    // dimension) and Fetch of such a coordinate returned a break character.
    [Fact]
    public void StringPath_GapReplaceAndFetch()
    {
        string buffer = "";
        buffer = PhextEngine.Insert(buffer, C(1, 3, 6), "gamma gamma gamma");
        var gap = C(1, 2, 6);
        Assert.Equal("", PhextEngine.Fetch(buffer, gap));
        string replaced = PhextEngine.Replace(buffer, gap, "delta!");
        Assert.Equal("delta!", PhextEngine.Fetch(replaced, gap));
        Assert.Equal("gamma gamma gamma", PhextEngine.Fetch(replaced, C(1, 3, 6)));
        var dyn = new DynamicPhext(buffer);
        dyn.Replace(gap, "delta!");
        Assert.Equal(replaced, dyn.Serialize());
        var tra = new TransitivePhext(buffer);
        tra.Replace(gap, "delta!");
        Assert.Equal(replaced, tra.Serialize());
    }
}
