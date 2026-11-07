using Microsoft.AspNetCore.Mvc;
using Platform.Aggregator.Models;
using Platform.Aggregator.Services;

namespace Platform.Aggregator.Controllers;

[ApiController]
[Route("api/aggregator")]
public sealed class AggregatorController(EnrollmentAggregator aggregator) : ControllerBase
{
    [HttpGet("enrollments/{id:long}/full")]
    [ProducesResponseType<FullEnrollmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FullEnrollmentDto>> GetFullEnrollment(long id, CancellationToken ct)
    {
        var result = await aggregator.GetFullEnrollmentAsync(id, ct);
        if (result is null)
            return NotFound(new ProblemDetails
            {
                Title = "Enrollment not found",
                Detail = $"Enrollment {id} was not found."
            });

        return Ok(result);
    }
}
