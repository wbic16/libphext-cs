# libphext-cs: The Fourth Root

🌳 **The Fourth Root** - A C# implementation of phext (11-dimensional plain hypertext)

This is the C# port of the phext library, tracking the canonical Rust implementation at https://github.com/wbic16/libphext-rs

## The Roots of Phext

| Root | Language | Repository |
|------|----------|------------|
| 🦀 **The Iron Root** | Rust | [libphext-rs](https://github.com/wbic16/libphext-rs) |
| 🌿 **The Branch of Breath** | Node.js | [libphext-node](https://github.com/wbic16/libphext-node) |
| 🐍 **The Tertiary Branch** | Python | [libphext-py](https://github.com/wbic16/libphext-py) |
| 💎 **The Fourth Root** | C# | libphext-cs (this repo) |

## What is Phext?

Phext is **11-dimensional plain hypertext** - a hierarchical text format that extends traditional 2D text files into 9 additional dimensions using ASCII control codes as dimension breaks.

### The Coordinate System

Phext coordinates are 9-dimensional addresses of the form:

```
library.shelf.series/collection.volume.book/chapter.section.scroll
```

For example: `5.5.5/4.6.7/9.5.9`

### Dimension Breaks (ASCII Control Codes)

| Dimension | Name | ASCII Code | Description |
|-----------|------|------------|-------------|
| 2 | Line | 0x0A | Standard newline |
| 3 | Scroll | 0x17 | End Transmission Block |
| 4 | Section | 0x18 | Cancel Block |
| 5 | Chapter | 0x19 | End of Tape |
| 6 | Book | 0x1A | Substitute |
| 7 | Volume | 0x1C | File Separator |
| 8 | Collection | 0x1D | Group Separator |
| 9 | Series | 0x1E | Record Separator |
| 10 | Shelf | 0x1F | Unit Separator |
| 11 | Library | 0x01 | Start of Header |

## Getting Started

### Installation

Add the library to your project:

```bash
dotnet add package libphext
```

Or clone and build from source:

```bash
git clone https://github.com/wbic16/libphext-cs
cd libphext-cs
dotnet build
```

### Usage

```csharp
using Phext;

// Create coordinates
var coord = Coordinate.FromString("1.1.1/1.1.1/1.1.1");
var coord2 = new Coordinate(1, 2, 3, 4, 5, 6, 7, 8, 9);

// Work with phext documents
string phext = "Hello\x17World\x18More content";

// Fetch content at a coordinate
string scroll = PhextEngine.Fetch(phext, coord);

// Insert content
string updated = PhextEngine.Insert(phext, coord2, "New scroll");

// Replace content
string replaced = PhextEngine.Replace(phext, coord, "Replaced content");

// Explode into dictionary
var scrolls = PhextEngine.Explode(phext);

// Merge two phexts
string merged = PhextEngine.Merge(phext1, phext2);

// Normalize (remove empty scrolls)
string normalized = PhextEngine.Normalize(phext);

// Expand/contract dimensions
string expanded = PhextEngine.Expand(phext);  // line -> scroll -> section...
string contracted = PhextEngine.Contract(phext);  // library -> shelf -> series...
```

## API Reference

### Core Types

- **`Coordinate`**: 9-dimensional address (library.shelf.series/collection.volume.book/chapter.section.scroll)
- **`PositionedScroll`**: A scroll of text with its coordinate location
- **`Range`**: A pair of coordinates defining a subspace region
- **`SubspaceBeacon`**: Helper for subspace navigation (start, end offsets + best match)

### PhextEngine Methods

| Method | Description |
|--------|-------------|
| `Fetch(phext, coord)` | Get scroll text at coordinate |
| `Insert(phext, coord, text)` | Insert text at end of scroll |
| `Replace(phext, coord, text)` | Replace scroll content |
| `Remove(phext, coord)` | Remove scroll at coordinate |
| `RangeReplace(phext, range, text)` | Replace range of scrolls |
| `Phokenize(phext)` | Split into positioned scrolls |
| `Dephokenize(scrolls)` | Serialize positioned scrolls |
| `Explode(phext)` | Convert to Dictionary<Coordinate, string> |
| `Implode(dict)` | Convert dictionary back to phext |
| `Merge(left, right)` | Zipper-merge two phexts |
| `Subtract(left, right)` | Remove matching coordinates |
| `Normalize(phext)` | Remove empty scrolls |
| `Expand(phext)` | Increase dimension breaks by one |
| `Contract(phext)` | Decrease dimension breaks by one |
| `Checksum(phext)` | Compute content hash |
| `Manifest(phext)` | Hierarchical checksums |
| `Textmap(phext)` | Text navigation map |
| `Navmap(urlbase, phext)` | HTML navigation map |
| `Offset(phext, coord)` | Get byte offset of scroll |
| `Index(phext)` | Get offset map as phext |

## Testing

```bash
cd libphext-cs
dotnet test
```

The test suite includes comprehensive tests ported from libphext-rs:
- Coordinate parsing and validation
- All 9 dimension break types
- Insert, replace, remove operations
- Range operations
- Merge and subtract
- Expand and contract
- Dead reckoning navigation
- Phokenize and dephokenize
- Performance tests

## Project Structure

```
libphext-cs/
├── libphext-cs.sln
├── README.md
├── src/
│   └── Phext/
│       ├── Phext.csproj
│       ├── Coordinate.cs
│       ├── PositionedScroll.cs
│       ├── Range.cs
│       ├── SubspaceBeacon.cs
│       └── PhextEngine.cs
└── tests/
    ├── Phext.Tests.csproj
    └── PhextTests.cs
```

## Why Phext?

In the 1980s, computers could write 25 KB/sec to a floppy disk. In the 2020s, we write 2 GB/sec to SSDs. This changed the definition of a "small" file, but our text abstractions haven't scaled.

Phext enables:
- **Planet-scale text** - Navigate petabytes with coordinate addresses
- **Hierarchical organization** - 9 dimensions of natural structure
- **AI-native format** - Perfect for LLM context and multi-agent coordination
- **Human-readable** - Still plain text, just with dimension breaks

## License

MIT License - Copyright (c) 2026 Will Bickford (Phext, Inc.)

## Links

- 🌐 [phext.io](https://phext.io) - Official website
- 📖 [Phext Specification](https://phext.io/api.php?seed=raap&token=research&coordinate=1.1.1/1.1.1/1.1.1)
- 🦀 [Rust Reference Implementation](https://github.com/wbic16/libphext-rs)
