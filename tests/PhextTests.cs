// ===========================================================================================================
// libphext-cs: The Fourth Root - Unit Tests
// Ported from libphext-rs test suite
//
// Copyright: (c) 2024-2025 Will Bickford (Phext, Inc.)
// License: MIT
// ===========================================================================================================

using Xunit;
using Phext;

namespace Phext.Tests;

public class Constants
{
    public const char SB = PhextConstants.SCROLL_BREAK;
    public const char SN = PhextConstants.SECTION_BREAK;
    public const char CH = PhextConstants.CHAPTER_BREAK;
    public const char BK = PhextConstants.BOOK_BREAK;
    public const char VM = PhextConstants.VOLUME_BREAK;
    public const char CN = PhextConstants.COLLECTION_BREAK;
    public const char SR = PhextConstants.SERIES_BREAK;
    public const char SF = PhextConstants.SHELF_BREAK;
    public const char LB = PhextConstants.LIBRARY_BREAK;
}

public class CoordinateTests : Constants
{
    [Fact]
    public void Test_CoordinateParsing()
    {
        string exampleCoordinate = "9.8.7/6.5.4/3.2.1";
        var test = Coordinate.FromString(exampleCoordinate);
        string address = test.ToString();
        Assert.Equal(exampleCoordinate, address);

        string weirdCoordinate = "HOME";
        var testWeird = Coordinate.FromString(weirdCoordinate);
        Assert.Equal("1.1.1/1.1.1/1.1.1", testWeird.ToString());
    }

    [Fact]
    public void Test_ToUrlEncoded()
    {
        var sample1 = Coordinate.FromString("1.1.1/1.1.1/1.1.1");
        Assert.Equal("1.1.1;1.1.1;1.1.1", sample1.ToUrlEncoded());

        var sample2 = Coordinate.FromString("98.76.54/32.10.1/23.45.67");
        Assert.Equal("98.76.54;32.10.1;23.45.67", sample2.ToUrlEncoded());
    }

    [Fact]
    public void Test_CoordinatesInvalid()
    {
        var c1 = Coordinate.FromString("0.0.0/0.0.0/0.0.0");
        var c2 = new Coordinate(0, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.Equal(c1, c2);
        Assert.False(c1.IsValid());
        Assert.False(c2.IsValid());
    }

    [Fact]
    public void Test_CoordinatesValid()
    {
        var c1 = Coordinate.FromString("142.143.144/145.146.147/148.149.150");
        Assert.True(c1.IsValid());

        var c2 = Coordinate.FromString("1001.1002.1003/1004.1005.1006/1007.1008.1009");
        Assert.False(c2.IsValid()); // exceeds max of 1000
    }

    [Fact]
    public void Test_UrlEncoding()
    {
        var c1 = Coordinate.FromString("142.143.144;145.146.147;148.149.150");
        var c2 = new Coordinate(142, 143, 144, 145, 146, 147, 148, 149, 150);
        Assert.Equal(c1, c2);
        Assert.True(c1.IsValid());
        Assert.True(c2.IsValid());
    }

    [Fact]
    public void Test_CoordinateComparison()
    {
        var c1 = Coordinate.FromString("1.1.1/1.1.1/1.1.1");
        var c2 = Coordinate.FromString("1.1.1/1.1.1/1.1.2");
        var c3 = Coordinate.FromString("2.1.1/1.1.1/1.1.1");

        Assert.True(c1 < c2);
        Assert.True(c2 < c3);
        Assert.True(c1 < c3);
        Assert.False(c2 < c1);
    }
}

public class PhextBreakTests : Constants
{
    [Fact]
    public void Test_LineBreak()
    {
        Assert.Equal('\n', PhextConstants.LINE_BREAK);
    }

    [Fact]
    public void Test_MoreCowbell()
    {
        var test1 = PhextEngine.CheckForCowbell("Hello\x07");
        var test2 = PhextEngine.CheckForCowbell("nope\x17just more scrolls");
        Assert.Equal('\x07', PhextConstants.MORE_COWBELL);
        Assert.True(test1);
        Assert.False(test2);
    }

