// ===========================================================================================================
// libphext-cs: The Fourth Root
// Coordinate.cs - 9-dimensional phext coordinate addressing
//
// Copyright: (c) 2024-2025 Will Bickford (Phext, Inc.)
// License: MIT
// ===========================================================================================================

namespace Phext;

/// <summary>
/// Represents a 9-dimensional coordinate in phext address space.
/// Format: library.shelf.series/collection.volume.book/chapter.section.scroll
/// </summary>
public class Coordinate : IComparable<Coordinate>, IEquatable<Coordinate>
{
    public const int COORDINATE_MINIMUM = 1;
    public const int COORDINATE_MAXIMUM = 1000;

    // Z-arm: Library, Shelf, Series
    public int Library { get; set; }
    public int Shelf { get; set; }
    public int Series { get; set; }

    // Y-arm: Collection, Volume, Book
    public int Collection { get; set; }
    public int Volume { get; set; }
    public int Book { get; set; }

    // X-arm: Chapter, Section, Scroll
    public int Chapter { get; set; }
    public int Section { get; set; }
    public int Scroll { get; set; }

    /// <summary>
    /// Creates a default coordinate at 1.1.1/1.1.1/1.1.1
    /// </summary>
    public Coordinate()
    {
        Library = 1; Shelf = 1; Series = 1;
        Collection = 1; Volume = 1; Book = 1;
        Chapter = 1; Section = 1; Scroll = 1;
    }

    /// <summary>
    /// Creates a coordinate with specified values
    /// </summary>
    public Coordinate(int library, int shelf, int series,
                      int collection, int volume, int book,
                      int chapter, int section, int scroll)
    {
        Library = library; Shelf = shelf; Series = series;
        Collection = collection; Volume = volume; Book = book;
        Chapter = chapter; Section = section; Scroll = scroll;
    }

    /// <summary>
    /// Creates a deep copy of another coordinate
    /// </summary>
    public Coordinate(Coordinate other)
    {
        Library = other.Library; Shelf = other.Shelf; Series = other.Series;
        Collection = other.Collection; Volume = other.Volume; Book = other.Book;
        Chapter = other.Chapter; Section = other.Section; Scroll = other.Scroll;
    }

    /// <summary>
    /// Parses a coordinate from string format (e.g., "1.2.3/4.5.6/7.8.9")
    /// </summary>
    public static Coordinate FromString(string address)
    {
        var result = new Coordinate();
        if (string.IsNullOrEmpty(address)) return result;

        int index = 0;
        int value = 0;

        foreach (char c in address)
        {
            if (c == '.' || c == '/' || c == ';')
            {
                switch (index)
                {
                    case 1: result.Library = value; index++; break;
                    case 2: result.Shelf = value; index++; break;
                    case 3: result.Series = value; index++; break;
                    case 4: result.Collection = value; index++; break;
                    case 5: result.Volume = value; index++; break;
                    case 6: result.Book = value; index++; break;
                    case 7: result.Chapter = value; index++; break;
                    case 8: result.Section = value; index++; break;
                }
                value = 0;
            }

            if (c >= '0' && c <= '9')
            {
                value = 10 * value + (c - '0');
                if (index == 0) index = 1;
            }
        }

        if (index > 0)
        {
            result.Scroll = value;
        }

        return result;
    }

    /// <summary>
    /// Validates that all dimension indices are within bounds
    /// </summary>
    public bool IsValid()
    {
        return ValidateDimension(Library) && ValidateDimension(Shelf) && ValidateDimension(Series) &&
               ValidateDimension(Collection) && ValidateDimension(Volume) && ValidateDimension(Book) &&
               ValidateDimension(Chapter) && ValidateDimension(Section) && ValidateDimension(Scroll);
    }

    private static bool ValidateDimension(int index) =>
        index >= COORDINATE_MINIMUM && index <= COORDINATE_MAXIMUM;

    private static int AdvanceCoordinate(int index) =>
        index + 1 < COORDINATE_MAXIMUM ? index + 1 : index;

