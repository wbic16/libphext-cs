// ===========================================================================================================
// libphext-cs: The Fourth Root
// DynamicPhext.cs - Capacity-managed dynamic-array scroll store (v0.7.0)
//
// Ported from libphext-py phext/dynamic.py (the proven reference structure).
//
// The PhextEngine buffer API is buffer-in/buffer-out on immutable strings:
// every Insert/Replace/Remove re-encodes the whole buffer, scans it byte by
// byte, and copies it into a fresh List<byte> — so a sequence of N edits on a
// growing phext costs O(N x size), quadratic overall. DynamicPhext pays the
// phokenize once: it holds the scrolls in a capacity-managed dynamic array
// and mutates it in place, serializing on demand through
// PhextEngine.Dephokenize.
//
// Capacity discipline (CLRS problem 3-5, the dynamic-table strategy):
// start at capacity 1, double when full on append, and shrink only when the
// load drops to one quarter — never at half full. Shrinking at half full
// thrashes: at a resize boundary, alternating inserts and deletes each copy
// the whole array, for quadratic total copying. With the quarter-full rule a
// halving leaves the array at most half full, so a grow can never immediately
// follow a shrink, and append/pop at the tail are amortized O(1). Resize
// events are instrumented (ResizeCount, ElementsCopied) so tests can assert
// the amortized behavior directly.
//
// Edit semantics are PhextEngine's own, exactly: insert at an existing
// coordinate concatenates old text before new, replace overwrites, an absent
// coordinate is placed in coordinate order, and fetch returns the scroll's
// text or "". The array is kept in strictly increasing coordinate order
// (phokenize order), so Serialize() is byte-identical to the string path over
// any edit sequence — the equivalence tests in tests/StoreTests.cs assert
// this after every operation.
//
// Domain note: phokenize never yields an empty scroll in a real buffer, and
// real scroll text carries no dimension-break characters. The store holds
// that invariant — it never stores an empty scroll. The degenerate empty-text
// edits therefore follow the normalized string semantics rather than the raw
// intermediate buffer: Replace(coord, "") removes the scroll (exactly
// PhextEngine.Remove), and Insert of "" is a no-op.
//
// The trade is memory: the backing array holds up to 2x the live scroll
// count in slots (load factor stays above 1/4), plus one PositionedScroll
// per scroll. For a single edit, the string API is the better tool; this
// store is for edit sequences.
// ===========================================================================================================

namespace Phext;

/// <summary>
/// A phext scroll store over a capacity-managed dynamic array:
/// double capacity on grow, shrink only at quarter-full.
/// </summary>
public class DynamicPhext
{
    private const int MinCapacity = 1;

    private readonly int _shrinkDivisor;
    private PositionedScroll[] _data;
    private int _n;
    private int _resizeCount;
    private long _elementsCopied;

    /// <param name="buffer">Initial phext buffer to phokenize into the store.</param>
    /// <param name="shrinkDivisor">
    /// Load denominator at which the array halves: the correct strategy is 4
    /// (quarter full). The value 2 reproduces the textbook's bad half-full
    /// strategy and exists so the thrash test can measure it; production use
    /// should never pass it.
    /// </param>
    public DynamicPhext(string buffer = "", int shrinkDivisor = 4)
    {
        if (shrinkDivisor < 2)
            throw new ArgumentOutOfRangeException(nameof(shrinkDivisor), "shrinkDivisor must be >= 2");
        _shrinkDivisor = shrinkDivisor;
        _data = new PositionedScroll[MinCapacity];
        _n = 0;
        if (!string.IsNullOrEmpty(buffer)) Load(buffer);
    }

    // -- capacity machinery --

    public int Capacity => _data.Length;
    public int ResizeCount => _resizeCount;
    public long ElementsCopied => _elementsCopied;

    public void ResetStats()
    {
        _resizeCount = 0;
        _elementsCopied = 0;
    }

