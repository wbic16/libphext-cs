// ===========================================================================================================
// libphext-cs: The Fourth Root - Benchmark
// Times the whole-document operations on a phext file.
//
// Usage: dotnet run -c Release --project bench/Phext.Bench -- <file.phext> [coordinate] [--out <dir>]
//        --out writes each result to <dir> so two builds can be compared byte for byte.
//
// Copyright: (c) 2024-2025 Will Bickford (Phext, Inc.)
// License: MIT
// ===========================================================================================================

using System.Diagnostics;
using System.Text;
using Phext;

string? outDir = null;
int outFlag = Array.IndexOf(args, "--out");
if (outFlag >= 0 && outFlag + 1 < args.Length)
{
    outDir = args[outFlag + 1];
    args = args.Where((_, i) => i != outFlag && i != outFlag + 1).ToArray();
    Directory.CreateDirectory(outDir);
}

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: Phext.Bench <file.phext> [coordinate] [--out <dir>]");
    return 1;
}

string path = args[0];
string phext = File.ReadAllText(path, Encoding.UTF8);
Console.WriteLine($"{Path.GetFileName(path)}: {new FileInfo(path).Length:N0} bytes, {phext.Length:N0} chars");

T Time<T>(string name, Func<T> op, Func<T, string> describe)
{
    var watch = Stopwatch.StartNew();
    T result = op();
    watch.Stop();
    Console.WriteLine($"  {name,-8} {watch.Elapsed.TotalMilliseconds,12:N1} ms  {describe(result)}");
    if (outDir != null)
    {
        string text = result is string s ? s : string.Concat(((Dictionary<Coordinate, string>)(object)result!)
            .OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}\t{kv.Value}\n"));
        File.WriteAllText(Path.Combine(outDir, $"{Path.GetFileName(path)}.{name}"), text, new UTF8Encoding(false));
    }
    return result;
}

var map = Time("explode", () => PhextEngine.Explode(phext), m => $"{m.Count:N0} scrolls");
var rebuilt = Time("implode", () => PhextEngine.Implode(map), s => $"{s.Length:N0} chars");
Time("textmap", () => PhextEngine.Textmap(phext), s => $"{s.Length:N0} chars");
Time("index", () => PhextEngine.Index(phext), s => $"{s.Length:N0} chars");

var target = args.Length > 1 ? Coordinate.FromString(args[1]) : map.Keys.Max()!;
Time("fetch", () => PhextEngine.Fetch(phext, target), s => $"{target}: {s.Length:N0} chars");
return 0;
