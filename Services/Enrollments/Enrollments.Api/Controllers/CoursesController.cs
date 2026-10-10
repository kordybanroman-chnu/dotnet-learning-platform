using Enrollments.Bll;
using Microsoft.AspNetCore.Mvc;

namespace Enrollments.Api.Controllers;

[ApiController]
[Route("api/courses")]
public sealed class CoursesController(ICourseService courses) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CourseDto>>> GetAll(CancellationToken ct)
        => Ok(await courses.GetAllAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<CourseDto>> GetById(long id, CancellationToken ct)
        => Ok(await courses.GetByIdAsync(id, ct));
}