    [Fact]
    public void Test_Scrolls()
    {
        string sample = "Hello World\x17Scroll #2 -- this text will be selected\x17Scroll #3 - this text will be ignored";

        var coord1 = Coordinate.FromString("1.1.1/1.1.1/1.1.1");
        Assert.Equal("Hello World", PhextEngine.Fetch(sample, coord1));

        var coord2 = Coordinate.FromString("1.1.1/1.1.1/1.1.2");
        Assert.Equal("Scroll #2 -- this text will be selected", PhextEngine.Fetch(sample, coord2));

        var coord3 = Coordinate.FromString("1.1.1/1.1.1/1.1.3");
        Assert.Equal("Scroll #3 - this text will be ignored", PhextEngine.Fetch(sample, coord3));
    }

    [Fact]
    public void Test_Sections()
    {
        string sample = "Section A\x18Section B\x18Section C";

        var coord1 = Coordinate.FromString("1.1.1/1.1.1/1.1.1");
        Assert.Equal("Section A", PhextEngine.Fetch(sample, coord1));

        var coord2 = Coordinate.FromString("1.1.1/1.1.1/1.2.1");
        Assert.Equal("Section B", PhextEngine.Fetch(sample, coord2));

        var coord3 = Coordinate.FromString("1.1.1/1.1.1/1.3.1");
        Assert.Equal("Section C", PhextEngine.Fetch(sample, coord3));
    }

    [Fact]
    public void Test_Chapters()
    {
        string sample = "Chapter Alpha" + CH + "Chapter Beta" + CH + "Chapter Gamma";

        var coord1 = Coordinate.FromString("1.1.1/1.1.1/1.1.1");
        Assert.Equal("Chapter Alpha", PhextEngine.Fetch(sample, coord1));

        var coord2 = Coordinate.FromString("1.1.1/1.1.1/2.1.1");
        Assert.Equal("Chapter Beta", PhextEngine.Fetch(sample, coord2));

        var coord3 = Coordinate.FromString("1.1.1/1.1.1/3.1.1");
        Assert.Equal("Chapter Gamma", PhextEngine.Fetch(sample, coord3));
    }

    [Fact]
    public void Test_Books()
    {
        string sample = "Book z1" + BK + "Book Something Else #2" + BK + "Book Part 3";

        var coord1 = Coordinate.FromString("1.1.1/1.1.1/1.1.1");
        Assert.Equal("Book z1", PhextEngine.Fetch(sample, coord1));

        var coord2 = Coordinate.FromString("1.1.1/1.1.2/1.1.1");
        Assert.Equal("Book Something Else #2", PhextEngine.Fetch(sample, coord2));

        var coord3 = Coordinate.FromString("1.1.1/1.1.3/1.1.1");
        Assert.Equal("Book Part 3", PhextEngine.Fetch(sample, coord3));
    }

    [Fact]
    public void Test_Volumes()
    {
        string sample = "Volume 1-1-1\x1CVolume 1-2-1\x1CVolume 1-3-1";

        var coord1 = Coordinate.FromString("1.1.1/1.1.1/1.1.1");
        Assert.Equal("Volume 1-1-1", PhextEngine.Fetch(sample, coord1));

        var coord2 = Coordinate.FromString("1.1.1/1.2.1/1.1.1");
        Assert.Equal("Volume 1-2-1", PhextEngine.Fetch(sample, coord2));

        var coord3 = Coordinate.FromString("1.1.1/1.3.1/1.1.1");
        Assert.Equal("Volume 1-3-1", PhextEngine.Fetch(sample, coord3));
    }

    [Fact]
    public void Test_Collections()
    {
        string sample = "Collection 1-1-1" + CN + "Collection 2-1-1" + CN + "Collection 3-1-1";

        var coord1 = Coordinate.FromString("1.1.1/1.1.1/1.1.1");
        Assert.Equal("Collection 1-1-1", PhextEngine.Fetch(sample, coord1));

        var coord2 = Coordinate.FromString("1.1.1/2.1.1/1.1.1");
        Assert.Equal("Collection 2-1-1", PhextEngine.Fetch(sample, coord2));

        var coord3 = Coordinate.FromString("1.1.1/3.1.1/1.1.1");
        Assert.Equal("Collection 3-1-1", PhextEngine.Fetch(sample, coord3));
    }

