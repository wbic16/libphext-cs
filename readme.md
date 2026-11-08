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

Coordinate components are unbounded (as of 0.4.0; releases through 0.3.x saturated components at 999).

> **Dense graphs:** phext is sparse by design. Coordinate components are unbounded, but a fully-populated dense graph contains the product of its dimensions' extents in scrolls — a dense block spanning 1000 in each of 9 dimensions implies 10^27 scrolls. Materializing, exploding, or indexing such a graph costs memory proportional to its populated scroll count. The old 999 cap never protected against this (999^9 was already unreachable); sparsity does. Keep graphs sparse.

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

## Binary Scrolls

Phext scrolls are text (valid UTF-8). Arbitrary binary data rides as text via base64, in one canonical format that every phext implementation (Rust, Python, Node, C#, C, ...) must match byte-for-byte:

```text
phext-binary:1:base64
<base64 body>
```

* Line 1 is exactly `phext-binary:1:base64`, followed by a single line break (0x0A).
* The body is RFC 4648 base64: standard alphabet (`A-Z`, `a-z`, `0-9`, `+`, `/`), padding (`=`) required, no line wrapping, no whitespace.
* An empty payload is the header line alone.
* A scroll whose content begins with the header line is a binary scroll by definition — the format claims that namespace. A prefix-matching scroll whose body is not strict base64 is a *malformed* binary scroll; `FetchBinary` returns its bytes verbatim rather than failing.

API (static class `BinaryScroll` in the core `Phext` assembly): `InsertBinary` / `ReplaceBinary` store a payload as a binary scroll unconditionally; `FetchBinary` returns the bytes at a coordinate (the decoded payload for a binary scroll, the scroll's own UTF-8 bytes otherwise, empty for an unoccupied coordinate). `EncodeScroll` / `DecodeScroll` / `IsBinaryScroll` expose the format itself.

### The Ingestion Rule (canonical behaviour)

> When non-UTF-8 data is observed in an incoming scroll update, we encode it with base64 verbatim.

Byte-oriented entry points — `BinaryScroll.InsertBytes`, `BinaryScroll.ReplaceBytes`, and `BinaryScroll.Ingest` for whole externally supplied phext byte streams — never reject and never throw on non-UTF-8 scroll data. Valid UTF-8 is stored as ordinary text; anything else is stored as a binary scroll holding the base64 of the exact observed bytes, verbatim, with no normalisation, so the matching fetch returns the original bytes exactly. Within a phext byte stream the nine dimension-break bytes are always structural; binary payloads that may contain them enter through the update functions, armoured as base64. `BinaryScroll.PhokenizeBytes` is the byte-level twin of `Phokenize`, returning `PositionedByteScroll` pairs.

## The XZ Seal

Compression is a seal, not a working format. A phext is authored, edited, and queried as plain text; when a phext is finished — archived, published, or transported — it may be sealed: xz-compressed as a whole. `PhextSeal.Seal` produces a standard xz stream (liblzma) over the phext's exact bytes, encoded at preset 6 with the CRC64 check (the preset is an encoder choice, not a format requirement; any valid xz stream unseals). `PhextSeal.Unseal` restores the exact original bytes, and sealing is deterministic for a given encoder and preset. To unseal foreign content that may carry non-UTF-8 scrolls, unseal to bytes (`Unseal`) and pass them through `BinaryScroll.Ingest`.

The seal lives in the companion assembly **`Phext.Seal`** (namespace `Phext.Seal`, static class `PhextSeal`), because .NET has no built-in xz support: it depends on the Joveler.Compression.XZ package, which bundles the native liblzma for Windows, Linux, and macOS. The core `Phext` assembly stays dependency-free — the same split as libphext-rs gating these capabilities behind its `binary` / `xz` features (binary support above is in the core assembly here, as it needs no dependency).

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
- Differential tests against the original phokenize (linear-time regression)

## Benchmarking

```bash
dotnet run -c Release --project bench/Phext.Bench -- path/to/file.phext [coordinate] [--out dir]
```

Times `Explode`, `Implode`, `Textmap`, `Index` and `Fetch` on one document. `--out` writes each result to a
directory so two builds can be compared byte for byte. The benchmark is not part of the package.

## Development and Release

**Prerequisites (read first):**

1. The **.NET SDK 8.0 or later** must be installed and `dotnet` on your PATH. The library and tests target `net8.0`. There is no way to build or test this repo without the SDK.
2. Note: the 0.4.0 changes in this repo were prepared on a machine with **no .NET toolchain**, so their compilation is **still unverified** — the first `dotnet test` run on a machine with the SDK is the real check. Treat a green run there as the verification, and please report any failure as a 0.4.0 regression.

**Test:**

1. Run `scripts/test.sh`
2. That runs exactly: `dotnet test`

**Build:**

1. Run `scripts/build.sh`
2. That runs exactly: `dotnet build -c Release`

**Publish (NuGet):**

1. Set your nuget.org API key in the environment: `export NUGET_API_KEY=<your key>` (never put the key in a file in this repo).
2. Run `scripts/publish.sh` — it runs the tests, then `dotnet pack -c Release`, prints exactly what will be published (package `libphext-cs`, version from `src/Phext/Phext.csproj`), and **stops**.
3. Only when that output looks right, run `PUBLISH_CONFIRM=1 scripts/publish.sh` — only then does it run `dotnet nuget push` to nuget.org.

## Project Structure

```
libphext-cs/
├── libphext-cs.sln
├── README.md
├── src/
│   ├── Phext/
│   │   ├── Phext.csproj
│   │   ├── Coordinate.cs
│   │   ├── PositionedScroll.cs
│   │   ├── PositionedByteScroll.cs
│   │   ├── BinaryScroll.cs
│   │   ├── Range.cs
│   │   ├── SubspaceBeacon.cs
│   │   └── PhextEngine.cs
│   └── Phext.Seal/
│       ├── Phext.Seal.csproj
│       └── PhextSeal.cs
├── tests/
│   ├── Phext.Tests.csproj
│   ├── PhextTests.cs
│   ├── PhokenizeTests.cs
│   ├── BinaryTests.cs
│   └── SealTests.cs
└── bench/
    └── Phext.Bench/
        ├── Phext.Bench.csproj
        └── Program.cs
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
