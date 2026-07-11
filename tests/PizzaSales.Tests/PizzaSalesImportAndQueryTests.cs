using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PizzaSales.Application;
using PizzaSales.Infrastructure;

namespace PizzaSales.Tests;

public sealed class PizzaSalesImportAndQueryTests : IAsyncLifetime
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"pizza-sales-{Guid.NewGuid():N}.db");
    private PizzaSalesDbContext dbContext = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<PizzaSalesDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        dbContext = new PizzaSalesDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await dbContext.DisposeAsync();
        SqliteConnection.ClearAllPools();
        File.Delete(databasePath);
    }

    [Fact]
    public async Task Imports_archive_and_exposes_expected_sales_totals()
    {
        var archivePath = Path.Combine(AppContext.BaseDirectory, "data", "pizza_place_sales_archive.zip");
        await using var archive = File.OpenRead(archivePath);

        var result = await new PizzaSalesImportService(dbContext).ImportAsync(archive, replaceExisting: false, CancellationToken.None);
        var dashboard = new SalesQueryService(dbContext);
        var summary = await dashboard.GetSummaryAsync(null, null, CancellationToken.None);
        var sales = await dashboard.GetSalesAsync(new SalesQuery(null, null, null, "Chicken", null, 1, 10, "lineTotal", "desc"), CancellationToken.None);

        Assert.Equal(new ImportResult(32, 96, 21_350, 48_620), result);
        Assert.Equal(21_350, summary.TotalOrders);
        Assert.Equal(49_574, summary.TotalPizzasSold);
        Assert.True(summary.TotalRevenue > 0);
        Assert.Equal(10, sales.Items.Count);
        Assert.All(sales.Items, line => Assert.Equal("Chicken", line.Category));
    }

    [Fact]
    public async Task Rejects_archive_missing_a_required_csv()
    {
        await using var archive = new MemoryStream();
        using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("orders.csv");
            await using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync("order_id,date,time\n1,2015-01-01,10:00:00\n");
        }

        archive.Position = 0;
        var exception = await Assert.ThrowsAsync<ImportValidationException>(() =>
            new PizzaSalesImportService(dbContext).ImportAsync(archive, replaceExisting: false, CancellationToken.None));

        Assert.Contains("pizza_types.csv", exception.Message, StringComparison.Ordinal);
        Assert.Empty(await dbContext.Orders.ToListAsync());
    }

    [Fact]
    public async Task Rejects_repeat_import_without_explicit_replacement()
    {
        var archivePath = Path.Combine(AppContext.BaseDirectory, "data", "pizza_place_sales_archive.zip");
        await using (var firstArchive = File.OpenRead(archivePath))
        {
            await new PizzaSalesImportService(dbContext).ImportAsync(firstArchive, replaceExisting: false, CancellationToken.None);
        }

        await using var secondArchive = File.OpenRead(archivePath);
        await Assert.ThrowsAsync<DataAlreadyImportedException>(() =>
            new PizzaSalesImportService(dbContext).ImportAsync(secondArchive, replaceExisting: false, CancellationToken.None));
    }
}