    [Fact]
    public void Test_Series()
    {
        string sample = "Series 1-1-1\x1ESeries 1-1-2\x1ESeries 1-1-3";

        var coord1 = Coordinate.FromString("1.1.1/1.1.1/1.1.1");
        Assert.Equal("Series 1-1-1", PhextEngine.Fetch(sample, coord1));

        var coord2 = Coordinate.FromString("1.1.2/1.1.1/1.1.1");
        Assert.Equal("Series 1-1-2", PhextEngine.Fetch(sample, coord2));

        var coord3 = Coordinate.FromString("1.1.3/1.1.1/1.1.1");
        Assert.Equal("Series 1-1-3", PhextEngine.Fetch(sample, coord3));
    }

    [Fact]
    public void Test_Shelves()
    {
        string sample = "Shelf 1-1-1\x1FShelf 1-2-1\x1FShelf 1-3-1";

        var coord1 = Coordinate.FromString("1.1.1/1.1.1/1.1.1");
        Assert.Equal("Shelf 1-1-1", PhextEngine.Fetch(sample, coord1));

        var coord2 = Coordinate.FromString("1.2.1/1.1.1/1.1.1");
        Assert.Equal("Shelf 1-2-1", PhextEngine.Fetch(sample, coord2));

        var coord3 = Coordinate.FromString("1.3.1/1.1.1/1.1.1");
        Assert.Equal("Shelf 1-3-1", PhextEngine.Fetch(sample, coord3));
    }

    [Fact]
    public void Test_Libraries()
    {
        string sample = "Library 1-1-1\x01Library 2-1-1\x01Library 3-1-1";

        var coord1 = Coordinate.FromString("1.1.1/1.1.1/1.1.1");
        Assert.Equal("Library 1-1-1", PhextEngine.Fetch(sample, coord1));

        var coord2 = Coordinate.FromString("2.1.1/1.1.1/1.1.1");
        Assert.Equal("Library 2-1-1", PhextEngine.Fetch(sample, coord2));

        var coord3 = Coordinate.FromString("3.1.1/1.1.1/1.1.1");
        Assert.Equal("Library 3-1-1", PhextEngine.Fetch(sample, coord3));
    }
}

public class PhextOperationTests
{
    const char SB = PhextConstants.SCROLL_BREAK;
    const char SN = PhextConstants.SECTION_BREAK;
    const char CH = PhextConstants.CHAPTER_BREAK;
    const char BK = PhextConstants.BOOK_BREAK;
    const char VM = PhextConstants.VOLUME_BREAK;
    const char CN = PhextConstants.COLLECTION_BREAK;
    const char SR = PhextConstants.SERIES_BREAK;
    const char SF = PhextConstants.SHELF_BREAK;
    const char LB = PhextConstants.LIBRARY_BREAK;

    [Fact]
    public void Test_CoordinateBasedInsert()
    {
        string test = "aaa" + LB + "bbb" + SB + "ccc";

        var coord1 = Coordinate.FromString("2.1.1/1.1.1/1.1.3");
        var update1 = PhextEngine.Insert(test, coord1, "ddd");
        Assert.Equal("aaa" + LB + "bbb" + SB + "ccc" + SB + "ddd", update1);

        var coord2 = Coordinate.FromString("2.1.1/1.1.1/1.1.4");
        var update2 = PhextEngine.Insert(update1, coord2, "eee");
        Assert.Equal("aaa" + LB + "bbb" + SB + "ccc" + SB + "ddd" + SB + "eee", update2);

        var coord3 = Coordinate.FromString("2.1.1/1.1.1/1.2.1");
        var update3 = PhextEngine.Insert(update2, coord3, "fff");
        Assert.Equal("aaa" + LB + "bbb" + SB + "ccc" + SB + "ddd" + SB + "eee" + SN + "fff", update3);

        var coord4 = Coordinate.FromString("2.1.1/1.1.1/1.2.2");
        var update4 = PhextEngine.Insert(update3, coord4, "ggg");
        Assert.Equal("aaa" + LB + "bbb" + SB + "ccc" + SB + "ddd" + SB + "eee" + SN + "fff" + SB + "ggg", update4);

        var coord5 = Coordinate.FromString("2.1.1/1.1.1/2.1.1");
        var update5 = PhextEngine.Insert(update4, coord5, "hhh");
        Assert.Equal("aaa" + LB + "bbb" + SB + "ccc" + SB + "ddd" + SB + "eee" + SN + "fff" + SB + "ggg" + CH + "hhh", update5);
    }