    // Dimension break methods - advance to next unit and reset lower dimensions
    public void LibraryBreak()
    {
        Library = AdvanceCoordinate(Library);
        Shelf = 1; Series = 1;
        Collection = 1; Volume = 1; Book = 1;
        Chapter = 1; Section = 1; Scroll = 1;
    }

    public void ShelfBreak()
    {
        Shelf = AdvanceCoordinate(Shelf);
        Series = 1;
        Collection = 1; Volume = 1; Book = 1;
        Chapter = 1; Section = 1; Scroll = 1;
    }

    public void SeriesBreak()
    {
        Series = AdvanceCoordinate(Series);
        Collection = 1; Volume = 1; Book = 1;
        Chapter = 1; Section = 1; Scroll = 1;
    }

    public void CollectionBreak()
    {
        Collection = AdvanceCoordinate(Collection);
        Volume = 1; Book = 1;
        Chapter = 1; Section = 1; Scroll = 1;
    }

    public void VolumeBreak()
    {
        Volume = AdvanceCoordinate(Volume);
        Book = 1;
        Chapter = 1; Section = 1; Scroll = 1;
    }

    public void BookBreak()
    {
        Book = AdvanceCoordinate(Book);
        Chapter = 1; Section = 1; Scroll = 1;
    }

    public void ChapterBreak()
    {
        Chapter = AdvanceCoordinate(Chapter);
        Section = 1; Scroll = 1;
    }

    public void SectionBreak()
    {
        Section = AdvanceCoordinate(Section);
        Scroll = 1;
    }

    public void ScrollBreak()
    {
        Scroll = AdvanceCoordinate(Scroll);
    }

    /// <summary>
    /// Copies values from another coordinate
    /// </summary>
    public void CopyFrom(Coordinate other)
    {
        Library = other.Library; Shelf = other.Shelf; Series = other.Series;
        Collection = other.Collection; Volume = other.Volume; Book = other.Book;
        Chapter = other.Chapter; Section = other.Section; Scroll = other.Scroll;
    }

    /// <summary>
    /// Returns canonical format: z3.z2.z1/y3.y2.y1/x3.x2.x1
    /// </summary>
    public override string ToString()
    {
        if (!IsValid()) return "";
        return $"{Library}.{Shelf}.{Series}/{Collection}.{Volume}.{Book}/{Chapter}.{Section}.{Scroll}";
    }

    /// <summary>
    /// Returns URL-safe format with semicolons: z3.z2.z1;y3.y2.y1;x3.x2.x1
    /// </summary>
    public string ToUrlEncoded()
    {
        if (!IsValid()) return "";
        return $"{Library}.{Shelf}.{Series};{Collection}.{Volume}.{Book};{Chapter}.{Section}.{Scroll}";
    }

    /// <summary>
    /// Returns a tuple representation for comparison
    /// </summary>
    public (int, int, int, int, int, int, int, int, int) AsTuple() =>
        (Library, Shelf, Series, Collection, Volume, Book, Chapter, Section, Scroll);

    public int CompareTo(Coordinate? other)
    {
        if (other is null) return 1;
        return AsTuple().CompareTo(other.AsTuple());
    }

    public bool Equals(Coordinate? other)
    {
        if (other is null) return false;
        return AsTuple() == other.AsTuple();
    }

    public override bool Equals(object? obj) => Equals(obj as Coordinate);

    public override int GetHashCode() => AsTuple().GetHashCode();

    public static bool operator ==(Coordinate? left, Coordinate? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(Coordinate? left, Coordinate? right) => !(left == right);

    public static bool operator <(Coordinate left, Coordinate right) => left.CompareTo(right) < 0;
    public static bool operator >(Coordinate left, Coordinate right) => left.CompareTo(right) > 0;
    public static bool operator <=(Coordinate left, Coordinate right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Coordinate left, Coordinate right) => left.CompareTo(right) >= 0;
}
