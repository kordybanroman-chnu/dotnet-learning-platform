using Enrollments.Bll;
using Microsoft.AspNetCore.Mvc;

namespace Enrollments.Api.Controllers;

[ApiController]
[Route("api/students")]
public sealed class StudentsController(IStudentService students) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StudentDto>>> GetAll(CancellationToken ct)
        => Ok(await students.GetAllAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<StudentDto>> GetById(long id, CancellationToken ct)
        => Ok(await students.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<StudentDto>> Create(CreateStudentDto input, CancellationToken ct)
    {
        var created = await students.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await students.DeleteAsync(id, ct);
        return NoContent();
    }
}
