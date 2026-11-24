// Phext.StoreBench — v0.7.0 performance harness (NOT part of the shipped API).
// Identical source runs against the pristine tree (string path only; store
// types absent -> NOT PRESENT) and the work tree (both stores, via reflection
// so this file compiles unchanged against 0.6.1).
// Workloads per DISCIPLINE.md: A = 2000 sequential inserts, B = 500 tail
// insert/remove pairs, C = 5000 fetches against targets generated once with
// Python random.Random(20261124) (read from --targets file).
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Phext;

string corpusPath = "", targetsPath = "", label = "";
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--corpus") corpusPath = args[i + 1];
    if (args[i] == "--targets") targetsPath = args[i + 1];
    if (args[i] == "--label") label = args[i + 1];
}

string raw = File.ReadAllText(corpusPath, Encoding.UTF8);
var corpus = PhextEngine.Phokenize(raw).Select(ps => ps.Scroll)
    .Where(t => t.Length >= 20 && t.Length <= 1200).ToList();
double mean = corpus.Average(t => t.Length);
Console.WriteLine($"benchmark_stores label='{label}'");
Console.WriteLine($"host={Environment.MachineName} dotnet={Environment.Version} os={Environment.OSVersion}");
Console.WriteLine($"corpus: {corpus.Count} texts from tasks.phext (20..1200 chars, mean {mean:F0}), cycled");
Console.WriteLine("workloads: A=2000 inserts, B=500 insert/remove pairs, C=5000 fetches, seed=20261124");
Console.WriteLine();

string Text(int i) => corpus[i % corpus.Count];
Coordinate CoordA(int i) => new Coordinate(1, 1, 1, 1, 1, 1, 1, (i / 50) % 40 + 1, i % 50 + 1);

var targets = File.ReadAllLines(targetsPath).Where(l => l.Length > 0)
    .Select(l => Coordinate.FromString(l)).ToList();

Type? dynType = Type.GetType("Phext.DynamicPhext, libphext");
Type? traType = Type.GetType("Phext.TransitivePhext, libphext");
var storeTypes = new List<(string, Type)>();
if (dynType != null) storeTypes.Add(("dynamic", dynType));
if (traType != null) storeTypes.Add(("transitive", traType));

dynamic NewStore(Type t, string buffer) =>
    // DynamicPhext's ctor carries the shrinkDivisor test parameter; optional
    // parameters are not matched by Activator with fewer arguments.
    t.Name == "DynamicPhext" ? Activator.CreateInstance(t, buffer, 4)! : Activator.CreateInstance(t, buffer)!;
string Extra(dynamic store, Type t) =>
    t.Name == "DynamicPhext"
        ? $" [capacity {store.Capacity}, resizes {store.ResizeCount}, elements copied {store.ElementsCopied}]"
        : "";

// ---------------- Workload A ----------------
var sw = Stopwatch.StartNew();
string bufA = "";
for (int i = 0; i < 2000; i++) bufA = PhextEngine.Insert(bufA, CoordA(i), Text(i));
sw.Stop();
double aString = sw.Elapsed.TotalSeconds;
Console.WriteLine($"A string      edits: {aString,9:F3}s  (final buffer {bufA.Length} chars)");

foreach (var (name, t) in storeTypes)
{
    dynamic store = NewStore(t, "");
    sw.Restart();
    for (int i = 0; i < 2000; i++) store.Insert(CoordA(i), Text(i));
    sw.Stop();
    double tEdit = sw.Elapsed.TotalSeconds;
    sw.Restart();
    string outSer = store.Serialize();
    sw.Stop();
    double tSer = sw.Elapsed.TotalSeconds;
    if (outSer != bufA) throw new Exception($"{name}: workload A serialization differs from string path");
    Console.WriteLine($"A {name,-11} edits: {tEdit,9:F3}s  serialize: {tSer,7:F3}s  total: {tEdit + tSer,9:F3}s{Extra(store, t)}");
}
foreach (var name in new[] { "dynamic", "transitive" })
    if (storeTypes.All(s => s.Item1 != name)) Console.WriteLine($"A {name,-11} NOT PRESENT in this tree");
Console.WriteLine();

// ---------------- Workload B ----------------
var baseScrolls = Enumerable.Range(0, 256).Select(i => new PositionedScroll(CoordA(i), Text(i))).ToList();
string baseBuf = PhextEngine.Dephokenize(baseScrolls);
var tail = new Coordinate(1, 1, 1, 1, 1, 1, 2, 1, 1);
const string tailText = "tail boundary scroll\nsecond line\n";

string bufB = baseBuf;
sw.Restart();
for (int i = 0; i < 500; i++)
{
    bufB = PhextEngine.Insert(bufB, tail, tailText);
    bufB = PhextEngine.Remove(bufB, tail);
}
sw.Stop();
double bString = sw.Elapsed.TotalSeconds;
if (bufB != baseBuf) throw new Exception("string: workload B end state differs");
Console.WriteLine($"B string      pairs: {bString,9:F3}s");

foreach (var (name, t) in storeTypes)
{
    dynamic store = NewStore(t, baseBuf);
    if (t.Name == "DynamicPhext") store.ResetStats();
    sw.Restart();
    for (int i = 0; i < 500; i++) { store.Insert(tail, tailText); store.Remove(tail); }
    sw.Stop();
    double tPairs = sw.Elapsed.TotalSeconds;
    if ((string)store.Serialize() != baseBuf) throw new Exception($"{name}: workload B end state differs");
    Console.WriteLine($"B {name,-11} pairs: {tPairs,9:F3}s{Extra(store, t)}");
}
foreach (var name in new[] { "dynamic", "transitive" })
    if (storeTypes.All(s => s.Item1 != name)) Console.WriteLine($"B {name,-11} NOT PRESENT in this tree");
Console.WriteLine();

// ---------------- Workload C ----------------
sw.Restart();
var expected = targets.Select(c => PhextEngine.Fetch(bufA, c)).ToList();
sw.Stop();
double cString = sw.Elapsed.TotalSeconds;
Console.WriteLine($"C string      fetches: {cString,9:F3}s");

foreach (var (name, t) in storeTypes)
{
    dynamic store = NewStore(t, bufA);
    sw.Restart();
    var got = targets.Select(c => (string)store.Fetch(c)).ToList();
    sw.Stop();
    double tFetch = sw.Elapsed.TotalSeconds;
    if (!got.SequenceEqual(expected)) throw new Exception($"{name}: workload C fetch results differ");
    Console.WriteLine($"C {name,-11} fetches: {tFetch,9:F3}s");
}
foreach (var name in new[] { "dynamic", "transitive" })
    if (storeTypes.All(s => s.Item1 != name)) Console.WriteLine($"C {name,-11} NOT PRESENT in this tree");
Console.WriteLine();
Console.WriteLine("all cross-path assertions passed (serializations and fetches identical)");
return 0;
