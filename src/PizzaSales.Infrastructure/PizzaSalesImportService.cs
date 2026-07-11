using System.Globalization;
using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PizzaSales.Application;
using PizzaSales.Domain;

namespace PizzaSales.Infrastructure;

public sealed class PizzaSalesImportService(PizzaSalesDbContext dbContext) : IPizzaSalesImportService
{
    private static readonly string[] PizzaTypeHeaders = ["pizza_type_id", "name", "category", "ingredients"];
    private static readonly string[] PizzaHeaders = ["pizza_id", "pizza_type_id", "size", "price"];
    private static readonly string[] OrderHeaders = ["order_id", "date", "time"];
    private static readonly string[] OrderItemHeaders = ["order_details_id", "order_id", "pizza_id", "quantity"];

    public async Task<ImportResult> ImportAsync(Stream archive, bool replaceExisting, CancellationToken cancellationToken)
    {
        var importData = ReadArchive(archive);

        if (!replaceExisting && await dbContext.Orders.AnyAsync(cancellationToken))
        {
            throw new DataAlreadyImportedException();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (replaceExisting)
        {
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM \"OrderItems\";", cancellationToken);
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM \"Orders\";", cancellationToken);
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM \"Pizzas\";", cancellationToken);
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM \"PizzaTypes\";", cancellationToken);
        }

        dbContext.PizzaTypes.AddRange(importData.PizzaTypes);
        dbContext.Pizzas.AddRange(importData.Pizzas);
        dbContext.Orders.AddRange(importData.Orders);
        await dbContext.SaveChangesAsync(cancellationToken);

        var autoDetectChanges = dbContext.ChangeTracker.AutoDetectChangesEnabled;
        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            foreach (var batch in importData.OrderItems.Chunk(1_000))
            {
                dbContext.OrderItems.AddRange(batch);
                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
            }
        }
        finally
        {
            dbContext.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
        }

        await transaction.CommitAsync(cancellationToken);
        return new ImportResult(importData.PizzaTypes.Count, importData.Pizzas.Count, importData.Orders.Count, importData.OrderItems.Count);
    }

    private static ImportData ReadArchive(Stream archive)
    {
        try
        {
            using var zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: false);
            var entries = zip.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .ToDictionary(entry => entry.Name, StringComparer.OrdinalIgnoreCase);

            var pizzaTypeRows = ReadTable(entries, "pizza_types.csv", PizzaTypeHeaders);
            var pizzaRows = ReadTable(entries, "pizzas.csv", PizzaHeaders);
            var orderRows = ReadTable(entries, "orders.csv", OrderHeaders);
            var orderItemRows = ReadTable(entries, "order_details.csv", OrderItemHeaders);

            var pizzaTypes = pizzaTypeRows.Select(row => new PizzaType
            {
                Id = Required(row, "pizza_type_id"),
                Name = Required(row, "name"),
                Category = Required(row, "category"),
                Ingredients = Required(row, "ingredients")
            }).ToList();
            EnsureUnique(pizzaTypes, pizzaType => pizzaType.Id, "pizza_type_id");

            var pizzas = pizzaRows.Select(row => new Pizza
            {
                Id = Required(row, "pizza_id"),
                PizzaTypeId = Required(row, "pizza_type_id"),
                Size = Required(row, "size"),
                CurrentPriceCents = ParsePriceCents(row, "price")
            }).ToList();
            EnsureUnique(pizzas, pizza => pizza.Id, "pizza_id");

            var orders = orderRows.Select(row => new Order
            {
                Id = ParsePositiveInt(row, "order_id"),
                OrderDate = ParseDate(row, "date"),
                OrderTime = ParseTime(row, "time")
            }).ToList();
            EnsureUnique(orders, order => order.Id, "order_id");

            var pizzaIds = pizzas.Select(pizza => pizza.Id).ToHashSet(StringComparer.Ordinal);
            var pizzaTypeIds = pizzaTypes.Select(pizzaType => pizzaType.Id).ToHashSet(StringComparer.Ordinal);
            var orderIds = orders.Select(order => order.Id).ToHashSet();

            foreach (var pizza in pizzas)
            {
                if (!pizzaTypeIds.Contains(pizza.PizzaTypeId))
                {
                    throw new ImportValidationException($"Pizza '{pizza.Id}' references unknown pizza type '{pizza.PizzaTypeId}'.");
                }
            }

            var pizzaPriceById = pizzas.ToDictionary(pizza => pizza.Id, pizza => pizza.CurrentPriceCents, StringComparer.Ordinal);
            var orderItems = orderItemRows.Select(row => new OrderItem
            {
                Id = ParsePositiveInt(row, "order_details_id"),
                OrderId = ParsePositiveInt(row, "order_id"),
                PizzaId = Required(row, "pizza_id"),
                Quantity = ParsePositiveInt(row, "quantity"),
                UnitPriceCents = 0
            }).ToList();
            EnsureUnique(orderItems, orderItem => orderItem.Id, "order_details_id");

            foreach (var item in orderItems)
            {
                if (!orderIds.Contains(item.OrderId))
                {
                    throw new ImportValidationException($"Order item '{item.Id}' references unknown order '{item.OrderId}'.");
                }

                if (!pizzaIds.Contains(item.PizzaId))
                {
                    throw new ImportValidationException($"Order item '{item.Id}' references unknown pizza '{item.PizzaId}'.");
                }
            }

            orderItems = orderItems.Select(item => new OrderItem
            {
                Id = item.Id,
                OrderId = item.OrderId,
                PizzaId = item.PizzaId,
                Quantity = item.Quantity,
                UnitPriceCents = pizzaPriceById[item.PizzaId]
            }).ToList();

            return new ImportData(pizzaTypes, pizzas, orders, orderItems);
        }
        catch (InvalidDataException exception)
        {
            throw new ImportValidationException($"The uploaded archive is invalid: {exception.Message}");
        }
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> ReadTable(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        string fileName,
        IReadOnlyList<string> expectedHeaders)
    {
        if (!entries.TryGetValue(fileName, out var entry))
        {
            throw new ImportValidationException($"The archive must contain '{fileName}'.");
        }

        using var stream = entry.Open();
        var rows = ParseCsv(stream);
        if (rows.Count == 0 || !rows[0].SequenceEqual(expectedHeaders, StringComparer.Ordinal))
        {
            throw new ImportValidationException($"'{fileName}' does not contain the expected CSV headers.");
        }

        return rows.Skip(1).Select((values, index) =>
        {
            if (values.Length != expectedHeaders.Count)
            {
                throw new ImportValidationException($"'{fileName}' row {index + 2} has an unexpected number of columns.");
            }

            return (IReadOnlyDictionary<string, string>)expectedHeaders
                .Select((header, column) => new KeyValuePair<string, string>(header, values[column]))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }).ToList();
    }

    private static List<string[]> ParseCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        var rows = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var hasContent = false;

        int character;
        while ((character = reader.Read()) != -1)
        {
            hasContent = true;
            if (character == '"')
            {
                if (inQuotes && reader.Peek() == '"')
                {
                    field.Append('"');
                    reader.Read();
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (character == ',' && !inQuotes)
            {
                fields.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (character == '\n' && !inQuotes)
            {
                fields.Add(field.ToString());
                rows.Add(fields.ToArray());
                fields.Clear();
                field.Clear();
                hasContent = false;
                continue;
            }

            if (character != '\r')
            {
                field.Append((char)character);
            }
        }

        if (inQuotes)
        {
            throw new ImportValidationException("A CSV value has an unclosed quote.");
        }

        if (hasContent || fields.Count > 0 || field.Length > 0)
        {
            fields.Add(field.ToString());
            rows.Add(fields.ToArray());
        }

        return rows;
    }

    private static string Required(IReadOnlyDictionary<string, string> row, string column)
    {
        var value = row[column].Trim();
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ImportValidationException($"Column '{column}' must have a value.");
    }

    private static int ParsePositiveInt(IReadOnlyDictionary<string, string> row, string column) =>
        int.TryParse(Required(row, column), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : throw new ImportValidationException($"Column '{column}' must be a positive whole number.");

    private static int ParsePriceCents(IReadOnlyDictionary<string, string> row, string column)
    {
        if (!decimal.TryParse(Required(row, column), NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price < 0)
        {
            throw new ImportValidationException($"Column '{column}' must be a non-negative price.");
        }

        var cents = price * 100;
        return cents == decimal.Truncate(cents)
            ? decimal.ToInt32(cents)
            : throw new ImportValidationException($"Column '{column}' must have no more than two decimal places.");
    }

    private static DateOnly ParseDate(IReadOnlyDictionary<string, string> row, string column) =>
        DateOnly.TryParseExact(Required(row, column), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : throw new ImportValidationException($"Column '{column}' must use YYYY-MM-DD format.");

    private static TimeOnly ParseTime(IReadOnlyDictionary<string, string> row, string column) =>
        TimeOnly.TryParseExact(Required(row, column), "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : throw new ImportValidationException($"Column '{column}' must use HH:MM:SS format.");

    private static void EnsureUnique<T, TKey>(IEnumerable<T> values, Func<T, TKey> keySelector, string column) where TKey : notnull
    {
        if (values.GroupBy(keySelector).Any(group => group.Count() > 1))
        {
            throw new ImportValidationException($"Column '{column}' contains duplicate values.");
        }
    }

    private sealed record ImportData(
        List<PizzaType> PizzaTypes,
        List<Pizza> Pizzas,
        List<Order> Orders,
        List<OrderItem> OrderItems);
}