    [Fact]
    public void Test_CoordinateBasedReplace()
    {
        string test = "aaa" + SB + "bbb" + SN + "ccc" + CH + "ddd" + BK + "eee" + VM + "fff" + CN + "ggg" + SR + "hhh" + SF + "iii" + LB + "jjj";

        var coord1 = Coordinate.FromString("1.1.1/1.1.1/1.1.1");
        var update1 = PhextEngine.Replace(test, coord1, "AAA");
        Assert.Equal("AAA" + SB + "bbb" + SN + "ccc" + CH + "ddd" + BK + "eee" + VM + "fff" + CN + "ggg" + SR + "hhh" + SF + "iii" + LB + "jjj", update1);

        var coord2 = Coordinate.FromString("1.1.1/1.1.1/1.1.2");
        var update2 = PhextEngine.Replace(update1, coord2, "222");
        Assert.Equal("AAA" + SB + "222" + SN + "ccc" + CH + "ddd" + BK + "eee" + VM + "fff" + CN + "ggg" + SR + "hhh" + SF + "iii" + LB + "jjj", update2);
    }

    [Fact]
    public void Test_CoordinateBasedRemove()
    {
        string test = "aaa" + SB + "bbb" + SN + "ccc" + CH + "ddd" + BK + "eee" + VM + "fff" + CN + "ggg" + SR + "hhh" + SF + "iii" + LB + "jjj";

        var coord1 = Coordinate.FromString("1.1.1/1.1.1/1.1.1");
        var update1 = PhextEngine.Remove(test, coord1);
        Assert.Equal(SB + "bbb" + SN + "ccc" + CH + "ddd" + BK + "eee" + VM + "fff" + CN + "ggg" + SR + "hhh" + SF + "iii" + LB + "jjj", update1);

        var coord2 = Coordinate.FromString("1.1.1/1.1.1/1.1.2");
        var update2 = PhextEngine.Remove(update1, coord2);
        Assert.Equal(SN + "ccc" + CH + "ddd" + BK + "eee" + VM + "fff" + CN + "ggg" + SR + "hhh" + SF + "iii" + LB + "jjj", update2);
    }

    [Fact]
    public void Test_RangeBasedReplace()
    {
        string doc1 = "Before" + CH + "text to be replaced" + VM + "also this" + CN + "and this" + SB + "After";
        var range1 = new Range(
            Coordinate.FromString("1.1.1/1.1.1/2.1.1"),
            Coordinate.FromString("1.1.1/2.1.1/1.1.1")
        );
        var update1 = PhextEngine.RangeReplace(doc1, range1, "");
        Assert.Equal("Before" + CH + SB + "After", update1);

        string doc2 = "Before" + LB + "Library two" + LB + "Library three" + LB + "Library four";
        var range2 = new Range(
            Coordinate.FromString("2.1.1/1.1.1/1.1.1"),
            Coordinate.FromString("3.1.1/1.1.1/1.1.1")
        );
        var update2 = PhextEngine.RangeReplace(doc2, range2, "");
        Assert.Equal("Before" + LB + LB + "Library four", update2);
    }

    [Fact]
    public void Test_NextScroll()
    {
        string doc1 = "3A" + SB + "B2" + SN + "C1";
        var (scroll, nextStart, remaining) = PhextEngine.NextScroll(doc1, Coordinate.FromString("1.1.1/1.1.1/1.1.1"));
        Assert.Equal("1.1.1/1.1.1/1.1.1", scroll.Coord.ToString());
        Assert.Equal("3A", scroll.Scroll);
        Assert.Equal("1.1.1/1.1.1/1.1.2", nextStart.ToString());
        Assert.Equal("B2" + SN + "C1", remaining);
    }

