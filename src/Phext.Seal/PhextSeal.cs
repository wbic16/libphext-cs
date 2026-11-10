// ===========================================================================================================
// libphext-cs: The Fourth Root
// PhextSeal.cs - The xz seal: canonical compression for a finished phext
//
// Ported from: libphext-rs src/seal.rs (0.5.0, feature: xz)
// Copyright: (c) 2024-2025 Will Bickford (Phext, Inc.)
// License: MIT
//
// Compression is a seal, not a working format. A phext is authored, edited, and queried
// as plain text; when a phext is finished - archived, published, or transported - it may
// be sealed: xz-compressed as a whole. Unsealing restores the exact original bytes.
//
// Seal specification:
// * The seal is a standard xz stream (liblzma) over the phext's exact bytes. Decoders in
//   any implementation must accept any valid xz stream.
// * This implementation encodes at preset 6 with the CRC64 stream check, matching
//   libphext-rs (xz2 defaults). The preset is an encoder choice, not a format
//   requirement; seals produced at other presets unseal identically.
// * Sealing is deterministic for a given encoder and preset: the same phext bytes seal
//   to the same sealed bytes.
//
// This surface lives in its own assembly because .NET has no built-in xz support: the
// seal needs liblzma, supplied by the Joveler.Compression.XZ package (which bundles the
// native library for Windows, Linux, and macOS). The core Phext assembly stays
// dependency-free - this mirrors how libphext-rs gates the seal behind its `xz` feature.
// ===========================================================================================================

using System.Runtime.InteropServices;
using System.Text;
using Joveler.Compression.XZ;

namespace Phext.Seal;

public static class PhextSeal
{
    /// <summary>The xz preset this implementation seals with.</summary>
    public const int SealPreset = 6;

    private static readonly object InitLock = new();
    private static bool _initialized;

    private static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (InitLock)
        {
            if (_initialized)
            {
                return;
            }

            // Joveler's parameterless GlobalInit() cannot find liblzma on
            // Windows: its loader only knows the system library names for
            // Linux (liblzma.so.5) and macOS (liblzma.dylib) and throws
            // PlatformNotSupportedException on Windows. Windows callers must
            // pass the bundled native library path explicitly (the same
            // construction Joveler's own USAGE.md prescribes), so resolve it
            // on every platform and only fall back to the parameterless
            // system search when no bundled copy exists.
            string? libPath = ResolveNativeLibraryPath();
            if (libPath != null)
            {
                XZInit.GlobalInit(libPath);
            }
            else
            {
                XZInit.GlobalInit();
            }

            // Latch success only. If GlobalInit throws, _initialized stays
            // false and the next call retries (surfacing the real error),
            // instead of every later call skipping init and dying inside
            // XZStream with the misleading "Please call XZInit.GlobalInit()
            // first!" message.
            _initialized = true;
        }
    }

    /// <summary>
    /// Finds the liblzma binary bundled with Joveler.Compression.XZ under
    /// the output directory's runtimes/&lt;rid&gt;/native folder (or flattened
    /// beside the assembly). Returns null when no bundled copy is found;
    /// on Windows, where there is no system-library fallback, that is a
    /// hard error with the searched location in the message.
    /// </summary>
    private static string? ResolveNativeLibraryPath()
    {
        string platform;
        string fileName;
        bool windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            platform = "win";
            fileName = "liblzma.dll";
            windows = true;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            platform = "linux";
            fileName = "liblzma.so";
            windows = false;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            platform = "osx";
            fileName = "liblzma.dylib";
            windows = false;
        }
        else
        {
            return null;
        }

        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => "x86",
            Architecture.X64 => "x64",
            Architecture.Arm => "arm",
            Architecture.Arm64 => "arm64",
            _ => "x64",
        };
        string rid = $"{platform}-{arch}";

        var bases = new List<string> { AppContext.BaseDirectory };
        string? assemblyDir = Path.GetDirectoryName(typeof(PhextSeal).Assembly.Location);
        if (!string.IsNullOrEmpty(assemblyDir) && !bases.Contains(assemblyDir))
        {
            bases.Add(assemblyDir);
        }

        foreach (string baseDir in bases)
        {
            string bundled = Path.Combine(baseDir, "runtimes", rid, "native", fileName);
            if (File.Exists(bundled))
            {
                return bundled;
            }

            string flat = Path.Combine(baseDir, fileName);
            if (File.Exists(flat))
            {
                return flat;
            }
        }

        if (windows)
        {
            throw new PlatformNotSupportedException(
                $"Unable to find native library [runtimes\\{rid}\\native\\{fileName}] under " +
                $"{AppContext.BaseDirectory}. Phext.Seal needs the liblzma binary bundled " +
                "with the Joveler.Compression.XZ package.");
        }

        return null;
    }

    /// <summary>Seals a phext: xz-compresses its full UTF-8 bytes.</summary>
    public static byte[] Seal(string input)
    {
        return SealBytes(Encoding.UTF8.GetBytes(input));
    }

    /// <summary>Seals raw phext bytes (see <see cref="Seal"/>).</summary>
    public static byte[] SealBytes(byte[] input)
    {
        EnsureInitialized();
        var options = new XZCompressOptions
        {
            // Joveler derives the numeric preset from Level; Level6 is preset 6 (SealPreset).
            Level = LzmaCompLevel.Level6,
            Check = LzmaCheck.Crc64,
            LeaveOpen = true,
        };

        using var output = new MemoryStream();
        using (var stream = new XZStream(output, options))
        {
            stream.Write(input, 0, input.Length);
        }
        return output.ToArray();
    }

    /// <summary>
    /// Unseals a sealed phext back to its exact original bytes. Throws
    /// <see cref="InvalidDataException"/> when the data is not a valid xz stream.
    /// </summary>
    public static byte[] Unseal(byte[] data)
    {
        EnsureInitialized();
        if (data.Length == 0)
        {
            throw new InvalidDataException("Cannot unseal an empty stream: not an xz stream.");
        }

        // DecodeFlags.None: a single xz stream, stopping at its end - the same
        // behaviour as the xz2 decoder used by libphext-rs. (Joveler's default,
        // Concatenated, would also decode any streams appended after the first.)
        var options = new XZDecompressOptions
        {
            DecodeFlags = LzmaDecodingFlag.None,
            LeaveOpen = true,
        };

        try
        {
            using var input = new MemoryStream(data);
            using var stream = new XZStream(input, options);
            using var output = new MemoryStream();
            stream.CopyTo(output);
            return output.ToArray();
        }
        catch (XZException error)
        {
            throw new InvalidDataException("Cannot unseal: not a valid xz stream.", error);
        }
    }

    /// <summary>
    /// Unseals directly to text. Throws <see cref="InvalidDataException"/> when the
    /// unsealed bytes are not valid UTF-8; to unseal foreign content that may carry
    /// non-UTF-8 scrolls, <see cref="Unseal"/> to bytes and pass them through
    /// BinaryScroll.Ingest, which applies the base64-verbatim ingestion rule instead
    /// of failing.
    /// </summary>
    public static string UnsealString(byte[] data)
    {
        byte[] bytes = Unseal(data);
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException error)
        {
            throw new InvalidDataException("Unsealed bytes are not valid UTF-8.", error);
        }
    }
}
