using Microsoft.EntityFrameworkCore;
using PizzaSales.Application;

namespace PizzaSales.Infrastructure;

public sealed class SalesQueryService(PizzaSalesDbContext dbContext) : ISalesQueryService
{
    public async Task<PagedResult<SalesLineDto>> GetSalesAsync(SalesQuery query, CancellationToken cancellationToken)
    {
        ValidateSalesQuery(query);
        var filtered = FilterLines(query.From, query.To, query.Search, query.Category, query.Size);
        var totalCount = await filtered.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var ordered = OrderSales(filtered, query.SortBy, query.SortDirection);
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(line => new SalesLineDto(
                line.OrderId,
                line.OrderDate,
                line.OrderTime,
                line.PizzaId,
                line.PizzaName,
                line.Category,
                line.Size,
                line.Quantity,
                line.UnitPriceCents / 100m,
                line.Quantity * line.UnitPriceCents / 100m))
            .ToListAsync(cancellationToken);

        return new PagedResult<SalesLineDto>(items, page, pageSize, totalCount, (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        ValidateDateRange(from, to);
        var lines = FilterLines(from, to, null, null, null);
        var totalOrders = await lines.Select(line => line.OrderId).Distinct().CountAsync(cancellationToken);
        var quantities = await lines.Select(line => line.Quantity).ToListAsync(cancellationToken);
        var revenueCents = await lines.Select(line => line.Quantity * line.UnitPriceCents).ToListAsync(cancellationToken);
        var totalRevenue = revenueCents.Sum() / 100m;
        return new DashboardSummaryDto(totalRevenue, totalOrders, quantities.Sum(), totalOrders == 0 ? 0 : totalRevenue / totalOrders);
    }

    public async Task<IReadOnlyList<SalesTrendPointDto>> GetSalesTrendAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        ValidateDateRange(from, to);
        var grouped = await FilterLines(from, to, null, null, null)
            .GroupBy(line => new { line.OrderDate.Year, line.OrderDate.Month })
            .OrderBy(group => group.Key.Year).ThenBy(group => group.Key.Month)
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                RevenueCents = group.Sum(line => line.Quantity * line.UnitPriceCents),
                Orders = group.Select(line => line.OrderId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        return grouped
            .Select(group => new SalesTrendPointDto($"{group.Year:D4}-{group.Month:D2}", group.RevenueCents / 100m, group.Orders))
            .ToList();
    }

    public async Task<IReadOnlyList<TopPizzaDto>> GetTopPizzasAsync(DateOnly? from, DateOnly? to, int limit, CancellationToken cancellationToken)
    {
        ValidateDateRange(from, to);
        if (limit is < 1 or > 20)
        {
            throw new ArgumentException("limit must be between 1 and 20.");
        }

        var grouped = await FilterLines(from, to, null, null, null)
            .GroupBy(line => new { line.PizzaId, line.PizzaName, line.Category })
            .Select(group => new
            {
                group.Key.PizzaId,
                group.Key.PizzaName,
                group.Key.Category,
                Quantity = group.Sum(line => line.Quantity),
                RevenueCents = group.Sum(line => line.Quantity * line.UnitPriceCents)
            })
            .OrderByDescending(item => item.Quantity).ThenBy(item => item.PizzaName)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return grouped
            .Select(item => new TopPizzaDto(item.PizzaId, item.PizzaName, item.Category, item.Quantity, item.RevenueCents / 100m))
            .ToList();
    }

    private IQueryable<SalesLine> FilterLines(DateOnly? from, DateOnly? to, string? search, string? category, string? size)
    {
        var query =
            from item in dbContext.OrderItems.AsNoTracking()
            join order in dbContext.Orders.AsNoTracking() on item.OrderId equals order.Id
            join pizza in dbContext.Pizzas.AsNoTracking() on item.PizzaId equals pizza.Id
            join pizzaType in dbContext.PizzaTypes.AsNoTracking() on pizza.PizzaTypeId equals pizzaType.Id
            select new SalesLine
            {
                OrderId = order.Id,
                OrderDate = order.OrderDate,
                OrderTime = order.OrderTime,
                PizzaId = pizza.Id,
                PizzaName = pizzaType.Name,
                Category = pizzaType.Category,
                Size = pizza.Size,
                Quantity = item.Quantity,
                UnitPriceCents = item.UnitPriceCents
            };

        if (from is not null)
        {
            query = query.Where(line => line.OrderDate >= from);
        }

        if (to is not null)
        {
            query = query.Where(line => line.OrderDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(line => line.Category == category.Trim());
        }

        if (!string.IsNullOrWhiteSpace(size))
        {
            query = query.Where(line => line.Size == size.Trim().ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(line => line.PizzaName.Contains(term) || line.PizzaId.Contains(term) || line.Category.Contains(term));
        }

        return query;
    }

    private static IOrderedQueryable<SalesLine> OrderSales(IQueryable<SalesLine> query, string sortBy, string sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        return sortBy.ToLowerInvariant() switch
        {
            "pizzaname" => descending ? query.OrderByDescending(line => line.PizzaName) : query.OrderBy(line => line.PizzaName),
            "quantity" => descending ? query.OrderByDescending(line => line.Quantity) : query.OrderBy(line => line.Quantity),
            "linetotal" => descending ? query.OrderByDescending(line => line.Quantity * line.UnitPriceCents) : query.OrderBy(line => line.Quantity * line.UnitPriceCents),
            _ => descending
                ? query.OrderByDescending(line => line.OrderDate).ThenByDescending(line => line.OrderTime).ThenByDescending(line => line.OrderId)
                : query.OrderBy(line => line.OrderDate).ThenBy(line => line.OrderTime).ThenBy(line => line.OrderId)
        };
    }

    private static void ValidateSalesQuery(SalesQuery query)
    {
        ValidateDateRange(query.From, query.To);
        if (query.Page < 1 || query.PageSize is < 1 or > 100)
        {
            throw new ArgumentException("page must be at least 1 and pageSize must be between 1 and 100.");
        }

        if (!new[] { "orderDate", "pizzaName", "quantity", "lineTotal" }.Contains(query.SortBy, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("sortBy must be orderDate, pizzaName, quantity, or lineTotal.");
        }

        if (!new[] { "asc", "desc" }.Contains(query.SortDirection, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("sortDirection must be asc or desc.");
        }
    }

    private static void ValidateDateRange(DateOnly? from, DateOnly? to)
    {
        if (from is not null && to is not null && from > to)
        {
            throw new ArgumentException("from must be on or before to.");
        }
    }

    private sealed class SalesLine
    {
        public int OrderId { get; init; }
        public DateOnly OrderDate { get; init; }
        public TimeOnly OrderTime { get; init; }
        public required string PizzaId { get; init; }
        public required string PizzaName { get; init; }
        public required string Category { get; init; }
        public required string Size { get; init; }
        public int Quantity { get; init; }
        public int UnitPriceCents { get; init; }
    }
}
