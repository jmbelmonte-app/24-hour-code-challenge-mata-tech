namespace PizzaSales.Application;

public sealed record ImportResult(int PizzaTypes, int Pizzas, int Orders, int OrderItems);

public sealed class ImportValidationException(string message) : Exception(message)
{
}

public sealed class DataAlreadyImportedException : Exception
{
    public DataAlreadyImportedException()
        : base("Sales data is already imported. Use replaceExisting=true to replace it.")
    {
    }
}

public interface IPizzaSalesImportService
{
    Task<ImportResult> ImportAsync(Stream archive, bool replaceExisting, CancellationToken cancellationToken);
}

public sealed record SalesQuery(
    string? Search,
    DateOnly? From,
    DateOnly? To,
    string? Category,
    string? Size,
    int Page = 1,
    int PageSize = 25,
    string SortBy = "orderDate",
    string SortDirection = "desc");

public sealed record SalesLineDto(
    int OrderId,
    DateOnly OrderDate,
    TimeOnly OrderTime,
    string PizzaId,
    string PizzaName,
    string Category,
    string Size,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record DashboardSummaryDto(decimal TotalRevenue, int TotalOrders, int TotalPizzasSold, decimal AverageOrderValue);
public sealed record SalesTrendPointDto(string Period, decimal Revenue, int Orders);
public sealed record TopPizzaDto(string PizzaId, string PizzaName, string Category, int Quantity, decimal Revenue);

public interface ISalesQueryService
{
    Task<PagedResult<SalesLineDto>> GetSalesAsync(SalesQuery query, CancellationToken cancellationToken);
    Task<DashboardSummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    Task<IReadOnlyList<SalesTrendPointDto>> GetSalesTrendAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    Task<IReadOnlyList<TopPizzaDto>> GetTopPizzasAsync(DateOnly? from, DateOnly? to, int limit, CancellationToken cancellationToken);
}
