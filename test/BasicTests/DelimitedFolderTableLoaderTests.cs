using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using KustoLoco.Core;

namespace BasicTests;

/// <summary>
/// The folder table loader: a query naming <c>Watchlist</c> is answered by <c>Watchlist.csv</c> on disk, loaded
/// on demand and typed from its header.
/// </summary>
[TestClass]
public class DelimitedFolderTableLoaderTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "kustoloco-folder-tables-" + Guid.NewGuid().ToString("N"));

    public DelimitedFolderTableLoaderTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch { /* best effort */ }
    }

    private void Write(string fileName, string content) =>
        File.WriteAllText(Path.Combine(_folder, fileName), content);

    private KustoQueryContext ContextFor(int maxRows = 1_000_000)
    {
        var context = new KustoQueryContext();
        context.SetTableLoader(new DelimitedFolderTableLoader(_folder, maxRows));
        return context;
    }

    [TestMethod]
    public async Task ServesACsvFileAsATableOfTheSameName()
    {
        Write("Watchlist.csv", "Host,Reason\nevil.example,c2\nok.example,benign\n");
        var result = await ContextFor().RunQuery("Watchlist | where Reason == 'c2' | project Host");
        result.Error.Should().BeNullOrEmpty();
        result.RowCount.Should().Be(1);
        result.GetRow(0)[0]?.ToString().Should().Be("evil.example");
    }

    [TestMethod]
    public async Task TypedHeadersBindNativeTypesNotStrings()
    {
        // Port:int must support a NUMERIC predicate — the point of declaring types in the header.
        Write("Ports.csv", "Service,Port:int\nssh,22\nhttps,443\n");
        var result = await ContextFor().RunQuery("Ports | where Port > 100 | project Service");
        result.Error.Should().BeNullOrEmpty();
        result.RowCount.Should().Be(1);
        result.GetRow(0)[0]?.ToString().Should().Be("https");
    }

    [TestMethod]
    public async Task ReadsGzippedFiles()
    {
        var path = Path.Combine(_folder, "Big.csv.gz");
        using (var file = File.Create(path))
        using (var gz = new GZipStream(file, CompressionLevel.Optimal))
        using (var writer = new StreamWriter(gz, Encoding.UTF8))
            writer.Write("Name\nalice\nbob\n");

        var result = await ContextFor().RunQuery("Big | count");
        result.Error.Should().BeNullOrEmpty();
        result.GetRow(0)[0]?.ToString().Should().Be("2");
    }

    [TestMethod]
    public async Task SplitsOnTheDelimiterImpliedByTheExtension()
    {
        Write("Tabbed.tsv", "Name\tRole\nalice\tadmin\n");
        var result = await ContextFor().RunQuery("Tabbed | project Role");
        result.Error.Should().BeNullOrEmpty();
        result.GetRow(0)[0]?.ToString().Should().Be("admin");
    }

    [TestMethod]
    public async Task CapsRowsAtMaxRows()
    {
        Write("Many.csv", "Name\n" + string.Join("\n", new[] { "a", "b", "c", "d", "e" }) + "\n");
        var result = await ContextFor(maxRows: 2).RunQuery("Many | count");
        result.GetRow(0)[0]?.ToString().Should().Be("2");
    }

    [TestMethod]
    public async Task ATableAlreadyInTheContextIsNotOverridden()
    {
        Write("Shared.csv", "Name\nfromdisk\n");
        var context = ContextFor();
        context.CopyDataIntoTable("Shared", new[] { new { Name = "inmemory" } });
        var result = await context.RunQuery("Shared | project Name");
        result.GetRow(0)[0]?.ToString().Should().Be("inmemory"); // host-supplied data wins
    }

    [TestMethod]
    public async Task AnUnknownTableIsReportedNotSilentlyEmpty()
    {
        // A missing watchlist must never look like "matched nothing" — the query fails with the engine's error.
        var result = await ContextFor().RunQuery("NoSuchTable | count");
        result.Error.Should().NotBeEmpty();
    }

    [TestMethod]
    public void AnUnknownDeclaredTypeIsRejected()
    {
        Write("Bad.csv", "Name,Value:frobnicate\nx,1\n");
        var loader = new DelimitedFolderTableLoader(_folder);
        var context = new KustoQueryContext();
        var act = () => loader.LoadTablesAsync(context, new[] { "Bad" }).GetAwaiter().GetResult();
        act.Should().Throw<InvalidOperationException>().WithMessage("*unknown type*");
    }

    [TestMethod]
    public async Task APerTableOverrideWinsOverTheFolder()
    {
        Write("Watchlist.csv", "Host\nfromfolder\n");
        var elsewhere = Path.Combine(_folder, "somewhere-else.csv");
        File.WriteAllText(elsewhere, "Host\nfromoverride\n");

        var context = new KustoQueryContext();
        context.SetTableLoader(new DelimitedFolderTableLoader(
            _folder, tableFiles: new Dictionary<string, string> { ["Watchlist"] = elsewhere }));

        var result = await context.RunQuery("Watchlist | project Host");
        result.GetRow(0)[0]?.ToString().Should().Be("fromoverride");
    }

    [TestMethod]
    public void FindFileLocatesTheBackingFileOrReturnsNull()
    {
        Write("Present.csv", "Name\nx\n");
        var loader = new DelimitedFolderTableLoader(_folder);
        loader.FindFile("Present").Should().NotBeNull();
        loader.FindFile("Absent").Should().BeNull();
    }
}