    [Fact]
    public void Test_Phokenize()
    {
        string doc1 = "one" + SB + "two" + SB + "three" + SB + "four";
        var update1 = PhextEngine.Phokenize(doc1);

        Assert.Equal(4, update1.Count);
        Assert.Equal("1.1.1/1.1.1/1.1.1", update1[0].Coord.ToString());
        Assert.Equal("one", update1[0].Scroll);
        Assert.Equal("1.1.1/1.1.1/1.1.2", update1[1].Coord.ToString());
        Assert.Equal("two", update1[1].Scroll);
        Assert.Equal("1.1.1/1.1.1/1.1.3", update1[2].Coord.ToString());
        Assert.Equal("three", update1[2].Scroll);
        Assert.Equal("1.1.1/1.1.1/1.1.4", update1[3].Coord.ToString());
        Assert.Equal("four", update1[3].Scroll);

        string doc2 = "one" + LB + "two" + SF + "three" + SR + "four" + CN + "five" + VM + "six" + BK + "seven" + CH + "eight" + SN + "nine" + SB + "ten";
        var update2 = PhextEngine.Phokenize(doc2);

        Assert.Equal(10, update2.Count);
        Assert.Equal("1.1.1/1.1.1/1.1.1", update2[0].Coord.ToString());
        Assert.Equal("one", update2[0].Scroll);
        Assert.Equal("2.1.1/1.1.1/1.1.1", update2[1].Coord.ToString());
        Assert.Equal("two", update2[1].Scroll);
        Assert.Equal("2.2.1/1.1.1/1.1.1", update2[2].Coord.ToString());
        Assert.Equal("three", update2[2].Scroll);
    }

    [Fact]
    public void Test_PhokenizeAllDimensions()
    {
        string doc3 = "one" + SB + "two" + SN + "three" + CH + "four" + BK + "five" + VM + "six" + CN + "seven" + SR + "eight" + SF + "nine" + LB + "ten";
        var update3 = PhextEngine.Phokenize(doc3);

        Assert.Equal(10, update3.Count);
        Assert.Equal("1.1.1/1.1.1/1.1.1", update3[0].Coord.ToString());
        Assert.Equal("1.1.1/1.1.1/1.1.2", update3[1].Coord.ToString());
        Assert.Equal("1.1.1/1.1.1/1.2.1", update3[2].Coord.ToString());
        Assert.Equal("1.1.1/1.1.1/2.1.1", update3[3].Coord.ToString());
        Assert.Equal("1.1.1/1.1.2/1.1.1", update3[4].Coord.ToString());
        Assert.Equal("1.1.1/1.2.1/1.1.1", update3[5].Coord.ToString());
        Assert.Equal("1.1.1/2.1.1/1.1.1", update3[6].Coord.ToString());
        Assert.Equal("1.1.2/1.1.1/1.1.1", update3[7].Coord.ToString());
        Assert.Equal("1.2.1/1.1.1/1.1.1", update3[8].Coord.ToString());
        Assert.Equal("2.1.1/1.1.1/1.1.1", update3[9].Coord.ToString());
    }

    [Fact]
    public void Test_LastEmptyScroll()
    {
        string doc1 = "hello\x17world\x17";
        var target1 = Coordinate.FromString("1.1.1/1.1.1/1.1.2");
        var test1 = PhextEngine.Fetch(doc1, target1);
        Assert.Equal("world", test1);
    }

    [Fact]
    public void Test_Merge()
    {
        string doc1a = "3A" + SB + "B2";
        string doc1b = "4C" + SB + "D1";
        var update1 = PhextEngine.Merge(doc1a, doc1b);
        Assert.Equal("3A4C" + SB + "B2D1", update1);

        string doc2a = "Hello \x17I've come to talk";
        string doc2b = "Darkness, my old friend.\x17 with you again.";
        var update2 = PhextEngine.Merge(doc2a, doc2b);
        Assert.Equal("Hello Darkness, my old friend.\x17I've come to talk with you again.", update2);

        string doc3a = "One\x17Two\x18Three\x19ruoF"; // C# sucks at \x literals
        string doc3b = "1" + SB + "2" + SN + "3" + CH + "4";
        var update3 = PhextEngine.Merge(doc3a, doc3b);
        Assert.Equal("One1\x17Two2\x18Three3\x19ruoF4", update3);
    }

