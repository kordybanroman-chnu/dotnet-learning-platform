using Enrollments.Bll;
using Microsoft.AspNetCore.Mvc;

namespace Enrollments.Api.Controllers;

[ApiController]
[Route("api/enrollments")]
public sealed class EnrollmentsController(IEnrollmentService enrollments) : ControllerBase
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
        var created = await enrollments.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("{id:long}/confirm")]
    public async Task<IActionResult> Confirm(long id, CancellationToken ct)
    {
        await enrollments.ConfirmAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
    {
        await enrollments.CancelAsync(id, ct);
        return NoContent();
    }
}
