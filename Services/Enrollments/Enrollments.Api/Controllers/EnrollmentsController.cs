using System.Diagnostics;
using Enrollments.Api.Metrics;
using Enrollments.Bll;
using Microsoft.AspNetCore.Mvc;

namespace Enrollments.Api.Controllers;

[ApiController]
[Route("api/enrollments")]
public sealed class EnrollmentsController(IEnrollmentService enrollments, EnrollmentMetrics metrics) : ControllerBase
{
    [HttpGet("{id:long}")]
    public async Task<ActionResult<EnrollmentDto>> GetById(long id, CancellationToken ct)
        => Ok(await enrollments.GetByIdAsync(id, ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EnrollmentDto>>> GetByStudent(
        [FromQuery] long studentId, CancellationToken ct)
        => Ok(await enrollments.GetByStudentAsync(studentId, ct));

    [HttpPost]
    public async Task<ActionResult<EnrollmentDto>> Create(CreateEnrollmentDto input, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        try
        {
            var created = await enrollments.CreateAsync(input, ct);
            metrics.Created(created.Status);
            success = true;
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        finally
        {
            metrics.RecordDuration("create", success, stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("{id:long}/confirm")]
    public async Task<IActionResult> Confirm(long id, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        try
        {
            await enrollments.ConfirmAsync(id, ct);
            metrics.Confirmed();
            success = true;
            return NoContent();
        }
        finally
        {
            metrics.RecordDuration("confirm", success, stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        try
        {
            await enrollments.CancelAsync(id, ct);
            metrics.Cancelled();
            success = true;
            return NoContent();
        }
        finally
        {
            metrics.RecordDuration("cancel", success, stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
