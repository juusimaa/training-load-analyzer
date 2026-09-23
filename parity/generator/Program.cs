using System.Globalization;
using System.Text.Json.Nodes;
using ParityGenerator;

// Regenerates every golden from every fixture (parity.md §1). Rewrites parity/golden and nothing
// else; exits non-zero if any fixture fails to load or run, so a broken fixture never leaves a
// partial set of goldens looking complete.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

var parity = FindParityDirectory();
var fixtures = Path.Combine(parity, "fixtures");
var golden = Path.Combine(parity, "golden");

if (Directory.Exists(golden))
{
    Directory.Delete(golden, recursive: true);
}

var written = 0;
var failures = new List<string>();

async Task Produce(string label, string target, Func<Task<JsonNode>> build)
{
    try
    {
        var node = await build();
        var text = node.ToJsonString();

        // A golden is committed, so a token must never reach one (009 FR-010).
        if (text.Contains("test-access", StringComparison.Ordinal)
            || text.Contains("test-refresh", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{label}: a token value reached the golden.");
        }

        GoldenJson.Write(target, node);
        written++;
        Console.WriteLine($"  {Path.GetRelativePath(parity, target)}");
    }
    catch (Exception failure)
    {
        failures.Add($"{label}: {failure}");
    }
}

Console.WriteLine("histories:");
foreach (var path in Directory.GetFiles(Path.Combine(fixtures, "histories"), "*.json").Order(StringComparer.Ordinal))
{
    var name = Path.GetFileNameWithoutExtension(path);
    await Produce(name, Path.Combine(golden, "histories", $"{name}.json"),
        async () => await HistoryGolden.BuildAsync(HistoryFixture.Load(path)));
}

Console.WriteLine("sync:");
foreach (var directory in Directory.GetDirectories(Path.Combine(fixtures, "sync")).Order(StringComparer.Ordinal))
{
    var name = Path.GetFileName(directory);
    await Produce(name, Path.Combine(golden, "sync", $"{name}.json"),
        async () => await SyncGolden.BuildAsync(SyncScenario.Load(directory)));
}

Console.WriteLine("probes:");
await Produce("display", Path.Combine(golden, "probes", "display.json"),
    () => Task.FromResult<JsonNode>(ProbeGolden.Display(Path.Combine(fixtures, "probes", "display.json"))));
await Produce("surfaces", Path.Combine(golden, "probes", "surfaces.json"),
    async () => await ProbeGolden.SurfacesAsync());

if (failures.Count > 0)
{
    Console.Error.WriteLine($"{failures.Count} fixture(s) failed:");
    failures.ForEach(Console.Error.WriteLine);

    return 1;
}

Console.WriteLine($"{written} goldens written.");

return 0;

static string FindParityDirectory()
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "parity", "fixtures");

            if (Directory.Exists(candidate))
            {
                return Path.Combine(directory.FullName, "parity");
            }
        }
    }

    throw new DirectoryNotFoundException("parity/fixtures was not found above the current directory.");
}
