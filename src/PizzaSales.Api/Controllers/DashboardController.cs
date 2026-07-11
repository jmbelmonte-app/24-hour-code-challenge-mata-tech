using Microsoft.AspNetCore.Mvc;
using PizzaSales.Application;

namespace PizzaSales.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(ISalesQueryService salesQueryService) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await salesQueryService.GetSummaryAsync(from, to, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "The dashboard query is invalid.", Detail = exception.Message });
        }
    }

    [HttpGet("sales-trend")]
    public async Task<ActionResult<IReadOnlyList<SalesTrendPointDto>>> GetSalesTrend([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await salesQueryService.GetSalesTrendAsync(from, to, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "The dashboard query is invalid.", Detail = exception.Message });
        }
    }

    [HttpGet("top-pizzas")]
    public async Task<ActionResult<IReadOnlyList<TopPizzaDto>>> GetTopPizzas(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await salesQueryService.GetTopPizzasAsync(from, to, limit, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "The dashboard query is invalid.", Detail = exception.Message });
        }
    }
}