    [Fact]
    public void Test_Subtract()
    {
        string doc1a = "Here's scroll one.\x17Scroll two.";
        string doc1b = "Just content at the first scroll";
        var update1 = PhextEngine.Subtract(doc1a, doc1b);
        Assert.Equal("\x17Scroll two.", update1);
    }

    [Fact]
    public void Test_Normalize()
    {
        string doc1 = "\x17Scroll two\x18\x18\x18\x18";
        var update1 = PhextEngine.Normalize(doc1);
        Assert.Equal("\x17Scroll two", update1);
    }

    [Fact]
    public void Test_Expand()
    {
        string doc1 = "nothing but line breaks\x0Ato test expansion to scrolls\x0Aline 3";
        var update1 = PhextEngine.Expand(doc1);
        Assert.Equal("nothing but line breaks\x17to test expansion to scrolls\x17line 3", update1);

        var update2 = PhextEngine.Expand(update1);
        Assert.Equal("nothing but line breaks\x18to test expansion to scrolls\x18line 3", update2);

        var update3 = PhextEngine.Expand(update2);
        Assert.Equal("nothing but line breaks\x19to test expansion to scrolls\x19line 3", update3);

        var update4 = PhextEngine.Expand(update3);
        Assert.Equal("nothing but line breaks\x1Ato test expansion to scrolls\x1Aline 3", update4);

        var update5 = PhextEngine.Expand(update4);
        Assert.Equal("nothing but line breaks\x1Cto test expansion to scrolls\x1Cline 3", update5);

        var update6 = PhextEngine.Expand(update5);
        Assert.Equal("nothing but line breaks\x1Dto test expansion to scrolls\x1Dline 3", update6);

        var update7 = PhextEngine.Expand(update6);
        Assert.Equal("nothing but line breaks\x1Eto test expansion to scrolls\x1Eline 3", update7);

        var update8 = PhextEngine.Expand(update7);
        Assert.Equal("nothing but line breaks\x1Fto test expansion to scrolls\x1Fline 3", update8);

        var update9 = PhextEngine.Expand(update8);
        Assert.Equal("nothing but line breaks\x01to test expansion to scrolls\x01line 3", update9);
    }

    [Fact]
    public void Test_Contract()
    {
        string doc1 = "A more complex example than expand\x01----\x1F++++\x1E____\x1Doooo\x1C====\x1Azzzz\x19gggg\x18....\x17qqqq";
        var update1 = PhextEngine.Contract(doc1);
        Assert.Equal("A more complex example than expand\x1F----\x1E++++\x1D____\x1Coooo\x1A====\x19zzzz\x18gggg\x17....\x0Aqqqq", update1);

        var update2 = PhextEngine.Contract(update1);
        Assert.Equal("A more complex example than expand\x1E----\x1D++++\x1C____\x1Aoooo\x19====\x18zzzz\x17gggg\x0A....\x0Aqqqq", update2);
    }

