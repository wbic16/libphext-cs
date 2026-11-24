// ===========================================================================================================
// libphext-cs: The Fourth Root
// TransitivePhext.cs - Branching hash-map scroll store (v0.7.0)
//
// Ported from libphext-py phext/transitive.py (the proven reference structure).
//
// The structure the original Phext Notepad used: hash maps of hash maps,
// transitively. A coordinate is addressed by walking one hash map per
// dimension — the library map's values are shelf maps, whose values are
// series maps, and so on down to scroll maps whose values are scroll text.
// Fetch, insert, and remove touch exactly one Dictionary per dimension:
// no sort, no scan, no binary search, and no dependence on how many scrolls
// the phext holds.
//
// The structure is dimension-generic, not a fixed 9-level chain.
// BranchingMap<TValue> is ONE recursive generic type — a hash map of
// dimension d whose values are hash maps of dimension d-1, recursively, down
// to leaves — with fetch/put/remove/items all defined recursively over the
// hierarchy. The depth is a parameter of the instantiation, not of the
// structure: TransitivePhext instantiates it at the depth derived from the
// Coordinate type itself (the length of a coordinate's key projection),
// never a hardcoded 9-step chain.
//
// Edit semantics are PhextEngine's own, exactly, as in DynamicPhext: insert
// at an existing coordinate concatenates old text before new, replace
// overwrites, fetch returns the scroll's text or "", and the store never
// holds an empty scroll (Replace(coord, "") removes, Insert of "" is a
// no-op — the normalized string semantics). Serialization walks the
// hierarchy in sorted key order at every level, which is coordinate order,
// so Serialize() is byte-identical to the string path over any edit
// sequence; tests/StoreTests.cs asserts this after every operation.
//
// The trade is memory and constant factors: one Dictionary per populated
// coordinate prefix, and a per-dimension lookup walk per operation
// regardless of phext size. Sparse phexts share prefixes heavily, so the
// dictionary count stays far below the scroll count in practice. For a
// single edit, the string API is the better tool; this store is for edit
// sequences and fetch-heavy work against one phext.
// ===========================================================================================================

namespace Phext;

/// <summary>
/// The coordinate key projection shared by the branching store: one integer
/// component per coordinate dimension, in Coordinate order (library, shelf,
/// series, collection, volume, book, chapter, section, scroll). The store's
/// depth is the length of this projection, so it follows the Coordinate
/// type rather than a hardcoded dimension count.
/// </summary>
public static class CoordinateKey
{
    public static int[] Of(Coordinate coord) => new[]
    {
        coord.Library, coord.Shelf, coord.Series,
        coord.Collection, coord.Volume, coord.Book,
        coord.Chapter, coord.Section, coord.Scroll
    };

    public static Coordinate ToCoordinate(int[] key) =>
        new Coordinate(key[0], key[1], key[2], key[3], key[4], key[5], key[6], key[7], key[8]);
}

/// <summary>
/// A hash map of dimension <see cref="Depth"/> branching into hash maps of
/// one fewer dimension, recursively, down to leaves holding values.
/// Keys are integer arrays of exactly Depth components. Every operation is
/// defined recursively over the hierarchy: one Dictionary lookup per level.
/// </summary>
public class BranchingMap<TValue>
{
    // Node values are either BranchingMap<TValue> (depth > 1 levels) or
    // TValue leaves (last level); both live in the same Dictionary per node.
    private readonly Dictionary<int, object> _root = new();
    private int _count;

    public BranchingMap(int depth)
    {
        if (depth < 1) throw new ArgumentOutOfRangeException(nameof(depth), "depth must be >= 1");
        Depth = depth;
    }

    public int Depth { get; }
    public int Count => _count;

    private void Check(int[] key)
    {
        if (key.Length != Depth)
            throw new ArgumentException($"key has {key.Length} components, map depth is {Depth}", nameof(key));
    }

    // -- fetch: one lookup per dimension, recursively --

    public bool TryFetch(int[] key, out TValue? value)
    {
        Check(key);
        return TryFetch(_root, key, 0, out value);
    }

    private static bool TryFetch(Dictionary<int, object> node, int[] key, int level, out TValue? value)
    {
        if (!node.TryGetValue(key[level], out var child))
        {
            value = default;
            return false;
        }
        if (level == key.Length - 1)
        {
            value = (TValue)child;
            return true;
        }
        return TryFetch((Dictionary<int, object>)child, key, level + 1, out value);
    }

    public bool Contains(int[] key) => TryFetch(key, out _);

    // -- put: descend recursively, branching new maps as needed --

    /// <summary>Store value at key; returns true when a new leaf was created.</summary>
    public bool Put(int[] key, TValue value)
    {
        Check(key);
        bool created = Put(_root, key, 0, value);
        if (created) _count++;
        return created;
    }

