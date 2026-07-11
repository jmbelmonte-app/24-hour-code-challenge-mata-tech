using Microsoft.AspNetCore.Mvc;
using PizzaSales.Application;

namespace PizzaSales.Api.Controllers;

[ApiController]
[Route("api/sales")]
public sealed class SalesController(ISalesQueryService salesQueryService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SalesLineDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<SalesLineDto>>> GetSales([FromQuery] SalesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var query = new SalesQuery(
                request.Search,
                request.From,
                request.To,
                request.Category,
                request.Size,
                request.Page,
                request.PageSize,
                request.SortBy,
                request.SortDirection);
            return Ok(await salesQueryService.GetSalesAsync(query, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "The sales query is invalid.", Detail = exception.Message });
        }
    }

    public sealed class SalesRequest
    {
        public string? Search { get; init; }
        public DateOnly? From { get; init; }
        public DateOnly? To { get; init; }
        public string? Category { get; init; }
        public string? Size { get; init; }
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 25;
        public string SortBy { get; init; } = "orderDate";
        public string SortDirection { get; init; } = "desc";
    }
}