    [Fact]
    public void Test_DeadReckoning()
    {
        string test = "";
        test += "random text in 1.1.1/1.1.1/1.1.1 that we can skip past";
        test += LB;
        test += "everything in here is at 2.1.1/1.1.1/1.1.1";
        test += SB;
        test += "and now we're at 2.1.1/1.1.1/1.1.2";
        test += SB;
        test += "moving on up to 2.1.1/1.1.1/1.1.3";
        test += BK;
        test += "and now over to 2.1.1/1.1.2/1.1.1";
        test += SF;
        test += "woot, up to 2.2.1/1.1.1/1.1.1";
        test += LB;
        test += "here we are at 3.1.1/1.1.1.1.1";
        test += LB; // 4.1.1/1.1.1/1.1.1
        test += LB; // 5.1.1/1.1.1/1.1.1
        test += "getting closer to our target now 5.1.1/1.1.1/1.1.1";
        test += SF; // 5.2.1
        test += SF; // 5.3.1
        test += SF; // 5.4.1
        test += SF; // 5.5.1
        test += SR; // 5.5.2
        test += SR; // 5.5.3
        test += SR; // 5.5.4
        test += SR; // 5.5.5
        test += "here we go! 5.5.5/1.1.1/1.1.1";
        test += CN; // 5.5.5/2.1.1/1.1.1
        test += CN; // 5.5.5/3.1.1/1.1.1
        test += CN; // 5.5.5/4.1.1/1.1.1
        test += BK; // 5.5.5/4.1.2/1.1.1
        test += BK; // 5.5.5/4.1.3/1.1.1
        test += BK; // 5.5.5/4.1.4/1.1.1
        test += "this test appears at 5.5.5/4.1.4/1.1.1";
        test += VM; // 5.5.5/4.2.1/1.1.1
        test += VM; // 5.5.5/4.3.1/1.1.1
        test += VM; // 5.5.5/4.4.1/1.1.1
        test += VM; // 5.5.5/4.5.1/1.1.1
        test += VM; // 5.5.5/4.6.1/1.1.1
        test += CH; // 5.5.5/4.6.1/2.1.1
        test += CH; // 5.5.5/4.6.1/3.1.1
        test += CH; // 5.5.5/4.6.1/4.1.1
        test += CH; // 5.5.5/4.6.1/5.1.1
        test += BK; // 5.5.5/4.6.2/1.1.1
        test += BK; // 5.5.5/4.6.3/1.1.1
        test += BK; // 5.5.5/4.6.4/1.1.1
        test += BK; // 5.5.5/4.6.5/1.1.1
        test += BK; // 5.5.5/4.6.6/1.1.1
        test += BK; // 5.5.5/4.6.7/1.1.1
        test += CH; // 5.5.5/4.6.7/2.1.1
        test += CH; // 5.5.5/4.6.7/3.1.1
        test += CH; // 5.5.5/4.6.7/4.1.1
        test += CH; // 5.5.5/4.6.7/5.1.1
        test += SB; // 5.5.5/4.6.7/5.1.2
        test += SB; // 5.5.5/4.6.7/5.1.3
        test += SB; // 5.5.5/4.6.7/5.1.4
        test += SB; // 5.5.5/4.6.7/5.1.5
        test += SB; // 5.5.5/4.6.7/5.1.6
        test += "here's a test at 5.5.5/4.6.7/5.1.6";
        test += SB; // 5.5.5/4.6.7/5.1.7
        test += CH; // 5.5.5/4.6.7/6.1.1
        test += CH; // 5.5.5/4.6.7/7.1.1
        test += CH; // 5.5.5/4.6.7/8.1.1
        test += CH; // 5.5.5/4.6.7/9.1.1
        test += SN; // 5.5.5/4.6.7/9.2.1
        test += SN; // 5.5.5/4.6.7/9.3.1
        test += SN; // 5.5.5/4.6.7/9.4.1
        test += SN; // 5.5.5/4.6.7/9.5.1
        test += SB;  // 5.5.5/4.6.7/9.5.2
        test += SB;  // 5.5.5/4.6.7/9.5.3
        test += SB;  // 5.5.5/4.6.7/9.5.4
        test += SB;  // 5.5.5/4.6.7/9.5.5
        test += SB;  // 5.5.5/4.6.7/9.5.6
        test += SB;  // 5.5.5/4.6.7/9.5.7
        test += SB;  // 5.5.5/4.6.7/9.5.8
        test += SB;  // 5.5.5/4.6.7/9.5.9
        test += "Expected Test Pattern Alpha Whisky Tango Foxtrot";

        var coord = Coordinate.FromString("5.5.5/4.6.7/9.5.9");
        var result = PhextEngine.Fetch(test, coord);
        Assert.Equal("Expected Test Pattern Alpha Whisky Tango Foxtrot", result);

        var coord2 = Coordinate.FromString("5.5.5/4.6.7/5.1.6");
        var result2 = PhextEngine.Fetch(test, coord2);
        Assert.Equal("here's a test at 5.5.5/4.6.7/5.1.6", result2);
    }