    private void Resize(int newCapacity)
    {
        var newData = new PositionedScroll[newCapacity];
        Array.Copy(_data, newData, _n);
        _data = newData;
        _resizeCount++;
        _elementsCopied += _n;
    }

    private void GrowIfFull()
    {
        if (_n == _data.Length) Resize(2 * _data.Length);
    }

    private void ShrinkIfSparse()
    {
        // Quarter-full rule: after halving, the load is at most 1/2, so the
        // next grow is at least capacity/4 appends away — no thrash.
        if (_data.Length > MinCapacity && _n <= _data.Length / _shrinkDivisor)
            Resize(Math.Max(MinCapacity, _data.Length / 2));
    }

    // -- loading and serialization --

    public void Load(string buffer)
    {
        _data = new PositionedScroll[MinCapacity];
        _n = 0;
        ResetStats();
        foreach (var ps in PhextEngine.Phokenize(buffer))
        {
            if (ps.Scroll.Length == 0) continue; // the store never holds an empty scroll
            Push(ps);
        }
    }

    public List<PositionedScroll> Scrolls()
    {
        var result = new List<PositionedScroll>(_n);
        for (int i = 0; i < _n; i++) result.Add(_data[i]);
        return result;
    }

    public string Serialize() => PhextEngine.Dephokenize(Scrolls());

    // -- lookup --

    // Binary search over the coordinate-ordered array. Returns the index;
    // when not found, index is the insertion point and found is false.
    private (int Index, bool Found) Find(Coordinate coord)
    {
        int lo = 0, hi = _n;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            int cmp = _data[mid].Coord.CompareTo(coord);
            if (cmp < 0) lo = mid + 1;
            else if (cmp > 0) hi = mid;
            else return (mid, true);
        }
        return (lo, false);
    }

    public string Fetch(Coordinate coord)
    {
        var (index, found) = Find(coord);
        return found ? _data[index].Scroll : "";
    }

    public bool Contains(Coordinate coord) => Find(coord).Found;

    public int Count => _n;

    // -- edits: PhextEngine update semantics, in place --

    private void Push(PositionedScroll ps)
    {
        GrowIfFull();
        _data[_n] = ps;
        _n++;
    }

    /// <summary>Amortized O(1) tail append; the coordinate must extend the tail.</summary>
    public void Append(Coordinate coord, string text)
    {
        if (_n > 0 && coord.CompareTo(_data[_n - 1].Coord) <= 0)
            throw new ArgumentException("append requires a coordinate past the current tail", nameof(coord));
        if (text.Length == 0) return;
        Push(new PositionedScroll(coord, text));
    }

    public void Update(Coordinate coord, string text, bool overwrite)
    {
        var (index, found) = Find(coord);
        if (found)
        {
            if (text.Length == 0)
            {
                if (overwrite) RemoveAt(index);
                return;
            }
            if (!overwrite) text = _data[index].Scroll + text;
            _data[index] = new PositionedScroll(coord, text);
            return;
        }
        if (text.Length == 0) return;
        GrowIfFull();
        for (int i = _n; i > index; i--) _data[i] = _data[i - 1];
        _data[index] = new PositionedScroll(coord, text);
        _n++;
    }

    /// <summary>Insert concatenates onto an existing scroll (PhextEngine.Insert semantics).</summary>
    public void Insert(Coordinate coord, string text) => Update(coord, text, false);

    /// <summary>Replace overwrites an existing scroll (PhextEngine.Replace semantics).</summary>
    public void Replace(Coordinate coord, string text) => Update(coord, text, true);

    private void RemoveAt(int index)
    {
        for (int i = index; i < _n - 1; i++) _data[i] = _data[i + 1];
        _n--;
        _data[_n] = null!;
        ShrinkIfSparse();
    }

    public void Remove(Coordinate coord)
    {
        var (index, found) = Find(coord);
        if (found) RemoveAt(index);
    }
}
