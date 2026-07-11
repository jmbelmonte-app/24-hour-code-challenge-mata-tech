using Microsoft.AspNetCore.Mvc;
using PizzaSales.Application;

namespace PizzaSales.Api.Controllers;

[ApiController]
[Route("api/imports")]
public sealed class ImportsController(IPizzaSalesImportService importService) : ControllerBase
{
    [HttpPost("pizza-sales")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_000_000)]
    [ProducesResponseType(typeof(ImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ImportResult>> Import(
        [FromForm] IFormFile? archive,
        [FromQuery] bool replaceExisting,
        CancellationToken cancellationToken)
    {
        if (archive is null || archive.Length == 0)
        {
            return BadRequest(new ProblemDetails { Title = "A non-empty ZIP archive is required." });
        }

        if (!string.Equals(Path.GetExtension(archive.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails { Title = "The uploaded file must be a ZIP archive." });
        }

        try
        {
            await using var stream = archive.OpenReadStream();
            return Ok(await importService.ImportAsync(stream, replaceExisting, cancellationToken));
        }
        catch (ImportValidationException exception)
        {
            return BadRequest(new ProblemDetails { Title = "The sales archive could not be imported.", Detail = exception.Message });
        }
        catch (DataAlreadyImportedException exception)
        {
            return Conflict(new ProblemDetails { Title = "Sales data is already available.", Detail = exception.Message });
        }
    }
}