    [Fact]
    public void Test_ExplodeImplode()
    {
        string original = "one\x17two\x18three\x19four";
        var exploded = PhextEngine.Explode(original);
        var imploded = PhextEngine.Implode(exploded);
        Assert.Equal(original, imploded);
    }

    [Fact]
    public void Test_Offset()
    {
        string example = "first scroll\x17second scroll\x18second section\x19second chapter\x1Abook 2\x1Cvolume 2\x1Dcollection 2\x1Eseries 2\x1Fshelf 2\x01library 2";

        var coord1 = Coordinate.FromString("1.1.1/1.1.1/1.1.1");
        Assert.Equal(0, PhextEngine.Offset(example, coord1));

        var coord2 = Coordinate.FromString("1.1.1/1.1.1/1.1.2");
        Assert.Equal(13, PhextEngine.Offset(example, coord2));
    }

    [Fact]
    public void Test_SoundexV1()
    {
        string sample = "it was the best of scrolls" + SB + "it was the worst of scrolls" + SB + "aaa" + SB + "bbb" + SB + "ccc" + SB + "ddd" + SB + "eee" + SB + "fff" + SB + "ggg" + SB + "hhh" + SB + "iii" + SB + "jjj" + SB + "kkk" + SB + "lll" + SN + "mmm" + SN + "nnn" + SN + "ooo" + SN + "ppp" + CH + "qqq" + CH + "rrr" + CH + "sss" + CH + "ttt" + BK + "uuu" + BK + "vvv" + BK + "www" + BK + "xxx" + BK + "yyy" + BK + "zzz";
        var result = PhextEngine.SoundexV1(sample);
        Assert.Equal("36" + SB + "41" + SB + "1" + SB + "4" + SB + "7" + SB + "10" + SB + "1" + SB + "4" + SB + "7" + SB + "1" + SB + "1" + SB + "7" + SB + "7" + SB + "13" + SN + "16" + SN + "16" + SN + "1" + SN + "4" + CH + "7" + CH + "19" + CH + "7" + CH + "10" + BK + "1" + BK + "4" + BK + "1" + BK + "7" + BK + "1" + BK + "7", result);
    }
}

public class PositionedScrollTests
{
    [Fact]
    public void Test_Creation()
    {
        var coord = Coordinate.FromString("1.2.3/4.5.6/7.8.9");
        var ps = new PositionedScroll(coord, "Hello World");

        Assert.Equal(coord, ps.Coord);
        Assert.Equal("Hello World", ps.Scroll);
    }

    [Fact]
    public void Test_Comparison()
    {
        var ps1 = new PositionedScroll(Coordinate.FromString("1.1.1/1.1.1/1.1.1"), "first");
        var ps2 = new PositionedScroll(Coordinate.FromString("1.1.1/1.1.1/1.1.2"), "second");
        var ps3 = new PositionedScroll(Coordinate.FromString("2.1.1/1.1.1/1.1.1"), "third");

        Assert.True(ps1.CompareTo(ps2) < 0);
        Assert.True(ps2.CompareTo(ps3) < 0);
        Assert.True(ps1.CompareTo(ps3) < 0);
    }
}

public class RangeTests
{
    [Fact]
    public void Test_Creation()
    {
        var range = new Range("1.1.1/1.1.1/1.1.1", "2.2.2/2.2.2/2.2.2");

        Assert.Equal("1.1.1/1.1.1/1.1.1", range.Start.ToString());
        Assert.Equal("2.2.2/2.2.2/2.2.2", range.End.ToString());
    }

    [Fact]
    public void Test_ToString()
    {
        var range = new Range(
            Coordinate.FromString("1.1.1/1.1.1/1.1.1"),
            Coordinate.FromString("2.2.2/2.2.2/2.2.2")
        );

        Assert.Equal("1.1.1/1.1.1/1.1.1-2.2.2/2.2.2/2.2.2", range.ToString());
    }
}