    private bool Put(Dictionary<int, object> node, int[] key, int level, TValue value)
    {
        int component = key[level];
        if (level == Depth - 1)
        {
            bool created = !node.ContainsKey(component);
            node[component] = value!;
            return created;
        }
        if (!node.TryGetValue(component, out var childObj))
        {
            childObj = new Dictionary<int, object>();
            node[component] = childObj;
        }
        return Put((Dictionary<int, object>)childObj, key, level + 1, value);
    }

    // -- remove: recursive, pruning branches left empty --

    public bool Remove(int[] key)
    {
        Check(key);
        bool removed = Remove(_root, key, 0);
        if (removed) _count--;
        return removed;
    }

    private bool Remove(Dictionary<int, object> node, int[] key, int level)
    {
        int component = key[level];
        if (level == Depth - 1)
        {
            return node.Remove(component);
        }
        if (!node.TryGetValue(component, out var childObj)) return false;
        var child = (Dictionary<int, object>)childObj;
        bool removed = Remove(child, key, level + 1);
        if (removed && child.Count == 0) node.Remove(component);
        return removed;
    }

    // -- items: recursive traversal, sorted at every level, which is
    //    lexicographic (coordinate) order over the full key --

    public IEnumerable<(int[] Key, TValue Value)> Items()
    {
        foreach (var item in Items(_root, new int[Depth], 0)) yield return item;
    }

    private IEnumerable<(int[] Key, TValue Value)> Items(Dictionary<int, object> node, int[] prefix, int level)
    {
        var components = new List<int>(node.Keys);
        components.Sort();
        foreach (int component in components)
        {
            prefix[level] = component;
            object child = node[component];
            if (level == Depth - 1)
            {
                yield return ((int[])prefix.Clone(), (TValue)child);
            }
            else
            {
                foreach (var item in Items((Dictionary<int, object>)child, prefix, level + 1))
                    yield return item;
            }
        }
    }
}

/// <summary>
/// Phext scroll store over a BranchingMap at the Coordinate depth — one
/// hash-map layer per coordinate dimension, leaves holding scroll text.
/// The depth is derived from Coordinate, not hardcoded.
/// </summary>
public class TransitivePhext
{
    private BranchingMap<string> _map;

    public TransitivePhext(string buffer = "")
    {
        Depth = CoordinateKey.Of(new Coordinate()).Length;
        _map = new BranchingMap<string>(Depth);
        if (!string.IsNullOrEmpty(buffer)) Load(buffer);
    }

    /// <summary>Branching depth, derived from the Coordinate type.</summary>
    public int Depth { get; }

    // -- loading and serialization --

    public void Load(string buffer)
    {
        _map = new BranchingMap<string>(Depth);
        foreach (var ps in PhextEngine.Phokenize(buffer))
        {
            if (ps.Scroll.Length == 0) continue; // the store never holds an empty scroll
            _map.Put(CoordinateKey.Of(ps.Coord), ps.Scroll);
        }
    }

    public List<PositionedScroll> Scrolls()
    {
        var result = new List<PositionedScroll>(_map.Count);
        foreach (var (key, text) in _map.Items())
            result.Add(new PositionedScroll(CoordinateKey.ToCoordinate(key), text));
        return result;
    }

    public string Serialize() => PhextEngine.Dephokenize(Scrolls());

    // -- lookup: one Dictionary lookup per dimension, independent of phext size --

    public string Fetch(Coordinate coord) =>
        _map.TryFetch(CoordinateKey.Of(coord), out var value) ? value! : "";

    public bool Contains(Coordinate coord) => _map.Contains(CoordinateKey.Of(coord));

    public int Count => _map.Count;

    // -- edits: PhextEngine update semantics, by hash walk --

    public void Update(Coordinate coord, string text, bool overwrite)
    {
        int[] key = CoordinateKey.Of(coord);
        if (text.Length == 0)
        {
            if (overwrite) _map.Remove(key);
            return;
        }
        if (!overwrite && _map.TryFetch(key, out var existing))
            text = existing + text;
        _map.Put(key, text);
    }

    /// <summary>Insert concatenates onto an existing scroll (PhextEngine.Insert semantics).</summary>
    public void Insert(Coordinate coord, string text) => Update(coord, text, false);

    /// <summary>Replace overwrites an existing scroll (PhextEngine.Replace semantics).</summary>
    public void Replace(Coordinate coord, string text) => Update(coord, text, true);

    public void Remove(Coordinate coord) => _map.Remove(CoordinateKey.Of(coord));

    /// <summary>
    /// A hash hierarchy has no tail; append is insert. Kept for API parity
    /// with DynamicPhext, whose append is the amortized-O(1) tail operation
    /// of an ordered array.
    /// </summary>
    public void Append(Coordinate coord, string text) => Insert(coord, text);
}
